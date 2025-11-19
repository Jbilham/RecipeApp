import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import axios from "axios";

interface MealPlanSummary {
  id: string;
  title: string;
  weekStart?: string;
  weekEnd?: string;
  createdAt?: string;
  range?: string;
  planCount: number;
  shoppingListSnapshotId?: string;
}

export default function MealPlans() {
  const [plans, setPlans] = useState<MealPlanSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchPlans = async () => {
      try {
        const res = await axios.get<MealPlanSummary[]>("http://localhost:5114/api/mealplans");
        setPlans(res.data);
      } catch (err: any) {
        console.error(err);
        setError(err.message || "Failed to load meal plans.");
      } finally {
        setLoading(false);
      }
    };

    fetchPlans();
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
      <h1 className="text-3xl font-bold text-blue-700 mb-6">Saved Meal Plans</h1>

      {loading && <p className="text-gray-600">Loading meal plans…</p>}
      {error && <p className="text-red-600">❌ {error}</p>}

      {!loading && !error && plans.length === 0 && (
        <p className="text-gray-600">No meal plan snapshots created yet.</p>
      )}

      <div className="space-y-4">
        {plans.map((plan) => {
          const weekLabel = formatDate(plan.weekStart);
          const createdLabel = formatDateTime(plan.createdAt);
          return (
            <div key={plan.id} className="border rounded-lg bg-white shadow-sm p-5">
              <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
                <div>
                  <h2 className="text-xl font-semibold text-gray-800">{plan.title}</h2>
                  {weekLabel && (
                    <p className="text-sm text-gray-600">
                      Week commencing {weekLabel}
                      {plan.range ? ` (${plan.range})` : ""}
                    </p>
                  )}
                  {createdLabel && (
                    <p className="text-sm text-gray-500">Created {createdLabel}</p>
                  )}
                  <p className="text-sm text-gray-500">
                    {plan.planCount} plan{plan.planCount === 1 ? "" : "s"}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  {plan.shoppingListSnapshotId && (
                    <Link
                      to={`/shopping-list/${plan.shoppingListSnapshotId}`}
                      className="inline-flex items-center justify-center rounded-md bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-green-700"
                    >
                      View shopping list →
                    </Link>
                  )}
                  <Link
                    to={`/meal-plan/${plan.id}`}
                    className="inline-flex items-center justify-center rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700"
                  >
                    View meal plan →
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
