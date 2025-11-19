import React, { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import axios from "axios";

interface MealPlanSnapshotMeal {
  mealId?: string;
  mealType: string;
  recipeName?: string | null;
  missingRecipe?: boolean;
  autoHandled?: boolean;
  freeText?: string | null;
  isSelected?: boolean;
}

interface MealPlanSnapshotPlan {
  id: string;
  name: string;
  date?: string;
  meals: MealPlanSnapshotMeal[];
}

interface MealPlanDetail {
  id: string;
  title: string;
  range?: string;
  weekStart?: string;
  weekEnd?: string;
  createdAt?: string;
  plans: MealPlanSnapshotPlan[];
  shoppingListSnapshotId?: string;
}

export default function MealPlanView() {
  const { id } = useParams<{ id: string }>();
  const [detail, setDetail] = useState<MealPlanDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [pageError, setPageError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    const fetchPlan = async () => {
      try {
        const res = await axios.get<MealPlanDetail>(`/api/mealplans/${id}`);
        setDetail(res.data);
        setDirty(false);
        setActionError(null);
        setSuccessMessage(null);
      } catch (err: any) {
        console.error(err);
        setPageError(err.message || "Failed to load meal plan");
      } finally {
        setLoading(false);
      }
    };

    if (id) fetchPlan();
  }, [id]);

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

  const autoHandledCount = useMemo(() => {
    if (!detail) return 0;
    return detail.plans.reduce(
      (acc, plan) =>
        acc +
        plan.meals.reduce((inner, meal) => inner + (meal.autoHandled ? 1 : 0), 0),
      0
    );
  }, [detail]);

  if (loading) {
    return <div className="text-center text-gray-600 py-10">Loading meal plan…</div>;
  }

  if (pageError) {
    return <div className="text-center text-red-600 py-10">❌ {pageError}</div>;
  }

  if (!detail) {
    return <div className="text-center text-gray-600 py-10">No meal plan found.</div>;
  }

  const weekLabel = formatDate(detail.weekStart);
  const createdLabel = formatDateTime(detail.createdAt);

  const handleToggleMeal = (planId: string, mealId: string | undefined, isSelected: boolean) => {
    if (!mealId) return;
    setDetail((current) => {
      if (!current) return current;
      const plans = current.plans.map((plan) => {
        if (plan.id !== planId) return plan;
        return {
          ...plan,
          meals: plan.meals.map((meal) =>
            meal.mealId === mealId ? { ...meal, isSelected } : meal
          ),
        };
      });
      return { ...current, plans };
    });
    setDirty(true);
    setSuccessMessage(null);
    setActionError(null);
  };

  const handleApplySelections = async () => {
    if (!detail || !id) return;
    setSaving(true);
    setActionError(null);
    try {
      const payload = {
        meals: detail.plans.flatMap((plan) =>
          plan.meals
            .filter((meal) => Boolean(meal.mealId))
            .map((meal) => ({
              mealId: meal.mealId!,
              isSelected: meal.isSelected !== false,
            }))
        ),
      };

      const res = await axios.patch<MealPlanDetail>(`/api/mealplans/${id}/selections`, payload);
      setDetail(res.data);
      setDirty(false);
      setSuccessMessage("Selections updated. Shopping list refreshed.");
    } catch (err: any) {
      console.error(err);
      setActionError(err.message || "Failed to update selections");
    } finally {
      setSaving(false);
    }
  };

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
          {autoHandledCount > 0 && (
            <p className="text-xs text-green-600">
              {autoHandledCount} snack{autoHandledCount === 1 ? "" : "s"} auto-classified
            </p>
          )}
        </div>
        <Link
          to="/meal-plans"
          className="inline-flex items-center rounded-md bg-gray-200 px-3 py-2 text-sm font-semibold text-gray-700 transition hover:bg-gray-300"
        >
          ← Back to meal plans
        </Link>
      </div>

      <div className="mb-6 rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={handleApplySelections}
            disabled={!dirty || saving}
            className={`rounded-md px-4 py-2 text-sm font-semibold text-white transition ${
              !dirty || saving
                ? "bg-blue-300 cursor-not-allowed"
                : "bg-blue-600 hover:bg-blue-700"
            }`}
          >
            {saving ? "Updating…" : "Update shopping list"}
          </button>
          {dirty && (
            <span className="text-sm text-gray-600">You have unsaved meal selection changes.</span>
          )}
          {successMessage && (
            <span className="text-sm text-green-600">{successMessage}</span>
          )}
        </div>
        {actionError && (
          <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {actionError}
          </p>
        )}
      </div>

      {detail.shoppingListSnapshotId && (
        <div className="mb-6">
          <Link
            to={`/shopping-list/${detail.shoppingListSnapshotId}`}
            className="inline-flex items-center rounded-md bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-green-700"
          >
            View linked shopping list →
          </Link>
        </div>
      )}

      <div className="space-y-6">
        {detail.plans.map((plan) => {
          const planDate = formatDate(plan.date);
          return (
            <div key={plan.id} className="rounded-lg border border-gray-200 bg-white shadow">
              <div className="border-b border-gray-100 bg-gray-50 px-6 py-3">
                <h2 className="text-lg font-semibold text-gray-800">
                  {plan.name}
                  {planDate ? ` — ${planDate}` : ""}
                </h2>
              </div>
              <div className="divide-y divide-gray-100">
                {plan.meals.map((meal, idx) => (
                  <div key={`${plan.id}-${idx}`} className="px-6 py-4">
                    <div className="flex items-baseline justify-between gap-4">
                      <h3 className="text-base font-semibold text-gray-800">{meal.mealType}</h3>
                      {meal.autoHandled && (
                        <span className="text-xs font-semibold text-green-600">
                          auto-classified snack
                        </span>
                      )}
                    </div>
                    <p className="mt-1 text-sm text-gray-700">
                      {meal.recipeName ? (
                        <>
                          <span className="font-medium">{meal.recipeName}</span>
                          {meal.freeText ? ` — ${meal.freeText}` : ""}
                        </>
                      ) : meal.freeText ? (
                        meal.freeText
                      ) : (
                        <span className="text-red-600">No recipe details</span>
                      )}
                    </p>
                    {meal.missingRecipe && !meal.autoHandled && (
                      <p className="mt-2 text-xs text-yellow-700">
                        No recipe matched for this meal.
                      </p>
                    )}
                    <div className="mt-3">
                      <label className="inline-flex items-center gap-2 text-sm text-gray-700">
                        <input
                          type="checkbox"
                          className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                          checked={meal.isSelected !== false}
                          disabled={!meal.mealId}
                          onChange={(event) =>
                            handleToggleMeal(plan.id, meal.mealId, event.target.checked)
                          }
                        />
                        <span>
                          Include in shopping list
                          {!meal.mealId && " (unavailable)"}
                        </span>
                      </label>
                      {meal.isSelected === false && (
                        <p className="mt-1 text-xs text-gray-500">
                          This meal is excluded from the shopping list.
                        </p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
