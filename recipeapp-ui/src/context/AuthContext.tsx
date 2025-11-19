import axios from "axios";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  ReactNode,
} from "react";
import type { AxiosError } from "axios";
import { UserSummary } from "../types/user";

interface CurrentUserResponse {
  user: UserSummary;
  children: UserSummary[];
}

interface AuthContextValue {
  user: UserSummary | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refresh: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserSummary | null>(null);
  const [loading, setLoading] = useState(true);

  const hydrate = useCallback(async () => {
    try {
      const response = await axios.get<CurrentUserResponse>("/api/users/me");
      setUser(response.data.user);
    } catch (error) {
      const axiosError = error as AxiosError;
      if (axiosError.response?.status === 401) {
        setUser(null);
      } else {
        console.error("Failed to hydrate user context", error);
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    hydrate();
  }, [hydrate]);

  const login = useCallback(
    async (email: string, password: string) => {
      setLoading(true);
      try {
        await axios.post("/api/auth/login", { email, password });
        await hydrate();
      } catch (error) {
        setLoading(false);
        throw error;
      }
    },
    [hydrate]
  );

  const logout = useCallback(async () => {
    try {
      await axios.post("/api/auth/logout");
    } finally {
      setUser(null);
      setLoading(false);
    }
  }, []);

  const refresh = useCallback(async () => {
    setLoading(true);
    await hydrate();
  }, [hydrate]);

  return (
    <AuthContext.Provider value={{ user, loading, login, logout, refresh }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuthContext() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuthContext must be used within an AuthProvider");
  }

  return context;
}
