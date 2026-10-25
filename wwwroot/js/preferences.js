(() => {
    "use strict";

    // Storage is optional: privacy settings and quota restrictions must not
    // disable navigation, forms, or the rest of the shared page script.
    const memory = new Map();
    const preferences = {
        get(key) {
            if (memory.has(key)) return memory.get(key);
            try { return localStorage.getItem(key); } catch { return null; }
        },
        set(key, value) {
            memory.set(key, value);
            try { localStorage.setItem(key, value); } catch { /* Session-only preference. */ }
        }
    };
    window.EduFlowPreferences = Object.freeze(preferences);

    const root = document.documentElement;
    function applyTheme(theme) {
        root.dataset.theme = theme;
        root.dataset.bsTheme = theme;
        document.querySelectorAll("[data-theme-toggle]").forEach(button => {
            button.setAttribute("aria-pressed", String(theme === "dark"));
            button.setAttribute("aria-label", theme === "dark" ? "Use light theme" : "Use dark theme");
            button.setAttribute("title", theme === "dark" ? "Use light theme" : "Use dark theme");
        });
    }
    applyTheme(preferences.get("sms-theme") === "dark" ? "dark" : "light");
    if (preferences.get("sms-sidebar-collapsed") === "true" && matchMedia("(min-width: 992px)").matches) {
        root.classList.add("sidebar-collapsed");
    }
    document.addEventListener("DOMContentLoaded", () => {
        applyTheme(root.dataset.theme);
        document.querySelectorAll("[data-theme-toggle]").forEach(button => {
            button.addEventListener("click", () => {
                const theme = root.dataset.theme === "dark" ? "light" : "dark";
                preferences.set("sms-theme", theme);
                applyTheme(theme);
            });
        });
    });
})();
