import { BrowserRouter as Router, Routes, Route } from "react-router-dom";
import Navbar from "./components/Navbar";
import Home from "./pages/Home";
import UploadRecipe from "./pages/UploadRecipe";
import UploadMealPlan from "./pages/UploadMealPlan";
import ShoppingLists from "./pages/ShoppingLists";
import ShoppingListView from "./pages/ShoppingListView";
import TrainingPeaksImport from "./pages/TrainingPeaksImport"; // ✅ add this
import MealPlans from "./pages/MealPlans";
import MealPlanView from "./pages/MealPlanView";
import Login from "./pages/Login";
import UserManagement from "./pages/UserManagement";

export default function App() {
  return (
    <Router>
      <Navbar />
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/upload-recipe" element={<UploadRecipe />} />
        <Route path="/upload-mealplan" element={<UploadMealPlan />} />
        <Route path="/shopping-lists" element={<ShoppingLists />} />
        <Route path="/shopping-list/:id" element={<ShoppingListView />} />
        <Route path="/meal-plans" element={<MealPlans />} />
        <Route path="/meal-plan/:id" element={<MealPlanView />} />
        <Route path="/import" element={<TrainingPeaksImport />} /> {/* ✅ New page */}
        <Route path="/login" element={<Login />} />
        <Route path="/users" element={<UserManagement />} />
      </Routes>
    </Router>
  );
}
