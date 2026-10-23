import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../wwwroot/service-worker.js', import.meta.url), 'utf8');
function worker() {
  const handlers = {}, requests = [], stored = [];
  vm.runInNewContext(source, {
    URL,
    self: { location: { origin: 'http://localhost' }, addEventListener: (name, handler) => { handlers[name] = handler; } },
    fetch: async (request, options) => { requests.push({ request, options }); return { ok: true, clone: () => ({}) }; },
    caches: { match: async () => undefined, open: async () => ({ put: async (request) => { stored.push(request); } }) }
  });
  async function request(path, destination, method = 'GET') {
    let response;
    handlers.fetch({ request: { url: `http://localhost${path}`, method, destination, mode: 'cors' }, respondWith: value => { response = value; } });
    if (response) await response;
    await Promise.resolve();
    return { requests, stored };
  }
  return request;
}

for (const path of ['/api/staff/1/photo', '/API/STAFF/2/PHOTO', '/api/staff-workspace/session', '/api/staff-resources/registrar/certificates', '/Staff/Certificates/1/Print']) {
  test(`private staff response stays out of the offline cache: ${path}`, async () => {
    const { requests, stored } = await worker()(path, 'image');
    assert.equal(requests.length, 1);
    assert.equal(requests[0].options.cache, 'no-store');
    assert.equal(stored.length, 0);
  });
}
test('public staff scripts retain offline asset caching', async () => {
  const { requests, stored } = await worker()('/js/staff-native.js?v=content-version', 'script');
  assert.equal(requests.length, 1);
  assert.equal(stored.length, 1);
});
test('mutations are never intercepted or cached by the service worker', async () => {
  const { requests, stored } = await worker()('/api/staff', '', 'POST');
  assert.equal(requests.length, 0);
  assert.equal(stored.length, 0);
});
