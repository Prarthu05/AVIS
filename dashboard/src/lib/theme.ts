const STORAGE_KEY = "avis-theme";

// Manual-only toggle, no OS prefers-color-scheme detection -- see globals.css
// for why (shared floor kiosks, not any one operator's own OS account).
// Default is "dark", not Relay's own "light" default: this app has shipped
// dark-only for most of this project, so an operator who's never touched
// the toggle keeps seeing exactly what they always have.
export type Theme = "light" | "dark";

export function getTheme(): Theme {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === "light" ? "light" : "dark";
  } catch {
    return "dark";
  }
}

export function setTheme(theme: Theme) {
  document.documentElement.setAttribute("data-theme", theme);
  try {
    localStorage.setItem(STORAGE_KEY, theme);
  } catch {
    // Private browsing / storage disabled -- the toggle still works for the
    // rest of this session, it just won't be remembered next visit.
  }
}
