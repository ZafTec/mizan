"use client";

import { useState, useEffect, useCallback } from "react";

type Theme = "light" | "dark" | "system";

interface AppearanceSettings {
    theme: Theme;
    compactMode: boolean;
    reduceAnimations: boolean;
}

const STORAGE_KEY = "macrochef-appearance";

const defaults: AppearanceSettings = {
    theme: "light",
    compactMode: false,
    reduceAnimations: false,
};

function getStoredSettings(): AppearanceSettings {
    if (typeof window === "undefined") return defaults;
    try {
        const stored = localStorage.getItem(STORAGE_KEY);
        return stored ? { ...defaults, ...JSON.parse(stored) } : defaults;
    } catch {
        return defaults;
    }
}

function applyTheme(theme: Theme) {
    if (typeof window === "undefined") return;
    const root = document.documentElement;
    if (theme === "system") {
        const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
        root.classList.toggle("dark", prefersDark);
    } else {
        root.classList.toggle("dark", theme === "dark");
    }
}

function applySettings(settings: AppearanceSettings) {
    if (typeof window === "undefined") return;
    applyTheme(settings.theme);
    document.documentElement.classList.toggle("compact", settings.compactMode);
    document.documentElement.classList.toggle("reduce-motion", settings.reduceAnimations);
}

export function useTheme() {
    const [settings, setSettings] = useState<AppearanceSettings>(getStoredSettings);

    useEffect(() => {
        applySettings(settings);

        if (settings.theme === "system") {
            const mq = window.matchMedia("(prefers-color-scheme: dark)");
            const handler = () => applyTheme("system");
            mq.addEventListener("change", handler);
            return () => mq.removeEventListener("change", handler);
        }
    }, [settings]);

    const updateSettings = useCallback((updates: Partial<AppearanceSettings>) => {
        setSettings((prev) => {
            const next = { ...prev, ...updates };
            localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
            applySettings(next);
            return next;
        });
    }, []);

    return { settings, updateSettings };
}
