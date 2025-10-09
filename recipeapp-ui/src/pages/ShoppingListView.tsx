import React, { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import axios from "axios";

interface ShoppingItem {
  ingredient: string;
  amount?: number | null;
  unit?: string | null;
}

interface ShoppingList {
  id: string;
  items: ShoppingItem[];
}

export default function ShoppingListView() {
  const { id } = useParams<{ id: string }>();
  const [shoppingList, setShoppingList] = useState<ShoppingList | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchList = async () => {
      try {
        const res = await axios.get(`http://localhost:5114/api/shoppinglist/${id}`);
        setShoppingList(res.data);
      } catch (err: any) {
        console.error(err);
        setError(err.message || "Failed to load shopping list");
      } finally {
        setLoading(false);
      }
    };

    if (id) fetchList();
  }, [id]);

  const handleDownloadPdf = async () => {
    try {
      const res = await axios.get(`http://localhost:5114/api/shoppinglist/${id}/pdf`, {
        responseType: "blob",
      });

      const blob = new Blob([res.data], { type: "application/pdf" });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `ShoppingList_${id}.pdf`;
      a.click();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      console.error("PDF download failed", err);
      alert("Failed to download PDF");
    }
  };

  if (loading)
    return <div className="text-center text-gray-600 py-10">Loading shopping list...</div>;

  if (error)
    return <div className="text-center text-red-600 py-10">❌ {error}</div>;

  if (!shoppingList)
    return <div className="text-center text-gray-600 py-10">No shopping list found.</div>;

  return (
    <div className="max-w-5xl mx-auto py-10 px-6">
      <h1 className="text-3xl font-bold text-blue-700 mb-8 text-center">🛒 Shopping List</h1>

      <div className="bg-white border rounded-xl shadow-lg overflow-hidden">
        <table className="w-full table-auto">
          <thead className="bg-blue-600 text-white">
            <tr>
              <th className="py-3 px-4 text-left">Ingredient</th>
              <th className="py-3 px-4 text-right">Amount</th>
              <th className="py-3 px-4 text-left">Unit</th>
            </tr>
          </thead>
          <tbody>
            {shoppingList.items.map((item, i) => (
              <tr key={i} className={i % 2 === 0 ? "bg-gray-50" : "bg-white"}>
                <td className="py-2 px-4">{item.ingredient}</td>
                <td className="py-2 px-4 text-right">{item.amount || ""}</td>
                <td className="py-2 px-4">{item.unit || ""}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="text-center mt-8">
        <button
          onClick={handleDownloadPdf}
          className="bg-green-600 hover:bg-green-700 text-white font-semibold py-3 px-6 rounded-lg shadow-md transition-all"
        >
          📄 Download PDF
        </button>
      </div>
    </div>
  );
}
