# Deploying RecipeApp to Google Cloud Platform

This guide walks through packaging the application into a container and deploying it to Cloud Run with a managed PostgreSQL database in Cloud SQL. Adjust names/regions to suit your project.

---

## 1. Prerequisites

- Google Cloud project with billing enabled.
- `gcloud` CLI installed and authenticated (`gcloud auth login`).
- Docker installed and configured to use the gcloud credential helper (`gcloud auth configure-docker`).
- A Cloud SQL instance (PostgreSQL) or plan to create one.
- Domain name managed in Cloud Domains or another registrar you can configure.

---

## 2. Build the container locally

```bash
# From the repository root
# 1) Build the image (this also builds the React bundle)
docker build -t recipeapp:local .

# 2) Run the container locally (swap values for your environment)
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=recipeapp;Username=postgres;Password=secret" \
  -e OpenAI__ApiKey="<your-openai-key>" \
  recipeapp:local
```

Browse to http://localhost:8080 to confirm the app serves both API and UI.

---

## 3. Push the image to Artifact Registry

```bash
PROJECT_ID=<your-project-id>
REGION=us-central1
REPO=recipeapp
IMAGE="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO}/recipeapp:$(git rev-parse --short HEAD)"

# Create a repository (one-time)
gcloud artifacts repositories create ${REPO} \
  --repository-format=docker \
  --location=${REGION} \
  --description="RecipeApp containers"

# Tag & push
docker tag recipeapp:local ${IMAGE}
docker push ${IMAGE}
```

---

## 4. Prepare Cloud SQL (PostgreSQL)

1. Create an instance: `gcloud sql instances create recipeapp-db --database-version=POSTGRES_15 --tier=db-custom-1-3840 --region=${REGION}`
2. Create a database and user:
   ```bash
   gcloud sql databases create recipeapp --instance=recipeapp-db
   gcloud sql users create recipeapp --instance=recipeapp-db --password=<password>
   ```
3. Note the instance connection name: `${PROJECT_ID}:${REGION}:recipeapp-db`.

Run migrations once the service is deployed (see section 6) or via `dotnet ef database update` using the Cloud SQL Auth Proxy locally.

---

## 5. Deploy to Cloud Run

```bash
SERVICE=recipeapp-api
INSTANCE="${PROJECT_ID}:${REGION}:recipeapp-db"

# Deploy
gcloud run deploy ${SERVICE} \
  --image ${IMAGE} \
  --platform managed \
  --region ${REGION} \
  --allow-unauthenticated \
  --add-cloudsql-instances ${INSTANCE} \
  --set-env-vars "ASPNETCORE_ENVIRONMENT=Production" \
  --set-env-vars "ConnectionStrings__DefaultConnection=Host=/cloudsql/${INSTANCE};Database=recipeapp;Username=recipeapp;Password=<password>" \
  --set-env-vars "OpenAI__ApiKey=<your-openai-key>" \
  --set-env-vars "Seed__MasterEmail=master@yourdomain.com" \
  --set-env-vars "Seed__MasterPassword=<choose-strong-password>"
```

Cloud Run automatically binds to port 8080. The deployment output includes the default URL (e.g. `https://recipeapp-api-xxxxx.run.app`).

---

## 6. Apply migrations

Two common options:

1. **Cloud Run job:**
   ```bash
   gcloud run jobs create recipeapp-migrate \
     --image ${IMAGE} \
     --region ${REGION} \
     --add-cloudsql-instances ${INSTANCE} \
     --set-env-vars "ConnectionStrings__DefaultConnection=Host=/cloudsql/${INSTANCE};Database=recipeapp;Username=recipeapp;Password=<password>" \
     --command "dotnet" --args "ef","database","update"
   gcloud run jobs execute recipeapp-migrate --region ${REGION}
   ```
2. **Cloud SQL Auth Proxy locally:**
   ```bash
   ./cloud-sql-proxy ${INSTANCE}=tcp:5432
   ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5432;Database=recipeapp;Username=recipeapp;Password=<password>" dotnet ef database update
   ```

Ensure the latest migration `AddMealSelectionFlags` has been applied so new columns exist.

---

## 7. Custom domain & HTTPS

1. Verify the domain in Google Cloud (Cloud Domains → Domain Mappings → Verify). Add the provided TXT record if your registrar is external.
2. Create a Cloud DNS managed zone for your domain and update the registrar nameservers.
3. Map the domain to the Cloud Run service:
   ```bash
   gcloud run domain-mappings create --service ${SERVICE} --domain app.yourdomain.com --region ${REGION}
   ```
   Cloud Run provisions a managed TLS certificate once DNS points correctly.
4. Update DNS A/AAAA records to the values returned by the domain mapping command.

---

## 8. Frontend configuration

The container serves the built React app directly. To point local dev builds at Cloud Run, set:

```bash
# recipeapp-ui/.env.production (example)
VITE_API_URL=https://app.yourdomain.com
```

Re-run `npm run build` if you ever rebuild the bundle outside Docker.

---

## 9. Environment variables summary

| Purpose                    | Environment variable                         |
|----------------------------|----------------------------------------------|
| Database connection        | `ConnectionStrings__DefaultConnection`       |
| OpenAI API key             | `OpenAI__ApiKey`                             |
| Seed demo credentials      | `Seed__MasterEmail`, `Seed__MasterPassword`, etc. |
| ASP.NET Core environment   | `ASPNETCORE_ENVIRONMENT` (set to `Production`)|

Use Secret Manager for sensitive values and reference them in Cloud Run with `--set-secrets` if preferred.

---

## 10. Continuous deployment (optional)

- Configure Cloud Build or GitHub Actions to build the Docker image on each merge to `main`, push to Artifact Registry, and redeploy Cloud Run.
- Use build substitutions for the image tag (`$COMMIT_SHA`).

---

With the Dockerfile committed, the app can be built and run identically in development, CI, and Cloud Run. Let me know when you're ready to script the Cloud Run deployment or set up a pipeline.
