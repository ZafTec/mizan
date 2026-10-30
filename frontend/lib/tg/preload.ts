/**
 * What a Mini App page will show, fetched before the person opens it. The home page
 * starts these once it has loaded; a page that finds its result settled renders it at
 * once, so its title can travel from the home row that opened it, and then loads again
 * for anything that changed since.
 */
const settled = new Map<string, unknown>();
const running = new Map<string, Promise<unknown>>();

export function preload<T>(key: string, load: () => Promise<T>): Promise<T> {
  const existing = running.get(key) as Promise<T> | undefined;
  if (existing) return existing;
  const next = load()
    .then((value) => {
      settled.set(key, value);
      return value;
    })
    .finally(() => running.delete(key));
  running.set(key, next);
  return next;
}

export function preloaded<T>(key: string): T | null {
  return (settled.get(key) as T | undefined) ?? null;
}

export function remember<T>(key: string, value: T) {
  settled.set(key, value);
}
