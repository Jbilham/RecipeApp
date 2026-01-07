import { BrowserRouter as Router, Routes, Route } from "react-router-dom";
import Navbar from "./components/Navbar";
import ProtectedRoute from "./components/ProtectedRoute";
import Home from "./pages/Home";
import UploadRecipe from "./pages/UploadRecipe";
import UploadMealPlan from "./pages/UploadMealPlan";
import ShoppingLists from "./pages/ShoppingLists";
import ShoppingListView from "./pages/ShoppingListView";
import TrainingPeaksImport from "./pages/TrainingPeaksImport";
import MealPlans from "./pages/MealPlans";
import MealPlanView from "./pages/MealPlanView";
import Login from "./pages/Login";
import UserManagement from "./pages/UserManagement";
import Recipes from "./pages/Recipes";

export default function App() {
  return (
    <Router>
      <Navbar />
      <Routes>
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <Home />
            </ProtectedRoute>
          }
        />
        <Route
          path="/upload-recipe"
          element={
            <ProtectedRoute>
              <UploadRecipe />
            </ProtectedRoute>
          }
        />
        <Route
          path="/upload-mealplan"
          element={
            <ProtectedRoute>
              <UploadMealPlan />
            </ProtectedRoute>
          }
        />
        <Route
          path="/shopping-lists"
          element={
            <ProtectedRoute>
              <ShoppingLists />
            </ProtectedRoute>
          }
        />
        <Route
          path="/shopping-list/:id"
          element={
            <ProtectedRoute>
              <ShoppingListView />
            </ProtectedRoute>
          }
        />
        <Route
          path="/meal-plans"
          element={
            <ProtectedRoute>
              <MealPlans />
            </ProtectedRoute>
          }
        />
        <Route
          path="/meal-plan/:id"
          element={
            <ProtectedRoute>
              <MealPlanView />
            </ProtectedRoute>
          }
        />
        <Route
          path="/import"
          element={
            <ProtectedRoute>
              <TrainingPeaksImport />
            </ProtectedRoute>
          }
        />
        <Route path="/login" element={<Login />} />
        <Route
          path="/users"
          element={
            <ProtectedRoute>
              <UserManagement />
            </ProtectedRoute>
          }
        />
        <Route
          path="/recipes"
          element={
            <ProtectedRoute>
              <Recipes />
            </ProtectedRoute>
          }
        />
      </Routes>
    </Router>
  );
}
