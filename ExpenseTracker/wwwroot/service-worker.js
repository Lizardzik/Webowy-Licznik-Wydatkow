const CACHE_NAME = 'expense-tracker-v7';
const OFFLINE_URL = '/offline.html';
const STATIC_ASSETS = [
    '/offline.html',
    '/manifest.json',
    '/css/theme.css',
    '/css/site.css',
    '/css/site-mobile.css',
    '/js/app-core.js',
    '/js/navbar.js',
    '/js/sidebar-desktop.js',
    '/js/sidebar-mobile.js',
    '/js/category-select.js',
    '/icons/icon-192.png',
    '/icons/icon-512.png'
];

self.addEventListener('install', function (event) {
    event.waitUntil(
        caches.open(CACHE_NAME).then(function (cache) {
            return Promise.allSettled(
                STATIC_ASSETS.map(function (assetUrl) {
                    return cache.add(assetUrl);
                })
            );
        })
    );
    self.skipWaiting();
});

self.addEventListener('activate', function (event) {
    event.waitUntil(
        caches.keys().then(function (cacheNames) {
            return Promise.all(
                cacheNames
                    .filter(function (name) {
                        return name !== CACHE_NAME;
                    })
                    .map(function (name) {
                        return caches.delete(name);
                    })
            );
        })
    );
    self.clients.claim();
});

self.addEventListener('fetch', function (event) {
    if (event.request.method !== 'GET') {
        return;
    }

    const requestUrl = new URL(event.request.url);
    if (requestUrl.origin !== self.location.origin) {
        return;
    }

    if (event.request.mode === 'navigate') {
        event.respondWith(
            fetch(event.request).catch(function () {
                return caches.match(OFFLINE_URL);
            })
        );
        return;
    }

    const isStaticAsset =
        requestUrl.pathname.startsWith('/css/') ||
        requestUrl.pathname.startsWith('/js/') ||
        requestUrl.pathname.startsWith('/icons/') ||
        requestUrl.pathname === '/manifest.json';

    if (isStaticAsset) {
        event.respondWith(
            caches.match(event.request).then(function (cachedResponse) {
                const networkFetch = fetch(event.request)
                    .then(function (networkResponse) {
                        if (networkResponse && networkResponse.ok) {
                            const responseClone = networkResponse.clone();
                            caches.open(CACHE_NAME).then(function (cache) {
                                cache.put(event.request, responseClone);
                            });
                        }
                        return networkResponse;
                    })
                    .catch(function () {
                        return cachedResponse;
                    });

                return cachedResponse || networkFetch;
            })
        );
    }
});