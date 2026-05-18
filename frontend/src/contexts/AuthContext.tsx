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

const NO_AUTH_USER: User = {
  id: "no-auth",
  username: "admin",
  email: null,
  role: "Admin",
};

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function initAuth() {
      try {
        // 先检查是否需要认证
        const statusRes = await fetch("/api/auth/status");
        if (statusRes.ok) {
          const status = await statusRes.json();
          if (!status.auth_required || status.authRequired === false) {
            // auth=none 模式：自动设置默认管理员用户
            setUser(NO_AUTH_USER);
            setToken("no-auth-token");
            setLoading(false);
            return;
          }
        }
      } catch {
        // 如果检查失败，回退到 token 检查
      }

      // JWT 模式：检查本地存储的 token
      const savedToken = localStorage.getItem("heimdall_token");
      if (savedToken) {
        setToken(savedToken);
        fetchUser(savedToken);
      } else {
        setLoading(false);
      }
    }

    initAuth();
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
