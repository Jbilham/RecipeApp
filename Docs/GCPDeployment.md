# 🚀 Deploying RecipeApp to Google Cloud Platform

This guide walks through everything needed to ship the combined ASP.NET API + React frontend from GitHub to Cloud Run using Cloud Build. Follow the steps in order; every command assumes you are authenticated with the `gcloud` CLI and have selected the correct project.

---

## 1. Prerequisites

1. **GCP project** (or create one) and set it as default:
   ```bash
   gcloud config set project YOUR_PROJECT_ID
   ```
2. **Billing enabled** on that project.
3. **gcloud CLI** ≥ 453.
4. **GitHub repo** containing this codebase (Cloud Build connects directly).

---

## 2. Enable services & create core resources

```bash
gcloud services enable run.googleapis.com cloudbuild.googleapis.com artifactregistry.googleapis.com sqladmin.googleapis.com secretmanager.googleapis.com iamcredentials.googleapis.com
```

1. **Artifact Registry (Docker repo)**  
   ```bash
   gcloud artifacts repositories create recipeapp \
     --repository-format=docker \
     --location=us-central1 \
     --description="RecipeApp containers"
   ```

2. **Cloud SQL (PostgreSQL)**  
   ```bash
   gcloud sql instances create recipeapp-db \
     --database-version=POSTGRES_15 \
     --cpu=2 --memory=4GiB \
     --region=us-central1 \
     --tier=db-custom-2-8192 \
     --storage-auto-increase
   gcloud sql databases create recipeapp --instance=recipeapp-db
   gcloud sql users create recipeapp --instance=recipeapp-db --password="CHANGE_ME"
   ```
   *Record the connection name (`PROJECT:REGION:recipeapp-db`).*

3. **Secret Manager values**
   ```bash
   echo -n 'Host=/cloudsql/PROJECT:REGION:recipeapp-db;Database=recipeapp;Username=recipeapp;Password=CHANGE_ME' | \
     gcloud secrets create recipeapp-connection-string --data-file=-
   echo -n 'sk-...' | gcloud secrets create openai-api-key --data-file=-
   ```
   (Use `gcloud secrets versions add` to update existing secrets later.)

4. **Service account for Cloud Run/Build**
   ```bash
   gcloud iam service-accounts create recipeapp-deployer \
     --display-name="RecipeApp Cloud Run deployer"

   gcloud projects add-iam-policy-binding $PROJECT \
     --member="serviceAccount:recipeapp-deployer@$PROJECT.iam.gserviceaccount.com" \
     --role="roles/run.admin"

   gcloud projects add-iam-policy-binding $PROJECT \
     --member="serviceAccount:recipeapp-deployer@$PROJECT.iam.gserviceaccount.com" \
     --role="roles/cloudsql.client"

   gcloud secrets add-iam-policy-binding recipeapp-connection-string \
     --member="serviceAccount:recipeapp-deployer@$PROJECT.iam.gserviceaccount.com" \
     --role="roles/secretmanager.secretAccessor"

   gcloud secrets add-iam-policy-binding openai-api-key \
     --member="serviceAccount:recipeapp-deployer@$PROJECT.iam.gserviceaccount.com" \
     --role="roles/secretmanager.secretAccessor"
   ```

---

## 3. Verify Dockerfile + configuration

The repository already contains a multi-stage `Dockerfile` that:
1. Builds the React UI (`recipeapp-ui`).
2. Publishes the ASP.NET backend.
3. Serves the compiled UI from `wwwroot`.

Ensure production settings rely on environment variables (default in ASP.NET). Cloud Run will inject:

| Setting | Source |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Secret `recipeapp-connection-string` |
| `OpenAI__ApiKey` | Secret `openai-api-key` |
| `ASPNETCORE_ENVIRONMENT` | Literal `Production` |

If you need extra config (SMTP, telemetry, etc.) add more secrets and `--set-secrets` flags in `cloudbuild.yaml`.

---

## 4. Configure Cloud Build

The root `cloudbuild.yaml` defines the pipeline:
1. Build the Docker image.
2. Push to Artifact Registry.
3. Deploy to Cloud Run (`recipeapp-api`) including Cloud SQL attachment + secrets.

Update the substitution defaults at the top of `cloudbuild.yaml` if you use different regions/names.

### Create GitHub trigger

1. In Cloud Console → **Cloud Build → Triggers**, click **Create Trigger**.
2. Choose **Connect Repository** and authorize the GitHub app if prompted.
3. Select your repo/branch (`main` by default).
4. Set **Trigger type** = “Push to a branch”.
5. Set **Build configuration** = `cloudbuild.yaml`.
6. Provide substitution overrides as needed (e.g. `_REGION`, `_SERVICE_ACCOUNT`).
7. Save.

Every push now runs the build + deploy automatically.

### First-time manual run (optional)

You can kick off a build to verify before wiring up GitHub:
```bash
gcloud builds submit --substitutions _REGION=us-central1,_SERVICE_NAME=recipeapp-api
```

---

## 5. Networking & access

1. **Cloud Run URL** appears after deployment (`https://recipeapp-api-xxxx.a.run.app`).  
2. (Optional) Map a custom domain via Cloud Run → “Manage Custom Domains”.
3. For private APIs, restrict ingress and front with Cloud Load Balancer + Identity-Aware Proxy.

---

## 6. Database migrations

Cloud Run instances are stateless, so run EF Core migrations via Cloud Build or manually:
```bash
dotnet ef database update --connection "Host=/cloudsql/PROJECT:REGION:recipeapp-db;Database=recipeapp;Username=recipeapp;Password=..."
```
You can also add a Cloud Build step that runs migrations using the same container image before deployment (ensure it has Cloud SQL access).

---

## 7. Frontend hosting alternatives

The current Dockerfile serves the built React bundle from ASP.NET. If you prefer separate hosting:
1. Build UI (`npm run build`).
2. Upload `/recipeapp-ui/dist` to Cloud Storage or Firebase Hosting.
3. Point the React app to the Cloud Run API base URL (set `VITE_API_BASE_URL` during `npm run build`).

---

## 8. Troubleshooting checklist

- **500 errors on startup** → check Cloud Run logs for missing env vars or migrations.
- **DB connection refused** → ensure Cloud SQL instance name in `_CLOUDSQL_INSTANCE` matches and service account has `cloudsql.client`.
- **LLM failures** → confirm the OpenAI secret exists and the Cloud Run service account can access it.
- **Build failures** → inspect Cloud Build logs; ensure Node/npm versions match (Dockerfile uses Node 20).

---

## 9. Summary of required manual actions

1. Create/choose GCP project & enable services.
2. Provision Artifact Registry, Cloud SQL, and secrets.
3. Configure `cloudbuild.yaml` substitutions to match resource names.
4. Create Cloud Build trigger tied to GitHub `main`.
5. (Optional) Map a custom domain once Cloud Run is live.

With that in place, every push to the selected branch builds the Docker image, pushes it to Artifact Registry, and redeploys the Cloud Run service automatically.
