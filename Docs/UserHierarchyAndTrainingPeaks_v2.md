# 🧩 User Hierarchy and Access Design Summary (for RecipeApp)

## Context
The **RecipeApp** is evolving into a multi-user platform designed for nutritionists and their clients.  
Each nutritionist can create and share **meal plans, recipes, and shopping lists** for their clients.  
Clients can also contribute their own data (e.g., custom recipes or plans) with limited visibility.

The system currently includes:
- Backend: **.NET 8 / EF Core / PostgreSQL** (running in Docker locally)
- Frontend: **React UI** (served at localhost:5174)
- API controllers including:
  - `MealPlanController`
  - `ShoppingListController`
  - `CalendarImportController`

---

## 👥 User Roles and Hierarchy

### 🧑‍💼 1. Master User (Top-Level / Admin)
- Represents the **nutritionist company owner**.
- Can create, edit, and delete **nutritionist accounts**.
- Has **full visibility** across all users and all data.
- Manages permissions (e.g., enabling/disabling recipe or client access).

### 👩‍⚕️ 2. Nutritionist Users
- Created by the Master User.
- Can:
  - Add and manage **recipes** (global or assigned to specific clients).
  - Create and manage **meal plans** for clients.
  - View and edit **assigned clients**.
  - Optionally approve **client-submitted recipes** or plans.
- Cannot see data belonging to other nutritionists unless shared company-wide.

### 🧍 3. Client Users
- Created by a Nutritionist (or via self-signup, assigned to a nutritionist).
- Can:
  - View **meal plans** assigned to them.
  - Add **personal recipes** (visible only to themselves by default).
  - Create their own **meal plans** (if allowed by nutritionist).
- Cannot view or modify data belonging to others.

---

## 🔗 TrainingPeaks Integration (Per-User ICS Links)

### Overview
Each user (nutritionist or client) can **link their TrainingPeaks account** by saving their personal **ICS calendar URL**.  
This allows RecipeApp to:
- Fetch upcoming **meal plan data** from TrainingPeaks.
- Parse and normalize daily nutrition entries.
- Automatically generate **Meal Plans** and **Shopping Lists** using existing logic.

### Implementation

Add a new field to the user model:

```csharp
public class AppUser : IdentityUser
{
    public string Role { get; set; } // Master, Nutritionist, Client
    public Guid? ParentUserId { get; set; }
    public string? TrainingPeaksIcsUrl { get; set; } // Persist user's ICS link
}
A new controller (UserCalendarController) enables:

Saving or updating the user's TrainingPeaks ICS link.

Triggering imports for “this week” or “next week”.

Automatically generating meal plans and shopping lists.

Endpoint	Method	Description
/api/usercalendar/set-url	POST	Save a user’s TrainingPeaks ICS link
/api/usercalendar/get-url	GET	Retrieve the saved link
/api/usercalendar/import?range=this	POST	Import and generate meal plan for current week
/api/usercalendar/import?range=next	POST	Import and generate meal plan for next week

Behind the Scenes
These endpoints reuse the CalendarImportService, which:

Fetches and parses the ICS file.

Extracts events labeled “Custom: Daily Nutrition Plan”.

Passes the parsed text to the LLM meal plan parser.

Builds Meal Plans and Shopping Lists using existing logic in MealPlanController.

Permissions
Nutritionists can view/import for assigned clients (if authorized).

Clients can only manage their own ICS link and imports.

🔐 Permissions & Visibility Rules
Action	Master	Nutritionist	Client
Create users	✅	❌	❌
Add recipes (global)	✅	✅	❌
Add recipes (private)	✅	✅	✅ (private only)
Create meal plans	✅	✅	✅ (for themselves)
Assign meal plans	✅	✅	❌
View all data	✅	❌	❌
Edit shared data	✅	✅	❌
Manage TrainingPeaks ICS link	✅	✅	✅ (own link only)
Trigger TrainingPeaks import	✅	✅	✅ (own data only)

🧠 Implementation Notes
Use ASP.NET Identity for authentication (extendable to MFA/email verification).

Extend user model as shown above.

Use [Authorize(Roles = "Master,Nutritionist,Client")] on secured endpoints.

Restrict queries to visible data only:

csharp
Copy code
var userId = User.GetUserId();
var visibleRecipes = _db.Recipes
    .Where(r => r.OwnerId == userId || r.IsGlobal)
    .ToList();
Refactor ICS parsing logic into a reusable CalendarImportService.

Future expansion: Garmin, MyFitnessPal, or other integrations.

🔄 Next Steps
Introduce the updated AppUser model with TrainingPeaksIcsUrl.

Create the UserCalendarController for per-user calendar imports.

Refactor CalendarImportController into a shared service.

Implement authentication & user visibility scoping.

Add a frontend UI for managing and triggering TrainingPeaks imports.

✅ Summary
This structure enables:

A clear role hierarchy between company admins, nutritionists, and clients.

Seamless TrainingPeaks calendar integration per user.

Scalable future growth (multi-tenancy, external integrations, client self-management).