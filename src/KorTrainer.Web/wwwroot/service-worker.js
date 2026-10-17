// Development service worker: does nothing, so `dotnet run` always serves fresh files.
// The published app uses service-worker.published.js (offline cache).
self.addEventListener('fetch', () => { });
