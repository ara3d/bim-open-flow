// Safe localStorage access: embedded browsers may throw on any touch of
// localStorage, so every read/write is wrapped and failures are silent.

export function readPref(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function writePref(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Persistence is a convenience; losing it is fine.
  }
}
