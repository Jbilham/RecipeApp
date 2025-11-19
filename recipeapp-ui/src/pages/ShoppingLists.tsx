import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import axios from "axios";

interface ShoppingListSummary {
  id: string;
  title: string;
  weekStart?: string;
  weekEnd?: string;
  createdAt: string;
  range?: string;
  itemCount: number;
  mealPlanSnapshotId?: string;
}

export default function ShoppingLists() {
  const [lists, setLists] = useState<ShoppingListSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchLists = async () => {
      try {
        const res = await axios.get<ShoppingListSummary[]>("http://localhost:5114/api/shoppinglists");
        setLists(res.data);
      } catch (err: any) {
        console.error(err);
        setError(err.message || "Failed to load shopping lists.");
      } finally {
        setLoading(false);
      }
    };

    fetchLists();
  }, []);

  const formatDate = (value?: string) => {
    if (!value) return null;
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return null;
    return date.toLocaleDateString(undefined, {
      weekday: "long",
      day: "numeric",
      month: "short",
      year: "numeric",
    });
  };

  const formatDateTime = (value?: string) => {
    if (!value) return null;
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return null;
    return date.toLocaleString();
  };

  return (
    <div className="max-w-5xl mx-auto p-8">
      <h1 className="text-3xl font-bold text-blue-700 mb-6">Saved Shopping Lists</h1>

      {loading && <p className="text-gray-600">Loading shopping lists…</p>}
      {error && <p className="text-red-600">❌ {error}</p>}

      {!loading && !error && lists.length === 0 && (
        <p className="text-gray-600">No shopping lists created yet.</p>
      )}

      <div className="space-y-4">
        {lists.map((list) => {
          const weekLabel = formatDate(list.weekStart);
          const createdLabel = formatDateTime(list.createdAt);
          return (
            <div key={list.id} className="border rounded-lg bg-white shadow-sm p-5">
              <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
                <div>
                  <h2 className="text-xl font-semibold text-gray-800">{list.title}</h2>
                  {weekLabel && (
                    <p className="text-sm text-gray-600">
                      Week commencing {weekLabel}
                      {list.range ? ` (${list.range})` : ""}
                    </p>
                  )}
                  {createdLabel && (
                    <p className="text-sm text-gray-500">Created {createdLabel}</p>
                  )}
                  <p className="text-sm text-gray-500">
                    {list.itemCount} item{list.itemCount === 1 ? "" : "s"}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  {list.mealPlanSnapshotId && (
                    <Link
                      to={`/meal-plan/${list.mealPlanSnapshotId}`}
                      className="inline-flex items-center justify-center rounded-md bg-purple-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-purple-700"
                    >
                      View meal plan →
                    </Link>
                  )}
                  <Link
                    to={`/shopping-list/${list.id}`}
                    className="inline-flex items-center justify-center rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700"
                  >
                    View list →
                  </Link>
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
