const CACHE_NAME = "eduflow-static-v4";
const STATIC_ASSETS = [
    "/offline.html",
    "/css/site.css",
    "/js/site.js",
    "/icons/eduflow.svg"
];

self.addEventListener("install", event => {
    event.waitUntil(caches.open(CACHE_NAME).then(cache => cache.addAll(STATIC_ASSETS)));
    self.skipWaiting();
});

self.addEventListener("activate", event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))))
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
    event.respondWith(
        caches.match(request).then(cached => cached || fetch(request).then(response => {
            if (response.ok) {
                const copy = response.clone();
                caches.open(CACHE_NAME).then(cache => cache.put(request, copy));
            }
            return response;
        }))
    );
});
