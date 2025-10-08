import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import Navbar from './components/Navbar';
import Home from './pages/Home';
import UploadRecipe from './pages/UploadRecipe';
import UploadMealPlan from './pages/UploadMealPlan';
import ShoppingLists from './pages/ShoppingLists';
export default function App() {
  return (
    <Router>
      <Navbar />
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/upload-recipe" element={<UploadRecipe />} />
        <Route path="/upload-mealplan" element={<UploadMealPlan />} />
        <Route path="/shopping-lists" element={<ShoppingLists />} />
      </Routes>
    </Router>
  );
}