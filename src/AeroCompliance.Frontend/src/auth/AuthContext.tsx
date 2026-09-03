import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import {
  api,
  clearTokens,
  getAccessToken,
  getRefreshToken,
  storeTokens,
} from "../api/client";
import type { CurrentUser, TokenPair } from "../api/types";

interface AuthState {
  user: CurrentUser | null;
  loading: boolean;
  login(email: string, password: string): Promise<void>;
  logout(): Promise<void>;
}
const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [loading, setLoading] = useState(true);
  const restore = async () => {
    try {
      if (getAccessToken())
        setUser((await api.get<CurrentUser>("/users/me")).data);
    } catch {
      clearTokens();
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => {
    void restore();
    const expired = () => {
      clearTokens();
      setUser(null);
    };
    window.addEventListener("aerocompliance:expired", expired);
    return () => window.removeEventListener("aerocompliance:expired", expired);
  }, []);
  const login = async (email: string, password: string) => {
    const tokens = (
      await api.post<TokenPair>("/auth/login", { email, password })
    ).data;
    storeTokens(tokens);
    try {
      setUser((await api.get<CurrentUser>("/users/me")).data);
    } catch (e) {
      clearTokens();
      throw e;
    }
  };
  const logout = async () => {
    const refreshToken = getRefreshToken();
    try {
      if (refreshToken) await api.post("/auth/logout", { refreshToken });
    } finally {
      clearTokens();
      setUser(null);
    }
  };
  return (
    <AuthContext.Provider value={{ user, loading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error("AuthProvider missing");
  return value;
}
export const hasRole = (
  user: CurrentUser | null,
  roles: CurrentUser["role"]["name"][],
) => !!user && roles.includes(user.role.name);
