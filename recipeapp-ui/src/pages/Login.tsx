import { FormEvent, useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import type { AxiosError } from "axios";
import useAuth from "../hooks/useAuth";

export default function Login() {
  const { user, loading, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState("master@recipeapp.local");
  const [password, setPassword] = useState("Master!123");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const redirectPath = (location.state as { from?: string } | null)?.from ?? "/";

  if (!loading && user) {
    return <Navigate to={redirectPath} replace />;
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await login(email, password);
      navigate(redirectPath, { replace: true });
    } catch (err) {
      const axiosError = err as AxiosError<{ detail?: string } | string>;
      if (axiosError.response?.data) {
        if (typeof axiosError.response.data === "string") {
          setError(axiosError.response.data);
        } else {
          setError(axiosError.response.data.detail ?? "Unable to sign in");
        }
      } else {
        setError("Unable to sign in");
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-md rounded-2xl bg-white p-8 shadow-xl">
        <h1 className="text-2xl font-bold text-slate-900">Welcome back</h1>
        <p className="mt-2 text-sm text-slate-600">
          Sign in with one of the seeded demo accounts to explore the dashboard.
        </p>

        <form className="mt-8 space-y-6" onSubmit={handleSubmit}>
          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-700" htmlFor="email">
              Email address
            </label>
            <input
              id="email"
              name="email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="w-full rounded-lg border border-slate-300 px-4 py-2 text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
              placeholder="you@example.com"
              required
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-700" htmlFor="password">
              Password
            </label>
            <input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="w-full rounded-lg border border-slate-300 px-4 py-2 text-slate-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
              placeholder="••••••••"
              required
            />
          </div>

          {error && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={submitting}
            className={`w-full rounded-lg px-4 py-2 text-white transition-colors ${
              submitting ? "bg-blue-300" : "bg-blue-600 hover:bg-blue-700"
            }`}
          >
            {submitting ? "Signing in..." : "Sign in"}
          </button>
        </form>

        <div className="mt-6 rounded-lg bg-slate-100 p-4 text-xs text-slate-600">
          <p className="font-semibold text-slate-700">Demo credentials</p>
          <ul className="mt-2 space-y-1">
            <li>Master · master@recipeapp.local / Master!123</li>
            <li>Nutritionist · nutritionist@recipeapp.local / Nutritionist!123</li>
            <li>Client · client@recipeapp.local / Client!123</li>
          </ul>
        </div>
      </div>
    </div>
  );
}
