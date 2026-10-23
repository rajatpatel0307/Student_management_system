/* Staff pages use the site's Razor views, Bootstrap components and plain JavaScript. */
(() => {
    'use strict';
    const root = document.querySelector('[data-staff-native]');
    if (!root) return;
    // These forms manage their own pending state instead of navigating away.
    root.querySelectorAll('form[data-json-api], form[data-modal-form], form[data-request-search]').forEach(form => {
        form.querySelectorAll('[type="submit"]').forEach(submit => { submit.dataset.noLoading = 'true'; });
    });
    const workspace = root.dataset.workspace;
    const page = root.dataset.page;
    if (workspace && /^#\/[a-z-]+$/.test(location.hash)) {
        location.replace(`/Staff/Workspace/${encodeURIComponent(workspace)}/${encodeURIComponent(location.hash.slice(2))}`);
        return;
    }
    const csrf = root.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const errorBox = root.querySelector('[data-staff-error]');
    const successBox = root.querySelector('[data-staff-success]');
    const el = (tag, text, css) => { const node = document.createElement(tag); if (text !== undefined && text !== null) node.textContent = String(text); if (css) node.className = css; return node; };
    function showError(error) { errorBox.textContent = error.message || String(error); errorBox.classList.remove('d-none'); errorBox.scrollIntoView({ block: 'nearest' }); }
    function success(text) { errorBox.classList.add('d-none'); successBox.textContent = text; successBox.classList.remove('d-none'); }
    async function api(url, method = 'GET', data) {
        const response = await fetch(url, { method, credentials: 'same-origin', cache: 'no-store', headers: { 'Content-Type': 'application/json', RequestVerificationToken: csrf }, body: data === undefined ? undefined : JSON.stringify(data) });
        if (response.redirected || response.status === 401) throw new Error('Your session ended. Sign in again to continue.');
        if (!response.ok) {
            const problem = await response.json().catch(() => ({}));
            throw new Error(problem.message || (problem.errors ? Object.values(problem.errors).flat().join(' ') : response.status === 403 ? 'Your account does not have permission for this action.' : 'The request failed. Please try again.'));
        }
        return response.headers.get('content-type')?.includes('json') ? response.json() : undefined;
    }
    const button = (label, action, css = 'btn btn-sm btn-outline-primary') => { const node = el('button', label, css); node.type = 'button'; node.addEventListener('click', () => Promise.resolve(action()).catch(showError)); return node; };
    function display(value) { if (value == null || value === '') return '—'; if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) return new Date(value).toLocaleDateString('en-IN'); return String(value); }
    function table(target, columns, rows, actions) {
        target.replaceChildren();
        if (!rows.length) { target.append(el('p', 'No records found. Add a record or adjust the filters.', 'text-muted p-3')); return; }
        const scroll = el('div', null, 'table-responsive'); const grid = el('table', null, 'table table-hover'); const head = el('thead'); const header = el('tr');
        for (const [, title] of columns) { const th = el('th', title); th.scope = 'col'; header.append(th); }
        if (actions) header.append(el('th', 'Actions')); head.append(header); grid.append(head);
        const body = el('tbody');
        for (const row of rows) {
            const tr = el('tr'); for (const [key] of columns) tr.append(el('td', display(row[key])));
            if (actions) { const td = el('td'); const group = el('div', null, 'd-flex gap-2 flex-wrap'); actions(row, group); td.append(group); tr.append(td); }
            body.append(tr);
        }
        grid.append(body); scroll.append(grid); target.append(scroll);
    }
    function pagination(target, data, load) {
        const pages = Math.max(1, Math.ceil(data.total / 10)); const nav = el('nav', null, 'd-flex justify-content-between align-items-center gap-3 mt-3'); nav.setAttribute('aria-label', 'Record pagination'); nav.append(el('span', `${data.total} records · Page ${data.page} of ${pages}`, 'small text-muted'));
        const controls = el('div', null, 'btn-group'); if (data.page > 1) controls.append(button('Previous', () => load(data.page - 1))); if (data.page < pages) controls.append(button('Next', () => load(data.page + 1))); nav.append(controls); target.append(nav);
    }
    function field(config, current = {}) {
        const column = el('div', null, config.type === 'textarea' ? 'col-12' : 'col-md-6'); const label = el('label', config.label + (config.required ? ' *' : ''), 'form-label'); const id = 'staff-field-' + config.name; label.htmlFor = id;
        let input;
        if (config.options || config.type === 'boolean') {
            input = el('select', null, 'form-select'); const options = config.type === 'boolean' ? [['true', 'Active'], ['false', 'Inactive']] : [['', 'Select…'], ...config.options.map(value => [value, value])];
            for (const [value, text] of options) { const option = el('option', text); option.value = value; input.append(option); }
        } else if (config.type === 'textarea') { input = el('textarea', null, 'form-control'); input.rows = 3; }
        else { input = el('input', null, 'form-control'); input.type = config.type || 'text'; }
        input.name = config.name; input.id = id; input.required = !!config.required;
        if (config.type === 'number') { input.min = config.step ? '0.01' : '1'; if (config.max) input.max = config.max; if (config.step) input.step = config.step; }
        else if (config.max) input.maxLength = config.max;
        let value = current[config.name] ?? config.default ?? ''; if (config.type === 'date') value = String(value).slice(0, 10); input.value = String(value);
        column.append(label, input); return column;
    }
    let modalAction;
    const modalNode = root.querySelector('#staff-native-modal');
    function modal(title, fields, current, action) {
        modalNode.querySelector('.modal-title').textContent = title;
        modalNode.querySelector('[data-modal-error]').classList.add('d-none');
        modalNode.querySelector('[type="submit"]').classList.remove('d-none');
        const target = modalNode.querySelector('[data-modal-fields]'); target.replaceChildren(...fields.map(config => field(config, current)));
        modalAction = async form => {
            const values = Object.fromEntries(new FormData(form));
            for (const config of fields) { if (config.type === 'number') values[config.name] = values[config.name] ? Number(values[config.name]) : null; if (config.type === 'boolean') values[config.name] = values[config.name] === 'true'; }
            await action(values);
        };
        bootstrap.Modal.getOrCreateInstance(modalNode).show();
    }
    modalNode?.querySelector('form').addEventListener('submit', async event => {
        if (event.defaultPrevented || !event.currentTarget.checkValidity()) return;
        event.preventDefault(); event.stopPropagation(); const form = event.currentTarget; const submit = form.querySelector('[type="submit"]'); submit.disabled = true;
        try { await modalAction(form); bootstrap.Modal.getOrCreateInstance(modalNode).hide(); success('Changes saved.'); }
        catch (error) { const box = modalNode.querySelector('[data-modal-error]'); box.textContent = error.message; box.classList.remove('d-none'); }
        finally { submit.disabled = false; }
    });
    root.addEventListener('submit', async event => {
        const form = event.target.closest('[data-json-api]'); if (!form || event.defaultPrevented || !form.checkValidity()) return; event.preventDefault(); event.stopPropagation();
        if (form.dataset.staffConfirm && !window.confirm(form.dataset.staffConfirm)) return;
        const submit = form.querySelector('[type="submit"]'); submit.disabled = true;
        try {
            let data = Object.fromEntries(new FormData(form)); delete data.__RequestVerificationToken;
            if (form.dataset.jsonKind === 'status') data = { active: data.active === 'true', delete: data.delete === 'true' };
            if (form.dataset.jsonKind === 'permissions') data = [...form.querySelectorAll('input[name="grant"]:checked')].map(input => input.value);
            await api(form.dataset.jsonApi, 'POST', data); success('Changes saved.');
            if (form.dataset.return) location.assign(form.dataset.return); else if (form.dataset.reload) location.reload();
        } catch (error) { showError(error); } finally { submit.disabled = false; }
    });
    if (root.dataset.staffId) {
        const id = root.dataset.staffId;
        api(`/api/staff/${id}/permissions`).then(data => {
            const list = root.querySelector('[data-permission-list]'); list.replaceChildren();
            for (const item of data) { const label = el('label', null, 'd-flex align-items-center gap-2 py-2'); const input = el('input', null, 'form-check-input'); input.type = 'checkbox'; input.name = 'grant'; input.value = item.key; input.checked = item.own || item.granted; input.disabled = item.own; label.append(input, el('span', item.title + (item.own ? ' (primary role)' : ''))); list.append(label); }
            root.querySelector('[data-permission-submit]').disabled = false;
        }).catch(showError);
        api(`/api/staff/${id}/activity`).then(data => {
            const target = root.querySelector('[data-staff-activity]'); target.replaceChildren(el('h3', 'Recent logins', 'h6'));
            for (const login of data.logins) target.append(el('p', `${new Date(login.createdUtc).toLocaleString('en-IN')} · ${login.succeeded ? 'Successful' : 'Failed'} · ${login.ipAddress || 'IP unavailable'}`, 'small'));
            if (!data.logins.length) target.append(el('p', 'No logins recorded.', 'text-muted'));
            target.append(el('h3', 'Activity', 'h6 mt-4')); for (const entry of data.activity) target.append(el('p', `${entry.action} · ${new Date(entry.createdUtc).toLocaleString('en-IN')}`, 'small'));
        }).catch(showError);
    }
    if (!workspace) return;
    if (page === 'dashboard') {
        api(`/api/staff-workspace/${workspace}/dashboard`).then(data => {
            const target = root.querySelector('[data-workspace-metrics]'); target.replaceChildren();
            for (const metric of data.metrics) { const card = el('article', null, 'metric-card'); card.append(el('span', metric.label, 'metric-label'), el('strong', metric.value == null ? '—' : new Intl.NumberFormat('en-IN').format(metric.value), 'metric-value')); if (metric.note) card.append(el('small', metric.note, 'text-muted')); target.append(card); }
            table(root.querySelector('[data-recent-activity]'), [['title', 'Request'], ['category', 'Category'], ['status', 'Status'], ['updatedUtc', 'Updated']], data.recent);
        }).catch(showError); return;
    }
    if (page === 'activity') { api('/api/staff-workspace/activity').then(data => table(root.querySelector('[data-personal-activity]'), [['action', 'Action'], ['entityName', 'Record'], ['createdUtc', 'Date']], data)).catch(showError); return; }
    if (page === 'messages') {
        let box = 'inbox';
        async function loadMessages(index = 1) {
            const data = await api(`/api/staff-workspace/messages?box=${box}&page=${index}`); const target = root.querySelector('[data-messages]');
            table(target, [['subject', 'Subject'], [box === 'sent' ? 'recipient' : 'sender', box === 'sent' ? 'To' : 'From'], ['createdUtc', 'Date']], data.items, (row, actions) => {
                actions.append(button('Read', async () => {
                    modal(row.subject, [{ name: 'message', label: `From ${row.sender} · To ${row.recipient}`, type: 'textarea' }], { message: row.body }, async () => {});
                    modalNode.querySelector('textarea').readOnly = true; modalNode.querySelector('[type="submit"]').classList.add('d-none');
                    if (box === 'inbox' && !row.readUtc) { await api(`/api/staff-workspace/messages/${row.id}/read`, 'POST', {}); await loadMessages(index); }
                }));
            }); pagination(target, data, loadMessages);
        }
        root.querySelectorAll('[data-message-box]').forEach(node => node.addEventListener('click', () => { box = node.dataset.messageBox; loadMessages().catch(showError); }));
        loadMessages().catch(showError); return;
    }
    const resourceSchema = window.StaffResourceSchemas?.[workspace]?.[page];
    if (resourceSchema) {
        const panel = el('section', null, 'card section-card mb-4'); const heading = el('div', null, 'd-flex justify-content-between align-items-center flex-wrap gap-3 mb-3'); heading.append(el('h2', resourceSchema.title, 'h5 mb-0'));
        const tableArea = el('div'); const search = el('input', null, 'form-control w-auto'); search.type = 'search'; search.placeholder = 'Search official records'; search.setAttribute('aria-label', 'Search official records');
        const searchForm = el('form', null, 'd-flex gap-2 flex-wrap mb-3'); const searchButton = el('button', 'Search', 'btn btn-outline-primary'); searchButton.type = 'submit'; searchForm.append(search, searchButton);
        panel.append(heading, searchForm, tableArea); root.querySelector('[data-resource-register]').append(panel);
        async function editResource(current = {}) {
            modal(`${current.id ? 'Edit' : 'Add'} ${resourceSchema.noun}`, resourceSchema.fields.filter(config => !config.createOnly || !current.id), current, async values => { await api(`/api/staff-resources/${workspace}/${page}`, 'POST', { ...values, id: current.id || 0, rowVersion: current.rowVersion }); await loadResources(); });
        }
        if (!resourceSchema.readonly) heading.append(button(`Add ${resourceSchema.noun}`, () => editResource(), 'btn btn-primary'));
        async function loadResources(index = 1) {
            const data = await api(`/api/staff-resources/${workspace}/${page}?${new URLSearchParams({ search: search.value, page: index })}`);
            table(tableArea, [['id', 'ID'], ...resourceSchema.columns, ['status', 'Status']], data.items, resourceSchema.readonly ? null : (row, actions) => {
                const certificate = resourceSchema.workflow === 'certificate';
                if (certificate ? row.status === 'Draft' : !['Approved', 'Completed', 'Cancelled'].includes(row.status)) actions.append(button('Edit', () => editResource(row)));
                if ((page === 'expenses' && row.status === 'Draft') || (certificate && !['Issued', 'Rejected'].includes(row.status))) actions.append(button('Review', () => {
                    const options = row.status === 'Draft' ? ['Verified', 'Rejected'] : row.status === 'Verified' ? ['Approved', 'Rejected'] : ['Issued'];
                    const fields = certificate ? [{ name: 'status', label: 'Decision', options, required: true, default: options[0] }] : [];
                    fields.push({ name: 'comment', label: 'Review comment', type: 'textarea', required: true, max: 1000 });
                    modal(certificate ? 'Review certificate' : 'Approve expense', fields, {}, async values => { await api(certificate ? `/api/staff-certificates/${row.id}/review` : `/api/staff-resources/accountant/expenses/${row.id}/approve`, 'POST', { ...values, status: values.status || 'Approved', rowVersion: row.rowVersion }); await loadResources(index); });
                }));
                if (certificate && row.status === 'Issued') { const link = el('a', 'Print', 'btn btn-sm btn-outline-secondary'); link.href = `/Staff/Certificates/${row.id}/Print`; link.target = '_blank'; link.rel = 'noopener'; actions.append(link); }
            }); pagination(tableArea, data, loadResources);
        }
        searchForm.addEventListener('submit', event => { event.preventDefault(); event.stopPropagation(); loadResources().catch(showError); }); loadResources().catch(showError);
    }
    const requestSearch = root.querySelector('[data-request-search]');
    if (requestSearch) {
        async function loadRequests(index = 1) {
            const query = new URLSearchParams(new FormData(requestSearch)); query.set('page', index); const data = await api(`/api/staff-workspace/${workspace}/records/${page}?${query}`); const target = root.querySelector('[data-request-list]');
            table(target, [['id', 'ID'], ['title', 'Request'], ['reference', 'Reference'], ['studentName', 'Student'], ['dueOn', 'Due'], ['status', 'Status'], ['reviewComment', 'Review']], data.items, (row, actions) => {
                if (['Completed', 'Rejected'].includes(row.status)) return;
                actions.append(button('Review', () => { const options = row.status === 'Pending' ? ['Verified', 'Rejected'] : row.status === 'Verified' ? ['Approved', 'Rejected'] : ['Completed']; modal('Review request', [{ name: 'status', label: 'Decision', options, default: options[0], required: true }, { name: 'comment', label: 'Review comment', type: 'textarea', required: true, max: 1000 }], {}, async values => { await api(`/api/staff-workspace/${workspace}/records/${row.id}/review`, 'POST', { ...values, rowVersion: row.rowVersion }); await loadRequests(index); }); }));
            }); pagination(target, data, loadRequests);
        }
        root.querySelector('[data-new-request]').addEventListener('click', () => modal('New supporting request', [{ name: 'title', label: 'Title', required: true, max: 160 }, { name: 'reference', label: 'Reference', max: 100 }, { name: 'studentId', label: 'Student ID (optional)', type: 'number' }, { name: 'dueOn', label: 'Due date', type: 'date' }, { name: 'description', label: 'Details', type: 'textarea', max: 2000 }], {}, async values => { await api(`/api/staff-workspace/${workspace}/records/${page}`, 'POST', { ...values, dueOn: values.dueOn || null }); await loadRequests(); }));
        requestSearch.addEventListener('submit', event => { event.preventDefault(); event.stopPropagation(); loadRequests().catch(showError); }); loadRequests().catch(showError);
    }
})();
