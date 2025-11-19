import { useRef, useState } from 'react';
import axios from 'axios';

export default function UploadRecipe() {
  const [files, setFiles] = useState<FileList | null>(null);
  const [response, setResponse] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

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
      setFiles(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
    } catch (err: any) {
      setError(err.message || "Upload failed. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  const handleFileChange = (fileList: FileList | null) => {
    setFiles(fileList);
    setError(null);
    setSuccess(false);
    setResponse(null);
  };

  const clearSelection = () => {
    setFiles(null);
    setSuccess(false);
    setResponse(null);
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const selectedFiles = files ? Array.from(files) : [];

  return (
    <div className="p-10 max-w-3xl mx-auto">
      <h1 className="text-3xl font-bold mb-6 text-blue-700">Upload Recipes</h1>

      <label className="block text-sm font-medium text-gray-700 mb-2">
        Choose one or more recipe images (hold ⌘ or Ctrl to select multiple)
      </label>
      <input
        type="file"
        multiple
        ref={fileInputRef}
        onChange={(e) => handleFileChange(e.target.files)}
        className="mb-3 block w-full cursor-pointer rounded border border-gray-300 bg-white px-3 py-2 text-gray-700 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/40"
      />

      {selectedFiles.length > 0 && (
        <div className="mb-4 rounded border border-blue-200 bg-blue-50 p-4 text-sm text-blue-900 shadow-sm">
          <div className="flex items-center justify-between">
            <p className="font-semibold">
              {selectedFiles.length} file{selectedFiles.length === 1 ? "" : "s"} ready to upload
            </p>
            <button
              type="button"
              onClick={clearSelection}
              className="text-xs font-semibold text-blue-600 hover:text-blue-800"
            >
              Clear
            </button>
          </div>
          <ul className="mt-2 space-y-1">
            {selectedFiles.map((file) => (
              <li key={file.name} className="flex justify-between border-b border-blue-100 py-1 last:border-none">
                <span className="truncate pr-4">{file.name}</span>
                <span className="text-blue-700">
                  {(file.size / 1024 / 1024).toFixed(2)} MB
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}

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
