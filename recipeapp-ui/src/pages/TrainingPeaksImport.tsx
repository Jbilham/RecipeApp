import React, { useState } from "react";

export default function TrainingPeaksImport() {
  const [url, setUrl] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [range, setRange] = useState("this");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<any>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setResult(null);

    try {
      const formData = new FormData();
      if (file) formData.append("file", file);

      // Build query string (we’ll always send range, and maybe url)
      const qs = new URLSearchParams();
      qs.append("range", range);
      if (url) qs.append("url", url);

      const response = await fetch(
        `http://localhost:5114/api/CalendarImport/import?${qs.toString()}`,
        {
          method: "POST",
          body: formData,
        }
      );

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `HTTP ${response.status}`);
      }

      const data = await response.json();
      setResult(data);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="max-w-3xl mx-auto mt-10 p-6 bg-white shadow rounded">
      <h1 className="text-2xl font-bold mb-4">Import from TrainingPeaks</h1>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700">
            TrainingPeaks iCal URL
          </label>
          <input
            type="url"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            className="mt-1 w-full border border-gray-300 rounded-md p-2"
            placeholder="https://www.trainingpeaks.com/ical/XXXX.ics"
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700">
            or Upload .ics File
          </label>
          <input
            type="file"
            accept=".ics"
            onChange={(e) => setFile(e.target.files?.[0] || null)}
            className="mt-1 w-full border border-gray-300 rounded-md p-2"
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700">
            Range
          </label>
          <select
            value={range}
            onChange={(e) => setRange(e.target.value)}
            className="mt-1 border border-gray-300 rounded-md p-2"
          >
            <option value="this">This week</option>
            <option value="next">Next week</option>
          </select>
        </div>

        <button
          type="submit"
          disabled={loading}
          className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50"
        >
          {loading ? "Importing..." : "Import Calendar"}
        </button>
      </form>

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
            Range: {result.weekStart} → {result.weekEnd}
          </p>

          <div className="space-y-4">
            {result.plans?.map((plan: any) => (
              <div
                key={plan.id}
                className="border rounded p-4 shadow-sm bg-gray-50"
              >
                <h3 className="font-semibold">
                  {plan.name} ({new Date(plan.date).toLocaleDateString()})
                </h3>
                <ul className="list-disc ml-5 text-sm mt-1">
                  {plan.meals.map((m: any, idx: number) => (
                    <li key={idx}>
                      <strong>{m.mealType}:</strong>{" "}
                      {m.recipeName || m.freeText || "No recipe"}
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>

          {result.shoppingList && (
            <div className="mt-8">
              <h2 className="text-xl font-semibold mb-2">Shopping List</h2>
              <ul className="list-disc ml-5">
                {result.shoppingList.items?.map((i: any, idx: number) => (
                <li key={idx}>
                    {i.amount !== null && i.amount !== undefined ? `${i.amount} ` : ""}
                    {i.unit ? `${i.unit} ` : ""}
                    {i.ingredient}
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
