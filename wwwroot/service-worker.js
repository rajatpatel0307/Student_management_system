const CACHE_NAME = "eduflow-static-v5";
const STATIC_ASSETS = [
    "/offline.html",
    "/css/site.css",
    "/js/site.js",
    "/js/preferences.js",
    "/icons/eduflow.svg"
];

self.addEventListener("install", event => {
    event.waitUntil(caches.open(CACHE_NAME).then(cache => cache.addAll(STATIC_ASSETS)));
    self.skipWaiting();
});

self.addEventListener("activate", event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(key => key.startsWith("eduflow-static-") && key !== CACHE_NAME).map(key => caches.delete(key))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener("fetch", event => {
    const request = event.request;
    const requestUrl = new URL(request.url);
    if (request.method !== "GET" || requestUrl.origin !== self.location.origin) return;

    const privatePrefixes = [
        "/api/staff",
        "/Staff/Certificates",
        "/StudyMaterials/Download",
        "/Students/Photo",
        "/Admissions/DownloadDocument",
        "/Assignment/DownloadAttachment",
        "/Profile/Image",
        "/HodManagement/ProfessorImage",
        "/uploads/materials",
        "/uploads/students",
        "/uploads/admissions",
        "/uploads/assignments",
        "/uploads/profiles"
    ];
    if (privatePrefixes.some(prefix =>
        requestUrl.pathname.toLowerCase().startsWith(prefix.toLowerCase()))) {
        event.respondWith(fetch(request, { cache: "no-store" }));
        return;
    }

    if (request.mode === "navigate") {
        event.respondWith(fetch(request).catch(() => caches.match("/offline.html")));
        return;
    }

    if (!["style", "script", "font", "image"].includes(request.destination)) return;
    // Only public asset directories belong in the offline cache. An image/script
    // destination alone does not establish that a controller response is public.
    if (!/^\/(css|js|lib|icons)\//i.test(requestUrl.pathname) && requestUrl.pathname !== "/favicon.ico") return;
    event.respondWith(
        caches.match(request).then(cached => cached || fetch(request).then(response => {
            const cacheControl = response.headers.get("Cache-Control") || "";
            if (response.ok && !response.redirected && !/\b(no-store|private|no-cache)\b/i.test(cacheControl)) {
                const copy = response.clone();
                event.waitUntil(caches.open(CACHE_NAME).then(cache => cache.put(request, copy)).catch(() => {
                    // Quota or storage restrictions must not fail an online response.
                }));
            }
            return response;
        }))
    );
});
