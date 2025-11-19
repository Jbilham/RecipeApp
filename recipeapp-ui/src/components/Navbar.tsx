import { Link, useNavigate } from "react-router-dom";
import useAuth from "../hooks/useAuth";

export default function Navbar() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const handleLogout = async () => {
    await logout();
    navigate("/login");
  };

  const canManageUsers = user && user.role !== "Client";

  return (
    <nav className="flex items-center justify-between bg-blue-600 px-6 py-4 text-white shadow-md">
      <div className="flex items-center gap-2">
        <h1 className="text-xl font-bold">RecipeApp</h1>
        {user && (
          <span className="rounded-full bg-white/20 px-3 py-0.5 text-xs uppercase tracking-wide">
            {user.role}
          </span>
        )}
      </div>
      <div className="flex items-center gap-4">
        <Link to="/" className="hover:text-yellow-300">
          Home
        </Link>
        <Link to="/upload-recipe" className="hover:text-yellow-300">
          Upload Recipe
        </Link>
        <Link to="/upload-mealplan" className="hover:text-yellow-300">
          Meal Plan
        </Link>
        <Link to="/import" className="hover:text-yellow-300">
          TrainingPeaks Import
        </Link>
        <Link to="/meal-plans" className="hover:text-yellow-300">
          Meal Plans
        </Link>
        <Link to="/shopping-lists" className="hover:text-yellow-300">
          Shopping Lists
        </Link>
        {canManageUsers && (
          <Link to="/users" className="font-semibold hover:text-yellow-300">
            User Management
          </Link>
        )}
        {user ? (
          <button
            type="button"
            onClick={handleLogout}
            className="rounded-full border border-white/40 px-4 py-1 text-sm font-semibold text-white transition hover:bg-white/20"
          >
            Log out
          </button>
        ) : (
          <Link
            to="/login"
            className="rounded-full border border-white/40 px-4 py-1 text-sm font-semibold text-white transition hover:bg-white/20"
          >
            Log in
          </Link>
        )}
      </div>
    </nav>
  );
}
