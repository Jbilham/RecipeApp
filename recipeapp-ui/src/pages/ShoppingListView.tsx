import React, { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import axios from "axios";

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

interface ShoppingListItem {
  ingredient: string;
  amount?: number | null;
  unit?: string | null;
  category?: string | null;
}

interface ShoppingListResponseDto {
  items: ShoppingListItem[];
}

interface ShoppingListPlanSummary {
  id: string;
  name: string;
  date?: string;
}

interface ShoppingListDetail {
  id: string;
  title: string;
  range?: string;
  weekStart?: string;
  weekEnd?: string;
  createdAt?: string;
  shoppingList?: ShoppingListResponseDto;
  items?: ShoppingListItem[];
  plans?: ShoppingListPlanSummary[];
  mealPlanSnapshotId?: string;
}

export default function ShoppingListView() {
  const { id } = useParams<{ id: string }>();
  const [detail, setDetail] = useState<ShoppingListDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchList = async () => {
      try {
        const res = await axios.get<ShoppingListDetail>(
          `http://localhost:5114/api/shoppinglists/${id}`
        );
        setDetail(res.data);
      } catch (err: any) {
        console.error(err);
        setError(err.message || "Failed to load shopping list");
      } finally {
        setLoading(false);
      }
    };

    if (id) fetchList();
  }, [id]);

  const items = detail?.shoppingList?.items ?? detail?.items ?? [];

  const groupedItems = useMemo(() => {
    const groups = new Map<string, ShoppingListItem[]>();

    for (const item of items) {
      const category = item.category || "Other";
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
      .map(([category, grouped]) => ({
        category,
        items: grouped.sort((a, b) =>
          a.ingredient.localeCompare(b.ingredient, undefined, {
            sensitivity: "base",
          })
        ),
      }))
      .sort((a, b) => rank(a.category) - rank(b.category));
  }, [items]);

  const formatQuantity = (item: ShoppingListItem) => {
    const { amount, unit } = item;
    if (amount === null || amount === undefined) {
      return unit ? unit : "";
    }

    const numeric = typeof amount === "number" ? amount : parseFloat(String(amount));
    if (Number.isNaN(numeric)) {
      return unit ? `${amount} ${unit}`.trim() : String(amount);
    }

    const formatted = Number.isInteger(numeric)
      ? numeric.toString()
      : numeric.toFixed(2).replace(/\.?0+$/, "");

    return unit ? `${formatted} ${unit}` : formatted;
  };

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

  if (loading) {
    return <div className="text-center text-gray-600 py-10">Loading shopping list…</div>;
  }

  if (error) {
    return <div className="text-center text-red-600 py-10">❌ {error}</div>;
  }

  if (!detail) {
    return <div className="text-center text-gray-600 py-10">No shopping list found.</div>;
  }

  const weekLabel = formatDate(detail.weekStart);
  const createdLabel = formatDateTime(detail.createdAt);

  return (
    <div className="max-w-5xl mx-auto py-10 px-6">
      <div className="mb-6 flex items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-blue-700 mb-2">{detail.title}</h1>
          {weekLabel && (
            <p className="text-sm text-gray-600">
              Week commencing {weekLabel}
              {detail.range ? ` (${detail.range})` : ""}
            </p>
          )}
          {createdLabel && (
            <p className="text-sm text-gray-500">Created {createdLabel}</p>
          )}
        </div>
        <Link
          to="/shopping-lists"
          className="inline-flex items-center rounded-md bg-gray-200 px-3 py-2 text-sm font-semibold text-gray-700 transition hover:bg-gray-300"
        >
          ← Back to lists
        </Link>
      </div>

      {detail.mealPlanSnapshotId && (
        <div className="mb-6">
          <Link
            to={`/meal-plan/${detail.mealPlanSnapshotId}`}
            className="inline-flex items-center rounded-md bg-purple-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-purple-700"
          >
            View linked meal plan →
          </Link>
        </div>
      )}

      {detail.plans && detail.plans.length > 0 && (
        <div className="mb-8 rounded border border-blue-100 bg-blue-50 p-4">
          <h2 className="text-lg font-semibold text-blue-800">Meal plans included</h2>
          <ul className="mt-2 list-disc pl-5 text-sm text-blue-900">
            {detail.plans.map((plan) => {
              const planDate = plan.date ? formatDate(plan.date) : null;
              return (
                <li key={plan.id}>
                  {plan.name}
                  {planDate ? ` — ${planDate}` : ""}
                </li>
              );
            })}
          </ul>
        </div>
      )}

      <div className="bg-white border rounded-xl shadow overflow-hidden">
        {groupedItems.length === 0 ? (
          <p className="p-6 text-gray-500">This shopping list is empty.</p>
        ) : (
          groupedItems.map((group) => (
            <div key={group.category} className="border-b last:border-b-0">
              <div className="bg-gray-100 px-6 py-3 text-sm font-semibold text-gray-700">
                {group.category}
              </div>
              <table className="w-full table-fixed">
                <tbody>
                  {group.items.map((item, idx) => (
                    <tr
                      key={`${group.category}-${item.ingredient}-${idx}`}
                      className={idx % 2 === 0 ? "bg-white" : "bg-gray-50"}
                    >
                      <td className="w-2/3 px-6 py-3 text-gray-800">{item.ingredient}</td>
                      <td className="w-1/3 px-6 py-3 text-right text-gray-600">
                        {formatQuantity(item) || "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
