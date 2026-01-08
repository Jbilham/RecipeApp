import { useEffect, useState } from "react";
import axios from "axios";
import useAuth from "../hooks/useAuth";

interface RecipeRow {
  id: string;
  title: string;
  calories?: number;
  protein?: number;
  carbs?: number;
  fat?: number;
  servings?: number;
  isGlobal: boolean;
  imageUrl?: string;
}

export default function Recipes() {
  const { user } = useAuth();
  const [recipes, setRecipes] = useState<RecipeRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [minCalories, setMinCalories] = useState("");
  const [maxCalories, setMaxCalories] = useState("");
  const [minProtein, setMinProtein] = useState("");
  const [maxProtein, setMaxProtein] = useState("");
  const [minCarbs, setMinCarbs] = useState("");
  const [maxCarbs, setMaxCarbs] = useState("");
  const [minFat, setMinFat] = useState("");
  const [maxFat, setMaxFat] = useState("");
  const [mainIngredient, setMainIngredient] = useState("");

  const fetchRecipes = async () => {
    setLoading(true);
    setError(null);
    try {
      const params: Record<string, string> = {};
      if (minCalories) params.minCalories = minCalories;
      if (maxCalories) params.maxCalories = maxCalories;
      if (minProtein) params.minProtein = minProtein;
      if (maxProtein) params.maxProtein = maxProtein;
      if (minCarbs) params.minCarbs = minCarbs;
      if (maxCarbs) params.maxCarbs = maxCarbs;
      if (minFat) params.minFat = minFat;
      if (maxFat) params.maxFat = maxFat;
      if (mainIngredient) params.mainIngredient = mainIngredient;

      const res = await axios.get<RecipeRow[]>("/api/recipes/filter", { params });
      setRecipes(res.data);
    } catch (err: any) {
      console.error(err);
      setError(err.response?.data ?? err.message ?? "Failed to load recipes.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRecipes();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleDelete = async (id: string) => {
    if (!window.confirm("Delete this recipe?")) return;
    try {
      await axios.delete(`/api/recipes/${id}`);
      setRecipes((prev) => prev.filter((r) => r.id !== id));
    } catch (err: any) {
      console.error(err);
      setError(err.response?.data ?? err.message ?? "Failed to delete recipe.");
    }
  };

  const canView = user && user.role !== "Client";

  if (!canView) {
    return (
      <div className="flex min-h-screen items-center justify-center text-red-600">
        You do not have permission to view recipes.
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto px-6 py-10 space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-3xl font-bold text-blue-700">Recipes</h1>
          <p className="text-sm text-gray-600">Filter by macros to find suitable meals.</p>
        </div>
        <button
          type="button"
          onClick={fetchRecipes}
          className="inline-flex items-center rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700"
        >
          Apply filters
        </button>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 bg-white p-4 rounded-lg shadow border border-gray-200">
        <div>
          <label className="text-xs uppercase text-gray-600">Min kcal</label>
          <input
            value={minCalories}
            onChange={(e) => setMinCalories(e.target.value)}
            className="mt-1 w-full rounded border px-2 py-1 text-sm"
            placeholder="e.g. 300"
          />
        </div>
        <div>
          <label className="text-xs uppercase text-gray-600">Max kcal</label>
          <input
            value={maxCalories}
            onChange={(e) => setMaxCalories(e.target.value)}
            className="mt-1 w-full rounded border px-2 py-1 text-sm"
            placeholder="e.g. 700"
          />
        </div>
        <div>
          <label className="text-xs uppercase text-gray-600">Min protein (g)</label>
          <input
            value={minProtein}
            onChange={(e) => setMinProtein(e.target.value)}
            className="mt-1 w-full rounded border px-2 py-1 text-sm"
            placeholder="e.g. 20"
          />
        </div>
        <div>
          <label className="text-xs uppercase text-gray-600">Max protein (g)</label>
          <input
            value={maxProtein}
            onChange={(e) => setMaxProtein(e.target.value)}
            className="mt-1 w-full rounded border px-2 py-1 text-sm"
            placeholder="e.g. 60"
          />
        </div>
        <div>
          <label className="text-xs uppercase text-gray-600">Min carbs (g)</label>
          <input
            value={minCarbs}
            onChange={(e) => setMinCarbs(e.target.value)}
            className="mt-1 w-full rounded border px-2 py-1 text-sm"
            placeholder="e.g. 20"
          />
        </div>
        <div>
          <label className="text-xs uppercase text-gray-600">Max carbs (g)</label>
          <input
            value={maxCarbs}
            onChange={(e) => setMaxCarbs(e.target.value)}
            className="mt-1 w-full rounded border px-2 py-1 text-sm"
            placeholder="e.g. 80"
          />
        </div>
        <div>
          <label className="text-xs uppercase text-gray-600">Min fat (g)</label>
          <input
            value={minFat}
            onChange={(e) => setMinFat(e.target.value)}
            className="mt-1 w-full rounded border px-2 py-1 text-sm"
            placeholder="e.g. 5"
          />
        </div>
        <div>
          <label className="text-xs uppercase text-gray-600">Max fat (g)</label>
          <input
            value={maxFat}
            onChange={(e) => setMaxFat(e.target.value)}
            className="mt-1 w-full rounded border px-2 py-1 text-sm"
            placeholder="e.g. 30"
          />
        </div>
        <div className="md:col-span-4">
          <label className="text-xs uppercase text-gray-600">Main ingredient (e.g. chicken, beef, vegetarian)</label>
          <input
            value={mainIngredient}
            onChange={(e) => setMainIngredient(e.target.value)}
            className="mt-1 w-full rounded border px-2 py-1 text-sm"
            placeholder="e.g. chicken"
          />
        </div>
      </div>

      {loading && <p className="text-gray-600">Loading recipes…</p>}
      {error && <p className="text-red-600">❌ {error}</p>}

      {!loading && !error && recipes.length === 0 && (
        <p className="text-gray-600">No recipes match these filters.</p>
      )}

      <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
        {recipes.map((r) => (
          <div key={r.id} className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <h2 className="text-lg font-semibold text-gray-800">{r.title}</h2>
                    <p className="text-xs uppercase text-gray-500">
                      {r.isGlobal ? "Global" : "Private"}
                      {r.servings ? ` · Servings: ${r.servings}` : ""}
                    </p>
                    {r.imageUrl && (
                      <a
                        href={r.imageUrl}
                        className="text-xs text-blue-600 underline"
                        target="_blank"
                        rel="noreferrer"
                      >
                        View image
                      </a>
                    )}
                  </div>
                  <div className="text-right text-sm text-gray-700">
                    <div className="font-semibold">{r.calories ? `${Math.round(r.calories)} kcal` : "—"}</div>
                    <div className="flex gap-2 justify-end text-xs">
                      <span className="rounded bg-blue-50 px-2 py-0.5">P: {r.protein ?? "—"}g</span>
                  <span className="rounded bg-yellow-50 px-2 py-0.5">C: {r.carbs ?? "—"}g</span>
                  <span className="rounded bg-amber-50 px-2 py-0.5">F: {r.fat ?? "—"}g</span>
                </div>
              </div>
            </div>
            <div className="mt-3 flex justify-end">
              <button
                type="button"
                onClick={() => handleDelete(r.id)}
                className="rounded-md border border-red-200 px-3 py-1 text-xs font-semibold text-red-700 hover:bg-red-50"
              >
                Delete
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
