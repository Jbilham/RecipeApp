import React, { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { API_BASE_URL } from "../config";

const CATEGORY_DISPLAY_ORDER = [
  "Produce",
  "Protein",
  "Dairy & Eggs",
  "Bakery & Grains",
  "Pantry",
  "Snacks & Supplements",
  "Beverages",
  "Condiments & Sauces",
  "Other",
];

export default function TrainingPeaksImport() {
  const [icsUrl, setIcsUrl] = useState("");
  const [overrideUrl, setOverrideUrl] = useState("");
  const [loadingUrl, setLoadingUrl] = useState(true);
  const [saving, setSaving] = useState(false);
  const [importingRange, setImportingRange] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<any>(null);

  useEffect(() => {
    const fetchUrl = async () => {
      try {
        const res = await fetch(`${API_BASE_URL}/api/usercalendar/get-url`, {
          credentials: "include",
        });
        if (!res.ok) {
          throw new Error(await res.text());
        }
        const data = await res.json();
        setIcsUrl(data.url || "");
      } catch (err: any) {
        setError(err.message || "Failed to load TrainingPeaks URL");
      } finally {
        setLoadingUrl(false);
      }
    };

    fetchUrl();
  }, []);

  const handleSaveUrl = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE_URL}/api/usercalendar/set-url`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ url: icsUrl }),
      });

      if (!res.ok) {
        throw new Error(await res.text());
      }
    } catch (err: any) {
      setError(err.message || "Failed to save TrainingPeaks URL");
    } finally {
      setSaving(false);
    }
  };

  const handleImport = async (range: string) => {
    setImportingRange(range);
    setError(null);
    setResult(null);
    try {
      const payload =
        overrideUrl && overrideUrl.trim().length > 0
          ? JSON.stringify({ urlOverride: overrideUrl })
          : "{}";

      const res = await fetch(`${API_BASE_URL}/api/usercalendar/import?range=${range}`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          credentials: "include",
          body: payload,
        }
      );

      if (!res.ok) {
        throw new Error(await res.text());
      }

      const data = await res.json();
      setResult(data);
    } catch (err: any) {
      setError(err.message || "Failed to import from TrainingPeaks");
    } finally {
      setImportingRange(null);
    }
  };

  const groupedShopping = useMemo(() => {
    if (!result?.shoppingList?.items) return [];
    const groups = new Map<string, any[]>();

    for (const item of result.shoppingList.items) {
      const category =
        (item.category && typeof item.category === "string"
          ? item.category
          : "Other") || "Other";
      if (!groups.has(category)) {
        groups.set(category, []);
      }
      groups.get(category)!.push(item);
    }

    const rank = (category: string) => {
      const idx = CATEGORY_DISPLAY_ORDER.indexOf(category);
      return idx >= 0 ? idx : CATEGORY_DISPLAY_ORDER.length;
    };

    return Array.from(groups.entries())
      .map(([category, items]) => ({
        category,
        items: items.sort((a, b) =>
          a.ingredient.localeCompare(b.ingredient, undefined, {
            sensitivity: "base",
          })
        ),
      }))
      .sort((a, b) => rank(a.category) - rank(b.category));
  }, [result]);

  const formatQuantity = (item: any) => {
    const { amount, unit } = item;
    if (amount === null || amount === undefined) {
      return unit ? unit : "";
    }

    const numeric =
      typeof amount === "number" ? amount : parseFloat(String(amount));
    if (Number.isNaN(numeric)) {
      return unit ? `${amount} ${unit}`.trim() : String(amount);
    }

    const formatted = Number.isInteger(numeric)
      ? numeric.toString()
      : numeric.toFixed(2).replace(/\.0+$/, "");

    return unit ? `${formatted} ${unit}` : formatted;
  };

  return (
    <div className="max-w-3xl mx-auto mt-10 p-6 bg-white shadow rounded">
      <h1 className="text-2xl font-bold mb-6">TrainingPeaks Integration</h1>

      <section className="mb-8">
        <h2 className="text-lg font-semibold mb-2">Linked Calendar</h2>
        <form onSubmit={handleSaveUrl} className="space-y-3">
          <div>
            <label className="block text-sm font-medium text-gray-700">
              TrainingPeaks ICS URL
            </label>
            <input
              type="url"
              value={icsUrl}
              onChange={(e) => setIcsUrl(e.target.value)}
              className="mt-1 w-full border border-gray-300 rounded-md p-2"
              placeholder="https://www.trainingpeaks.com/ical/XXXX.ics"
              disabled={loadingUrl || saving}
            />
          </div>
          <button
            type="submit"
            disabled={saving}
            className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50"
          >
            {saving ? "Saving..." : "Save Link"}
          </button>
        </form>
      </section>

  <section className="mb-8">
        <h2 className="text-lg font-semibold mb-2">Manual Override (optional)</h2>
        <p className="text-sm text-gray-600 mb-2">
          Provide a one-off TrainingPeaks URL to import without affecting your saved link.
        </p>
        <input
          type="url"
          value={overrideUrl}
          onChange={(e) => setOverrideUrl(e.target.value)}
          className="mt-1 w-full border border-gray-300 rounded-md p-2"
          placeholder="https://www.trainingpeaks.com/ical/override.ics"
        />
      </section>

      <section className="mb-8">
        <h2 className="text-lg font-semibold mb-3">Import</h2>
        <div className="flex gap-3">
          <button
            onClick={() => handleImport("this")}
            disabled={importingRange !== null}
            className="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700 disabled:opacity-50"
          >
            {importingRange === "this" ? "Importing..." : "Import This Week"}
          </button>
          <button
            onClick={() => handleImport("next")}
            disabled={importingRange !== null}
            className="bg-indigo-600 text-white px-4 py-2 rounded hover:bg-indigo-700 disabled:opacity-50"
          >
            {importingRange === "next" ? "Importing..." : "Import Next Week"}
          </button>
        </div>
      </section>

      {error && (
        <div className="mt-4 text-red-600 border border-red-300 bg-red-50 p-3 rounded">
          {error}
        </div>
      )}

      {result && (
        <div className="mt-8">
          <h2 className="text-xl font-semibold mb-2">
            Imported {result.totalPlans} Meal Plans
          </h2>
          <p className="text-sm text-gray-600 mb-4">
            Range: {new Date(result.weekStart).toLocaleDateString()} →{" "}
            {new Date(result.weekEnd).toLocaleDateString()}
          </p>

          <div className="space-y-4">
            {result.plans?.map((plan: any) => (
              <div
                key={plan.id}
                className="border rounded p-4 shadow-sm bg-gray-50"
              >
                <h3 className="font-semibold">
                  {plan.name} (
                  {plan.date ? new Date(plan.date).toLocaleDateString() : ""})
                </h3>
                <ul className="list-disc ml-5 text-sm mt-1">
                  {plan.meals.map((m: any, idx: number) => (
                    <li key={idx}>
                      <strong>{m.mealType}:</strong>{" "}
                      {m.recipeName || m.freeText || "No recipe"}
                      {m.missingRecipe && (
                        <span className="ml-2 text-xs font-semibold text-red-600">
                          (no recipe matched)
                        </span>
                      )}
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>

          {result.shoppingListId && (
            <div className="mt-4 flex gap-3">
              <Link
                to={`/shopping-list/${result.shoppingListId}`}
                className="inline-flex items-center rounded-md bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-green-700"
              >
                View saved shopping list →
              </Link>
              {result.mealPlanSnapshotId && (
                <Link
                  to={`/meal-plan/${result.mealPlanSnapshotId}`}
                  className="inline-flex items-center rounded-md bg-purple-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-purple-700"
                >
                  View saved meal plan →
                </Link>
              )}
            </div>
          )}

          {result.shoppingList && (
            <div className="mt-8">
              <h2 className="text-xl font-semibold mb-4">Shopping List</h2>
              {groupedShopping.length === 0 ? (
                <p className="text-sm text-gray-500">
                  No shopping list items were generated.
                </p>
              ) : (
                groupedShopping.map((group) => (
                  <div key={group.category} className="mb-6">
                    <h3 className="text-lg font-semibold text-gray-800 mb-2">
                      {group.category}
                    </h3>
                    <div className="overflow-x-auto rounded border border-gray-200 shadow-sm">
                      <table className="min-w-full divide-y divide-gray-200 text-sm">
                        <thead className="bg-gray-100">
                          <tr>
                            <th className="px-4 py-2 text-left font-semibold text-gray-700">
                              Ingredient
                            </th>
                            <th className="px-4 py-2 text-right font-semibold text-gray-700">
                              Quantity
                            </th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100">
                          {group.items.map((item: any, idx: number) => (
                            <tr key={`${group.category}-${item.ingredient}-${idx}`}>
                              <td className="px-4 py-2 text-gray-800">
                                {item.ingredient}
                              </td>
                              <td className="px-4 py-2 text-right text-gray-600">
                                {formatQuantity(item) || "—"}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}

          {result.missingMeals && result.missingMeals.length > 0 && (
            <div className="mt-6 border border-yellow-300 bg-yellow-50 p-4 rounded">
              <h3 className="text-lg font-semibold text-yellow-800">
                Meals without a matched recipe
              </h3>
              <ul className="list-disc ml-5 mt-2 text-sm text-yellow-900">
                {result.missingMeals.map((missing: any, idx: number) => (
                  <li key={idx}>
                    <strong>{missing.mealPlanName}</strong> — {missing.mealType}:{" "}
                    {missing.name}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
