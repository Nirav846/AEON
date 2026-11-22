/* AEON Field Console — offline cache.
   Network-first for data (so promotions land fast), cache-first for the shell. */
const SHELL = "aeon-shell-v1";
const DATA = "aeon-data-v1";
const SHELL_ASSETS = ["./index.html", "./manifest.webmanifest"];

self.addEventListener("install", e => {
  e.waitUntil(caches.open(SHELL).then(c => c.addAll(SHELL_ASSETS)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", e => {
  e.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys.filter(k => k !== SHELL && k !== DATA).map(k => caches.delete(k)));
    await self.clients.claim();
  })());
});

self.addEventListener("fetch", e => {
  const url = new URL(e.request.url);
  const isData = /\/(build|data)\//.test(url.pathname);
  if (isData) {
    e.respondWith((async () => {
      try {
        const fresh = await fetch(e.request);
        const c = await caches.open(DATA);
        c.put(e.request, fresh.clone());
        return fresh;
      } catch {
        const cached = await caches.match(e.request);
        if (cached) return cached;
        throw new Error("offline and uncached");
      }
    })());
    return;
  }
  e.respondWith((async () => {
    const cached = await caches.match(e.request);
    if (cached) return cached;
    try {
      const fresh = await fetch(e.request);
      const c = await caches.open(SHELL);
      c.put(e.request, fresh.clone());
      return fresh;
    } catch {
      return caches.match("./index.html");
    }
  })());
});
