import { useState } from 'react';
import axios from 'axios';
export default function UploadMealPlan() {
  const [file, setFile] = useState<File | null>(null);
  const [response, setResponse] = useState<any>(null);
  const handleUpload = async () => {
    if (!file) return alert('Select a file first');
    const formData = new FormData();
    formData.append('file', file);
    const res = await axios.post('http://localhost:5114/api/mealplan/week', formData, { headers: { 'Content-Type': 'multipart/form-data' } });
    setResponse(res.data);
  };
  return (
    <div className="p-10 max-w-3xl mx-auto">
      <h1 className="text-2xl font-bold mb-4">Upload Meal Plan</h1>
      <input type="file" onChange={(e) => setFile(e.target.files?.[0] || null)} className="mb-4" />
      <button onClick={handleUpload} className="bg-yellow-500 text-white px-4 py-2 rounded hover:bg-yellow-600">Upload</button>
      {response && <pre className="mt-6 bg-gray-100 p-4 rounded">{JSON.stringify(response, null, 2)}</pre>}
    </div>
  );
}