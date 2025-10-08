import { useState } from 'react';
import axios from 'axios';

export default function UploadRecipe() {
  const [files, setFiles] = useState<FileList | null>(null);
  const [response, setResponse] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const handleUpload = async () => {
    if (!files || files.length === 0) {
      setError("Please select at least one recipe image to upload.");
      return;
    }

    setLoading(true);
    setError(null);
    setSuccess(false);

    try {
      const formData = new FormData();
      Array.from(files).forEach((file) => formData.append('files', file));

      const res = await axios.post(
        'http://localhost:5114/api/ingestion/upload/batch',
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } }
      );

      setResponse(res.data);
      setSuccess(true);
    } catch (err: any) {
      setError(err.message || "Upload failed. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="p-10 max-w-3xl mx-auto">
      <h1 className="text-3xl font-bold mb-6 text-blue-700">Upload Recipes</h1>

      <input
        type="file"
        multiple
        onChange={(e) => setFiles(e.target.files)}
        className="mb-4 block w-full text-gray-700"
      />

      <button
        onClick={handleUpload}
        disabled={loading}
        className={`px-5 py-2 rounded text-white font-semibold transition-colors 
          ${loading ? 'bg-gray-400 cursor-not-allowed' : 'bg-blue-600 hover:bg-blue-700'}
        `}
      >
        {loading ? 'Uploading...' : 'Upload'}
      </button>

      {error && (
        <p className="mt-4 text-red-600 bg-red-50 p-2 rounded">
          ❌ {error}
        </p>
      )}

      {success && (
        <p className="mt-4 text-green-600 bg-green-50 p-2 rounded">
          ✅ Recipes uploaded successfully!
        </p>
      )}

      {response && (
        <div className="mt-8">
          <h2 className="text-xl font-semibold mb-2">Upload Result</h2>
          <pre className="bg-gray-100 p-4 rounded text-sm overflow-x-auto">
            {JSON.stringify(response, null, 2)}
          </pre>
        </div>
      )}
    </div>
  );
}
