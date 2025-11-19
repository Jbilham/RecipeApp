import { Link } from 'react-router-dom';
export default function Navbar() {
  return (
    <nav className="bg-blue-600 text-white px-6 py-4 shadow-md flex justify-between items-center">
      <h1 className="text-xl font-bold">RecipeApp</h1>
      <div className="space-x-4">
        <Link to="/" className="hover:text-yellow-400">Home</Link>
        <Link to="/upload-recipe" className="hover:text-yellow-400">Upload Recipe</Link>
        <Link to="/upload-mealplan" className="hover:text-yellow-400">Meal Plan</Link>
        <Link to="/import" className="hover:text-yellow-400">TrainingPeaks Import</Link>
        <Link to="/shopping-lists" className="hover:text-yellow-400">Shopping Lists</Link>
      </div>
    </nav>
  );
}
