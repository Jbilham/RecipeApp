const envApiUrl = import.meta.env.VITE_API_URL;

// In production we serve the API from the same origin as the React app.
// If VITE_API_URL is unset, fall back to the current origin instead of localhost.
export const API_BASE_URL =
  envApiUrl && envApiUrl.trim().length > 0 ? envApiUrl : window.location.origin;
