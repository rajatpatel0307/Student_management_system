import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const source = readFileSync(new URL('../wwwroot/js/private-drafts.js', import.meta.url), 'utf8');
function storage(initial = {}) {
    const values = { ...initial };
    Object.defineProperties(values, {
        getItem: { value: key => values[key] ?? null },
        setItem: { value: (key, value) => { values[key] = value; } },
        removeItem: { value: key => { delete values[key]; } }
    });
    return values;
}
function setup(owner = 'teacher-a', session = storage(), local = storage(), blocked = false, clearDraft) {
    const handlers = {};
    const denied = new Proxy({}, { get() { throw new Error('Storage denied'); }, ownKeys() { throw new Error('Storage denied'); } });
    const context = { window: {}, sessionStorage: blocked ? denied : session, localStorage: blocked ? denied : local,
        document: { body: { dataset: { draftUser: owner, clearDraft } }, querySelectorAll: () => [{ addEventListener: (name, handler) => { handlers[name] = handler; } }] } };
    vm.runInNewContext(source, context);
    return { api: context.window.EduFlowDrafts, session, handlers };
}
test('drafts are isolated by account and workflow without persistent student data', () => {
    const session = storage(), local = storage();
    const first = setup('a', session, local);
    assert.equal(first.api.save('marks:1:2', 'student-7:v1', [{ studentId: '7', score: '85' }]), true);
    assert.equal(first.api.load('marks:1:2', 'student-7:v1')[0].score, '85');
    assert.equal(setup('b', session, local).api.load('marks:1:2', 'student-7:v1'), null);
    assert.equal(first.api.load('marks:2:2', 'student-7:v1'), null);
    assert.equal(Object.keys(local).length, 0);
});
test('changed roster or record revision prevents stale draft restoration', () => {
    const state = setup();
    state.api.save('marks:1:2', 'student-7:v1', []);
    assert.equal(state.api.load('marks:1:2', 'student-7:v2'), null);
    assert.equal(Object.keys(state.session).length, 0);
});
test('legacy shared drafts are retired while unrelated storage survives', () => {
    const local = storage({ 'marks:1:2': 'sensitive', 'attendance:1:today': 'sensitive', 'sms-theme': 'dark' });
    setup('a', storage(), local);
    assert.deepEqual(Object.keys(local), ['sms-theme']);
});
test('blocked storage and anonymous pages report unavailable drafts without throwing', () => {
    assert.equal(setup('a', storage(), storage(), true).api.save('scope', 'roster', []), false);
    assert.equal(setup('a', storage(), storage(), true).api.load('scope', 'roster'), null);
    assert.equal(setup('').api.save('scope', 'roster', []), false);
});
test('expired drafts are discarded and logout clears only private drafts', () => {
    const session = storage({ 'eduflow-draft:a:scope': JSON.stringify({ roster: 'roster', savedAt: 0, values: [] }), preference: 'retain' });
    const state = setup('a', session);
    assert.equal(state.api.load('scope', 'roster'), null);
    state.api.save('scope', 'roster', []);
    state.handlers.submit();
    assert.deepEqual(Object.keys(session), ['preference']);
});
test('server-confirmed save clears only the matching draft', () => {
    const first = setup();
    first.api.save('marks:1:2', 'roster', []);
    first.api.save('attendance:3:today', 'roster', []);
    const next = setup('teacher-a', first.session, storage(), false, 'marks:1:2');
    assert.equal(next.api.load('marks:1:2', 'roster'), null);
    assert.notEqual(next.api.load('attendance:3:today', 'roster'), null);
});
