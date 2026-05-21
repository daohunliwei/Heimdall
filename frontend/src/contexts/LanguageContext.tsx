 
'use client';

import React, { createContext, useContext, useMemo, useState, ReactNode } from 'react';
import zhMessages from '../messages/zh.json';

type Messages = Record<string, any>;
type LanguageContextType = {
  language: string;
  setLanguage: (lang: string) => void;
  messages: Messages;
  supportedLanguages: Record<string, string>;
};

const LanguageContext = createContext<LanguageContextType | undefined>(undefined);
const supportedLanguages = { zh: '中文' };

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<string>('zh');

  const value = useMemo<LanguageContextType>(() => ({
    language,
    setLanguage: () => {
      setLanguageState('zh');
      if (typeof document !== 'undefined') {
        document.documentElement.lang = 'zh';
      }
    },
    messages: zhMessages as Messages,
    supportedLanguages,
  }), [language]);

  return (
    <LanguageContext.Provider value={value}>
      {children}
    </LanguageContext.Provider>
  );
}

export function useLanguage() {
  const context = useContext(LanguageContext);
  if (context === undefined) {
    throw new Error('useLanguage 必须在 LanguageProvider 内使用');
  }
  return context;
}
