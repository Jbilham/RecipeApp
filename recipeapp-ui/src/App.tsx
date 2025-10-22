import { BrowserRouter as Router, Routes, Route } from "react-router-dom";
import Navbar from "./components/Navbar";
import Home from "./pages/Home";
import UploadRecipe from "./pages/UploadRecipe";
import UploadMealPlan from "./pages/UploadMealPlan";
import ShoppingLists from "./pages/ShoppingLists";
import ShoppingListView from "./pages/ShoppingListView";
import TrainingPeaksImport from "./pages/TrainingPeaksImport"; // ✅ add this

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
        <Route path="/import" element={<TrainingPeaksImport />} /> {/* ✅ New page */}
      </Routes>
    </Router>
  );
}
