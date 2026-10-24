// Offline support for the published app (see https://aka.ms/blazor-offline-considerations).
//   * App shell: every file in the build's asset manifest is cached at install, so the trainer
//     starts without the network and an update is one atomic switch to a new cache.
//   * Recordings (/api/audio/{word}): cache first — they never change. Synthesized speech is cached by
//     text, so a new voice bumps audioCacheName and the old audio cache is dropped on activation.
//   * Other GET /api requests: network first, falling back to the last answer when offline,
//     so today's plan, words and lessons stay readable. Answers are not queued: practising
//     offline shows the usual "no connection" error when the result is saved.

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const apiCacheName = 'api-cache-v1';
const audioCacheNamePrefix = 'audio-cache-';
const audioCacheName = `${audioCacheNamePrefix}v2`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.ico$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/];

const base = '/';
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);
const audioPath = /^\/api\/audio\/(?!words$|credits$)[^/]+$/;
const uncachedApi = /^\/api\/(export|reminder)/;

async function onInstall() {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);
    // Take over at once; the page offers a reload when the new version is active.
    await self.skipWaiting();
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => (key.startsWith(cacheNamePrefix) && key !== cacheName)
            || (key.startsWith(audioCacheNamePrefix) && key !== audioCacheName))
        .map(key => caches.delete(key)));
    await self.clients.claim();
}

async function onFetch(event) {
    const request = event.request;
    if (request.method !== 'GET') {
        return fetch(request);
    }

    const url = new URL(request.url);
    if (url.origin === self.origin && url.pathname.startsWith('/api/')) {
        if (audioPath.test(url.pathname)) {
            return cacheFirst(request, audioCacheName);
        }
        return uncachedApi.test(url.pathname) ? fetch(request) : networkFirst(request, apiCacheName);
    }

    // Navigations to client routes get index.html from the app-shell cache.
    const shouldServeIndexHtml = request.mode === 'navigate' && !manifestUrlList.some(item => item === request.url);
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(shouldServeIndexHtml ? 'index.html' : request);
    return cachedResponse || fetch(request);
}

async function cacheFirst(request, name) {
    const cache = await caches.open(name);
    const cached = await cache.match(request);
    if (cached) {
        return cached;
    }
    const response = await fetch(request);
    if (response.ok) {
        await cache.put(request, response.clone());
    }
    return response;
}

async function networkFirst(request, name) {
    const cache = await caches.open(name);
    try {
        const response = await fetch(request);
        if (response.ok) {
            await cache.put(request, response.clone());
        }
        return response;
    } catch (error) {
        const cached = await cache.match(request);
        if (cached) {
            return cached;
        }
        throw error;
    }
}
