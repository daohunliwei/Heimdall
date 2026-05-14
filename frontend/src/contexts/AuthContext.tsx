"use client";

import { createContext, useContext, useEffect, useState, useCallback, ReactNode } from "react";

interface User {
  id: string;
  username: string;
  email: string | null;
  role: string;
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  loading: boolean;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const savedToken = localStorage.getItem("heimdall_token");
    if (savedToken) {
      setToken(savedToken);
      fetchUser(savedToken);
    } else {
      setLoading(false);
    }
  }, []);

  async function fetchUser(t: string) {
    try {
      const baseUrl = process.env.NEXT_PUBLIC_API_URL || "";
      const res = await fetch(`${baseUrl}/auth/me`, {
        headers: { Authorization: `Bearer ${t}` },
      });
      if (res.ok) {
        const u = await res.json();
        setUser(u);
      } else {
        localStorage.removeItem("heimdall_token");
        setToken(null);
      }
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  }

  const login = useCallback(async (username: string, password: string) => {
    const baseUrl = process.env.NEXT_PUBLIC_API_URL || "";
    const res = await fetch(`${baseUrl}/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username, password }),
    });
    if (!res.ok) throw new Error("登录失败");
    const data = await res.json();
    localStorage.setItem("heimdall_token", data.access_token);
    setToken(data.access_token);
    await fetchUser(data.access_token);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem("heimdall_token");
    setToken(null);
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: !!user,
        login,
        logout,
        loading,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
