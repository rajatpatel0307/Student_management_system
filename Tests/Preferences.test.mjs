import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../wwwroot/js/preferences.js', import.meta.url), 'utf8');
function setup({ theme = null, blocked = false } = {}) {
    const storage = new Map([['sms-theme', theme], ['sms-sidebar-collapsed', 'true']]);
    const attributes = new Map(), handlers = {}, classes = new Set();
    const button = { setAttribute: (key, value) => attributes.set(key, value), addEventListener: (key, value) => { handlers[key] = value; } };
    const root = { dataset: {}, classList: { add: value => classes.add(value) } };
    const context = {
        window: {}, matchMedia: () => ({ matches: true }),
        localStorage: {
            getItem: key => { if (blocked) throw new Error('Storage denied'); return storage.get(key); },
            setItem: (key, value) => { if (blocked) throw new Error('Storage denied'); storage.set(key, value); }
        },
        document: { documentElement: root, querySelectorAll: () => [button], addEventListener: (key, value) => { handlers[key] = value; } }
    };
    vm.runInNewContext(source, context);
    handlers.DOMContentLoaded();
    return { root, handlers, storage, attributes, classes, preferences: context.window.EduFlowPreferences };
}

test('saved dark theme and collapsed sidebar survive page initialization', () => {
    const state = setup({ theme: 'dark' });
    assert.equal(state.root.dataset.theme, 'dark');
    assert.equal(state.root.dataset.bsTheme, 'dark');
    assert.equal(state.attributes.get('aria-pressed'), 'true');
    assert.equal(state.classes.has('sidebar-collapsed'), true);
    state.handlers.click();
    assert.equal(state.root.dataset.theme, 'light');
    assert.equal(state.storage.get('sms-theme'), 'light');
    assert.equal(state.attributes.get('aria-label'), 'Use dark theme');
});

test('blocked browser storage preserves usable session preferences and controls', () => {
    const state = setup({ blocked: true });
    assert.equal(state.root.dataset.theme, 'light');
    state.handlers.click();
    assert.equal(state.root.dataset.theme, 'dark');
    assert.equal(state.preferences.get('sms-theme'), 'dark');
    state.preferences.set('sms-sidebar-collapsed', 'true');
    assert.equal(state.preferences.get('sms-sidebar-collapsed'), 'true');
});

test('unknown theme values safely fall back to light', () => {
    assert.equal(setup({ theme: 'invalid' }).root.dataset.theme, 'light');
});
