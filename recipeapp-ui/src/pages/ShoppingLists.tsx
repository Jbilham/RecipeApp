import { useEffect, useState } from 'react';
import axios from 'axios';
export default function ShoppingLists() {
  const [lists, setLists] = useState<any[]>([]);
  useEffect(() => { axios.get('http://localhost:5114/api/shoppinglists').then((res) => setLists(res.data)).catch(console.error); }, []);
  return (
    <div className="p-10 max-w-4xl mx-auto">
      <h1 className="text-2xl font-bold mb-6">Shopping Lists</h1>
      <ul className="space-y-4">
        {lists.length > 0 ? lists.map((list) => (
          <li key={list.id} className="p-4 bg-gray-100 rounded shadow">
            <h2 className="font-semibold">{list.name}</h2>
            <pre className="text-sm mt-2">{JSON.stringify(list.items, null, 2)}</pre>
          </li>
        )) : <p>No shopping lists available.</p>}
      </ul>
    </div>
  );
}