import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

// Exercise the production event handler with controlled asynchronous lookups.
const site = readFileSync(new URL('../wwwroot/js/site.js', import.meta.url), 'utf8');
const start = site.indexOf('    function setupDependentSelects()');
const end = site.indexOf('    function setupEditForms()', start);

class Select extends EventTarget {
    constructor(dataset = {}, value = '') {
        super(); this.dataset = dataset; this.value = value;
        this.attributes = new Map(); this.disabled = false;
    }
    set innerHTML(html) { this.html = html; this.value = ''; }
    get innerHTML() { return this.html; }
    setAttribute(name, value) { this.attributes.set(name, value); }
    removeAttribute(name) { this.attributes.delete(name); }
}

function setup() {
    const department = new Select({}, '1');
    const classes = new Select({ dependentParent: '#department', dependentUrl: '/classes?departmentId={value}' }, '10');
    const sections = new Select({ dependentParent: '#class', dependentUrl: '/sections?classId={value}' }, '100');
    const requests = [];
    const errors = [];
    const context = vm.createContext({
        Event, AbortController,
        escapeHtml: value => String(value),
        showToast: message => errors.push(message),
        document: {
            querySelectorAll: () => [classes, sections],
            querySelector: selector => selector === '#department' ? department : classes
        },
        fetch: (url, options) => new Promise((resolve, reject) => requests.push({ url, options, resolve, reject }))
    });
    vm.runInContext(site.slice(start, end) + '\nsetupDependentSelects();', context);
    return { department, classes, sections, requests, errors };
}
const tick = () => new Promise(resolve => setImmediate(resolve));
const response = options => ({ ok: true, json: async () => options });

test('changing department immediately clears both class and stale section', async () => {
    const state = setup();
    state.department.value = '2';
    state.department.dispatchEvent(new Event('change'));
    assert.equal(state.classes.value, '');
    assert.equal(state.sections.value, '');
    assert.equal(state.classes.disabled, true);
    assert.equal(state.sections.disabled, true);
    state.requests[0].resolve(response([{ value: 20, text: 'Class 20' }]));
    await tick();
    assert.equal(state.classes.disabled, false);
    assert.equal(state.sections.disabled, true);
    state.classes.value = '20';
    state.classes.dispatchEvent(new Event('change'));
    assert.equal(state.requests[1].url, '/sections?classId=20');
    state.requests[1].resolve(response([{ value: 200, text: 'Section 200' }]));
    await tick();
    assert.equal(state.sections.disabled, false);
});

test('old requests cannot clear the new busy state or replace current options', async () => {
    const state = setup();
    state.department.dispatchEvent(new Event('change'));
    state.department.value = '2';
    state.department.dispatchEvent(new Event('change'));
    assert.equal(state.requests[0].options.signal.aborted, true);
    state.requests[0].resolve(response([{ value: 10, text: 'Old class' }]));
    await tick();
    assert.equal(state.classes.attributes.get('aria-busy'), 'true');
    assert.equal(state.classes.innerHTML.includes('Old class'), false);
    state.requests[1].resolve(response([{ value: 20, text: 'Current class' }]));
    await tick();
    assert.equal(state.classes.innerHTML.includes('Current class'), true);
    assert.equal(state.classes.attributes.has('aria-busy'), false);
});

test('failed lookup disables choices and surfaces feedback', async () => {
    const state = setup();
    state.department.dispatchEvent(new Event('change'));
    state.requests[0].resolve({ ok: false });
    await tick();
    assert.equal(state.classes.disabled, true);
    assert.equal(state.sections.value, '');
    assert.equal(state.errors.length, 1);
    assert.equal(state.classes.attributes.has('aria-busy'), false);
});
