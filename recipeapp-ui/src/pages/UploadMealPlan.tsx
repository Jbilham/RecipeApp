import React, { useState } from "react";
import axios from "axios";
import { useNavigate } from "react-router-dom";

interface Meal {
  mealType: string;
  recipeName?: string;
  freeText?: string;
}

interface MealPlan {
  name: string;
  date: string;
  meals: Meal[];
}

interface ShoppingItem {
  ingredient: string;
  amount?: number | null;
  unit?: string | null;
}

interface ShoppingList {
  items: ShoppingItem[];
}

export default function UploadMealPlan() {
  const [mealPlan, setMealPlan] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<{ plans?: MealPlan[]; shoppingList?: ShoppingList } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setMealPlan((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setResult(null);

    try {
      const payload = {
        name: "Weekly Plan",
        startDate: new Date().toISOString(),
        monday: mealPlan.monday || "",
        tuesday: mealPlan.tuesday || "",
        wednesday: mealPlan.wednesday || "",
        thursday: mealPlan.thursday || "",
        friday: mealPlan.friday || "",
        saturday: mealPlan.saturday || "",
        sunday: mealPlan.sunday || "",
      };

      const res = await axios.post("http://localhost:5114/api/mealplan/week", payload, {
        headers: { "Content-Type": "multipart/form-data" },
      });

      // ✅ Expect response like { shoppingListId: "...", response: {...} }
      if (res.data.shoppingListId) {
        navigate(`/shopping-list/${res.data.shoppingListId}`);
      } else {
        setResult(res.data.response || res.data);
      }
    } catch (err: any) {
      console.error(err);
      setError(err.message || "Something went wrong");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="max-w-6xl mx-auto py-10 px-6">
      <h1 className="text-3xl font-bold mb-8 text-center text-blue-700">
        Weekly Meal Plan Creator
      </h1>

      <form onSubmit={handleSubmit}>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"].map(
            (day) => (
              <div key={day} className="bg-white border rounded-xl shadow p-4">
                <h3 className="font-semibold text-lg mb-2 text-gray-800">{day}</h3>
                <textarea
                  name={day.toLowerCase()}
                  placeholder={`Enter meals for ${day}...`}
                  value={mealPlan[day.toLowerCase()] || ""}
                  onChange={handleChange}
                  className="w-full border rounded-lg p-2 h-28 focus:ring-2 focus:ring-blue-400 focus:outline-none"
                />
              </div>
            )
          )}
        </div>

        <div className="text-center mt-8">
          <button
            type="submit"
            disabled={loading}
            className="bg-blue-600 hover:bg-blue-700 text-white font-semibold py-3 px-8 rounded-lg shadow-md transition-all"
          >
            {loading ? "Generating..." : "Generate Shopping List"}
          </button>
        </div>
      </form>

      {/* 🟢 Optional fallback results if redirect skipped */}
      {result && (
        <div className="mt-10">
          <h2 className="text-2xl font-bold mb-4 text-green-700">
            Generated Meal Plan
          </h2>

          {result.plans?.map((plan, idx) => (
            <div key={idx} className="bg-gray-50 border rounded-lg p-4 mb-6">
              <h3 className="text-xl font-semibold text-gray-800 mb-2">
                {plan.name} ({new Date(plan.date).toLocaleDateString()})
              </h3>
              <ul className="list-disc list-inside space-y-1 text-gray-700">
                {plan.meals.map((meal, i) => (
                  <li key={i}>
                    <span className="font-medium">{meal.mealType}:</span>{" "}
                    {meal.recipeName || meal.freeText || "N/A"}
                  </li>
                ))}
              </ul>
            </div>
          ))}

          {result.shoppingList && (
            <div className="bg-white border rounded-lg shadow p-4">
              <h3 className="text-xl font-bold mb-3 text-gray-800">
                Shopping List
              </h3>
              <ul className="divide-y divide-gray-200">
                {result.shoppingList.items.map((item, i) => (
                  <li key={i} className="py-2 flex justify-between">
                    <span>{item.ingredient}</span>
                    <span className="text-gray-500">
                      {item.amount || ""} {item.unit || ""}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {error && (
        <div className="mt-6 bg-red-100 border border-red-400 text-red-700 p-4 rounded">
          ❌ {error}
        </div>
      )}
    </div>
  );
}
