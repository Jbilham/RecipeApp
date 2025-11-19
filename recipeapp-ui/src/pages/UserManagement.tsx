import axios, { AxiosError } from "axios";
import { useCallback, useEffect, useMemo, useState, FormEvent } from "react";
import { Navigate } from "react-router-dom";
import useAuth from "../hooks/useAuth";
import { UserSummary } from "../types/user";

interface CurrentUserResponse {
  user: UserSummary;
  children: UserSummary[];
}

export default function UserManagement() {
  const { user, loading } = useAuth();
  const [nutritionists, setNutritionists] = useState<UserSummary[]>([]);
  const [clients, setClients] = useState<UserSummary[]>([]);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isFetching, setIsFetching] = useState(false);
  const [creatingNutritionist, setCreatingNutritionist] = useState(false);
  const [creatingClient, setCreatingClient] = useState(false);

  const [nutritionistForm, setNutritionistForm] = useState({
    email: "",
    password: "Nutritionist!123",
    phoneNumber: "",
  });

  const [clientForm, setClientForm] = useState({
    email: "",
    password: "Client!123",
    nutritionistId: "",
    phoneNumber: "",
  });

  const canManageNutritionists = user?.role === "Master";
  const canManageClients = user?.role === "Master" || user?.role === "Nutritionist";

  const refreshData = useCallback(async () => {
    setIsFetching(true);
    setErrorMessage(null);
    try {
      const meResponse = await axios.get<CurrentUserResponse>("/api/users/me");
      if (user?.role === "Master") {
        setNutritionists(meResponse.data.children);
      } else if (user?.role === "Nutritionist") {
        setNutritionists([meResponse.data.user]);
      }

      const clientsResponse = await axios.get<UserSummary[]>("/api/users/clients");
      setClients(clientsResponse.data);
    } catch (error) {
      setErrorMessage("Unable to load users right now. Please try again in a moment.");
    } finally {
      setIsFetching(false);
    }
  }, [user?.role]);

  useEffect(() => {
    if (!loading && user && canManageClients) {
      refreshData();
    }
  }, [loading, user, canManageClients, refreshData]);

  useEffect(() => {
    if (user?.role === "Nutritionist") {
      setClientForm((current) => ({ ...current, nutritionistId: user.id }));
    }
  }, [user]);

  if (!loading && !user) {
    return <Navigate to="/login" state={{ from: "/users" }} replace />;
  }

  if (!canManageClients && user) {
    return (
      <div className="mx-auto mt-12 max-w-3xl rounded-2xl bg-white p-8 shadow">
        <h1 className="text-2xl font-semibold text-slate-900">User management</h1>
        <p className="mt-4 text-slate-600">
          Your account type does not grant access to manage additional users. Please contact your
          nutritionist or the master administrator if you believe this is incorrect.
        </p>
      </div>
    );
  }

  const extractError = (error: unknown) => {
    const axiosError = error as AxiosError<{ errors?: string[] } | string>;
    if (!axiosError.response) return "Something went wrong.";
    const data = axiosError.response.data;
    if (typeof data === "string") return data;
    if (data?.errors && data.errors.length > 0) return data.errors.join(", ");
    return "Unable to process the request.";
  };

  const handleCreateNutritionist = async (event: FormEvent) => {
    event.preventDefault();
    setCreatingNutritionist(true);
    setStatusMessage(null);
    setErrorMessage(null);
    try {
      await axios.post("/api/users/nutritionists", nutritionistForm);
      setStatusMessage(`Created nutritionist ${nutritionistForm.email}`);
      setNutritionistForm({ email: "", password: "Nutritionist!123", phoneNumber: "" });
      await refreshData();
    } catch (error) {
      setErrorMessage(extractError(error));
    } finally {
      setCreatingNutritionist(false);
    }
  };

  const handleCreateClient = async (event: FormEvent) => {
    event.preventDefault();
    setCreatingClient(true);
    setStatusMessage(null);
    setErrorMessage(null);

    const payload = {
      email: clientForm.email,
      password: clientForm.password,
      phoneNumber: clientForm.phoneNumber,
      nutritionistId:
        user?.role === "Nutritionist" ? user.id : clientForm.nutritionistId || null,
    };

    if (user?.role === "Master" && !payload.nutritionistId) {
      setErrorMessage("Select a nutritionist to assign this client to.");
      setCreatingClient(false);
      return;
    }

    try {
      await axios.post("/api/users/clients", payload);
      setStatusMessage(`Created client ${clientForm.email}`);
      setClientForm({
        email: "",
        password: "Client!123",
        nutritionistId: user?.role === "Nutritionist" ? user.id : "",
        phoneNumber: "",
      });
      await refreshData();
    } catch (error) {
      setErrorMessage(extractError(error));
    } finally {
      setCreatingClient(false);
    }
  };

  const nutritionistLookup = useMemo(() => {
    const map = new Map<string, UserSummary>();
    nutritionists.forEach((n) => map.set(n.id, n));
    return map;
  }, [nutritionists]);

  return (
    <div className="mx-auto max-w-5xl px-4 py-10">
      <div className="mb-8 flex flex-col gap-3">
        <h1 className="text-3xl font-bold text-slate-900">User management</h1>
        <p className="text-slate-600">
          Invite new nutritionists and clients to the platform. Master admins can manage the entire
          hierarchy, while nutritionists can add clients under their care.
        </p>
        {statusMessage && (
          <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
            {statusMessage}
          </div>
        )}
        {errorMessage && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
            {errorMessage}
          </div>
        )}
      </div>

      <div className="grid gap-8 lg:grid-cols-2">
        {canManageNutritionists && (
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="mb-4">
              <h2 className="text-xl font-semibold text-slate-900">Create nutritionist</h2>
              <p className="text-sm text-slate-600">
                Nutritionists inherit access from the master account and can invite their own clients.
              </p>
            </div>
            <form className="space-y-4" onSubmit={handleCreateNutritionist}>
              <div>
                <label className="text-sm font-medium text-slate-700">Email</label>
                <input
                  type="email"
                  className="mt-1 w-full rounded-lg border border-slate-300 px-4 py-2 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
                  value={nutritionistForm.email}
                  onChange={(event) =>
                    setNutritionistForm((current) => ({ ...current, email: event.target.value }))
                  }
                  required
                />
              </div>
              <div>
                <label className="text-sm font-medium text-slate-700">Temporary password</label>
                <input
                  type="text"
                  className="mt-1 w-full rounded-lg border border-slate-300 px-4 py-2 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
                  value={nutritionistForm.password}
                  onChange={(event) =>
                    setNutritionistForm((current) => ({ ...current, password: event.target.value }))
                  }
                  required
                />
              </div>
              <div>
                <label className="text-sm font-medium text-slate-700">Phone number (optional)</label>
                <input
                  type="tel"
                  className="mt-1 w-full rounded-lg border border-slate-300 px-4 py-2 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
                  value={nutritionistForm.phoneNumber}
                  onChange={(event) =>
                    setNutritionistForm((current) => ({ ...current, phoneNumber: event.target.value }))
                  }
                />
              </div>
              <button
                type="submit"
                disabled={creatingNutritionist}
                className={`w-full rounded-lg px-4 py-2 font-semibold text-white transition-colors ${
                  creatingNutritionist ? "bg-blue-300" : "bg-blue-600 hover:bg-blue-700"
                }`}
              >
                {creatingNutritionist ? "Creating..." : "Invite nutritionist"}
              </button>
            </form>
          </section>
        )}

        {canManageClients && (
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="mb-4">
              <h2 className="text-xl font-semibold text-slate-900">Create client</h2>
              <p className="text-sm text-slate-600">
                Clients gain access to assigned meal plans, shopping lists, and shared resources.
              </p>
            </div>
            <form className="space-y-4" onSubmit={handleCreateClient}>
              <div>
                <label className="text-sm font-medium text-slate-700">Email</label>
                <input
                  type="email"
                  className="mt-1 w-full rounded-lg border border-slate-300 px-4 py-2 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
                  value={clientForm.email}
                  onChange={(event) =>
                    setClientForm((current) => ({ ...current, email: event.target.value }))
                  }
                  required
                />
              </div>
              <div>
                <label className="text-sm font-medium text-slate-700">Temporary password</label>
                <input
                  type="text"
                  className="mt-1 w-full rounded-lg border border-slate-300 px-4 py-2 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
                  value={clientForm.password}
                  onChange={(event) =>
                    setClientForm((current) => ({ ...current, password: event.target.value }))
                  }
                  required
                />
              </div>
              {user?.role === "Master" && (
                <div>
                  <label className="text-sm font-medium text-slate-700">Assign to nutritionist</label>
                  <select
                    className="mt-1 w-full rounded-lg border border-slate-300 px-4 py-2 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
                    value={clientForm.nutritionistId}
                    onChange={(event) =>
                      setClientForm((current) => ({ ...current, nutritionistId: event.target.value }))
                    }
                    required
                  >
                    <option value="">Select nutritionist</option>
                    {nutritionists.map((n) => (
                      <option key={n.id} value={n.id}>
                        {n.email}
                      </option>
                    ))}
                  </select>
                </div>
              )}
              <div>
                <label className="text-sm font-medium text-slate-700">Phone number (optional)</label>
                <input
                  type="tel"
                  className="mt-1 w-full rounded-lg border border-slate-300 px-4 py-2 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
                  value={clientForm.phoneNumber}
                  onChange={(event) =>
                    setClientForm((current) => ({ ...current, phoneNumber: event.target.value }))
                  }
                />
              </div>
              <button
                type="submit"
                disabled={creatingClient}
                className={`w-full rounded-lg px-4 py-2 font-semibold text-white transition-colors ${
                  creatingClient ? "bg-emerald-300" : "bg-emerald-600 hover:bg-emerald-700"
                }`}
              >
                {creatingClient ? "Creating..." : "Invite client"}
              </button>
            </form>
          </section>
        )}
      </div>

      <div className="mt-10 grid gap-8 lg:grid-cols-2">
        {canManageNutritionists && (
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="mb-4 flex items-center justify-between">
              <div>
                <h3 className="text-lg font-semibold text-slate-900">Nutritionists</h3>
                <p className="text-sm text-slate-600">{nutritionists.length} active</p>
              </div>
              {isFetching && <span className="text-xs text-slate-500">Refreshing...</span>}
            </div>
            <ul className="divide-y divide-slate-100">
              {nutritionists.length === 0 && (
                <li className="py-4 text-sm text-slate-500">No nutritionists yet.</li>
              )}
              {nutritionists.map((n) => (
                <li key={n.id} className="py-4">
                  <p className="font-medium text-slate-900">{n.email}</p>
                  <p className="text-sm text-slate-500">User ID: {n.id}</p>
                </li>
              ))}
            </ul>
          </section>
        )}

        {canManageClients && (
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="mb-4 flex items-center justify-between">
              <div>
                <h3 className="text-lg font-semibold text-slate-900">Clients</h3>
                <p className="text-sm text-slate-600">{clients.length} active</p>
              </div>
              {isFetching && <span className="text-xs text-slate-500">Refreshing...</span>}
            </div>
            <ul className="divide-y divide-slate-100">
              {clients.length === 0 && (
                <li className="py-4 text-sm text-slate-500">No clients yet.</li>
              )}
              {clients.map((client) => (
                <li key={client.id} className="py-4">
                  <p className="font-medium text-slate-900">{client.email}</p>
                  <p className="text-sm text-slate-500">
                    Assigned to {nutritionistLookup.get(client.parentUserId ?? "")?.email ?? "Unknown"}
                  </p>
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>
    </div>
  );
}
