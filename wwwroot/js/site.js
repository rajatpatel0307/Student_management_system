(() => {
    "use strict";

    if ("serviceWorker" in navigator &&
        (location.protocol === "https:" || location.hostname === "localhost")) {
        window.addEventListener("load", () => {
            navigator.serviceWorker.register("/service-worker.js").catch(() => {
                // The application remains fully usable when installation is unavailable.
            });
        });
    }

    const root = document.documentElement;
    const body = document.body;
    const sidebar = document.getElementById("appSidebar");
    const backdrop = document.getElementById("sidebarBackdrop");
    const loader = document.getElementById("pageLoader");
    const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
    const desktopSidebar = matchMedia("(min-width: 992px)");
    const sidebarToggle = document.querySelector("[data-sidebar-toggle]");
    const sidebarPanels = sidebar
        ? [...sidebar.querySelectorAll(".brand-link, .sidebar-nav, .sidebar-footer")]
        : [];

    root.dataset.theme = "light";
    localStorage.removeItem("sms-theme");

    function updateSidebarControl(isExpanded) {
        sidebarToggle?.setAttribute("aria-expanded", String(isExpanded));
        sidebarToggle?.setAttribute("aria-label", isExpanded ? "Collapse navigation" : "Expand navigation");
        sidebarToggle?.setAttribute("title", isExpanded ? "Collapse navigation" : "Expand navigation");
    }

    function setSidebarPanelsVisible(visible) {
        sidebarPanels.forEach(panel => {
            panel.toggleAttribute("inert", !visible);
            panel.setAttribute("aria-hidden", String(!visible));
        });
    }

    function setSidebar(open) {
        if (!sidebar || desktopSidebar.matches) return;
        if (!open && sidebar.contains(document.activeElement)) sidebarToggle?.focus();
        sidebar.classList.toggle("open", open);
        setSidebarPanelsVisible(open);
        backdrop?.classList.toggle("show", open);
        backdrop?.setAttribute("aria-hidden", String(!open));
        updateSidebarControl(open);
        body.style.overflow = open ? "hidden" : "";
        document.querySelector(".app-shell")?.toggleAttribute("inert", open);
    }

    function restoreDesktopSidebar() {
        const collapsed = localStorage.getItem("sms-sidebar-collapsed") === "true";
        root.classList.toggle("sidebar-collapsed", collapsed);
        sidebar?.classList.remove("open");
        setSidebarPanelsVisible(true);
        backdrop?.classList.remove("show");
        backdrop?.setAttribute("aria-hidden", "true");
        body.style.overflow = "";
        document.querySelector(".app-shell")?.removeAttribute("inert");
        updateSidebarControl(!collapsed);
    }

    sidebarToggle?.addEventListener("click", () => {
        if (desktopSidebar.matches) {
            const collapsed = !root.classList.contains("sidebar-collapsed");
            root.classList.toggle("sidebar-collapsed", collapsed);
            localStorage.setItem("sms-sidebar-collapsed", String(collapsed));
            updateSidebarControl(!collapsed);
            return;
        }
        setSidebar(!sidebar?.classList.contains("open"));
    });
    backdrop?.addEventListener("click", () => setSidebar(false));
    document.addEventListener("keydown", event => {
        if (event.key === "Tab" && !desktopSidebar.matches && sidebar?.classList.contains("open")) {
            const controls = [...sidebar.querySelectorAll('a[href], button, summary, input, select, textarea, [tabindex="0"]')]
                .filter(control => control.getClientRects().length && !control.disabled && !control.closest("[inert]"));
            const first = controls[0];
            const last = controls[controls.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last?.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first?.focus();
            }
        }
        if (event.key === "Escape" && !desktopSidebar.matches && sidebar?.classList.contains("open")) {
            setSidebar(false);
            sidebarToggle?.focus();
        }
    });
    desktopSidebar.addEventListener("change", event => {
        if (event.matches) restoreDesktopSidebar();
        else {
            root.classList.remove("sidebar-collapsed");
            setSidebar(false);
        }
    });
    if (desktopSidebar.matches) restoreDesktopSidebar();
    else setSidebar(false);

    document.querySelectorAll(".nav-disclosure").forEach(details => {
        const summary = details.querySelector(":scope > summary");
        const summaryLabel = summary?.querySelector("span")?.textContent.trim();
        if (summary && summaryLabel) {
            summary.setAttribute("title", summaryLabel);
            summary.setAttribute("aria-label", summaryLabel);
            summary.addEventListener("click", event => {
                if (!desktopSidebar.matches || !root.classList.contains("sidebar-collapsed")) return;
                event.preventDefault();
                root.classList.remove("sidebar-collapsed");
                localStorage.setItem("sms-sidebar-collapsed", "false");
                updateSidebarControl(true);
                details.open = true;
            });
        }
        details.addEventListener("toggle", () => {
            if (!details.open) return;
            document.querySelectorAll(".nav-disclosure[open]").forEach(other => {
                if (other !== details && !other.querySelector(".active")) other.open = false;
            });
        });
    });

    document.querySelectorAll(".sidebar-nav .nav-link").forEach(link => {
        const label = link.querySelector("span")?.textContent.trim();
        if (!label) return;
        link.setAttribute("title", label);
        if (!link.hasAttribute("aria-label")) link.setAttribute("aria-label", label);
    });
    document.querySelectorAll(".sidebar-nav a.active").forEach(link => link.setAttribute("aria-current", "page"));

    const commandDialog = document.getElementById("commandDialog");
    const commandInput = document.getElementById("commandSearch");
    const commandResults = document.getElementById("commandResults");
    const commandEmpty = commandDialog?.querySelector(".command-empty");
    let commandItems = [];
    let selectedIndex = 0;

    function collectCommands() {
        commandItems = [...document.querySelectorAll(".sidebar-nav a[href]")]
            .map(link => ({
                label: link.textContent.trim().replace(/\s+/g, " "),
                href: link.href,
                group: link.closest(".nav-group")?.querySelector(".nav-label")?.textContent.trim() || "Navigation",
                icon: link.querySelector("i")?.className || "fa-regular fa-circle"
            }))
            .filter((item, index, items) => items.findIndex(candidate => candidate.href === item.href) === index);
    }

    function renderCommands(query = "") {
        const normalized = query.trim().toLowerCase();
        const matches = commandItems.filter(item => `${item.label} ${item.group}`.toLowerCase().includes(normalized)).slice(0, 12);
        selectedIndex = Math.min(selectedIndex, Math.max(0, matches.length - 1));
        if (commandResults) {
            commandResults.innerHTML = matches.map((item, index) => `
                <a class="command-result ${index === selectedIndex ? "selected" : ""}" href="${item.href}" data-command-index="${index}">
                    <i class="${item.icon}" aria-hidden="true"></i>
                    <span>${escapeHtml(item.label)}<small>${escapeHtml(item.group)}</small></span>
                    <i class="fa-solid fa-arrow-up-right-from-square" aria-hidden="true"></i>
                </a>`).join("");
        }
        if (commandEmpty) commandEmpty.hidden = matches.length > 0;
        return matches;
    }

    function openCommands() {
        if (!commandDialog) return;
        collectCommands();
        commandDialog.hidden = false;
        body.style.overflow = "hidden";
        commandInput.value = "";
        renderCommands();
        requestAnimationFrame(() => commandInput?.focus());
    }

    function closeCommands() {
        if (!commandDialog) return;
        commandDialog.hidden = true;
        body.style.overflow = "";
    }

    document.querySelectorAll("[data-command-open]").forEach(button => button.addEventListener("click", openCommands));
    document.querySelectorAll("[data-command-close]").forEach(button => button.addEventListener("click", closeCommands));
    commandInput?.addEventListener("input", event => {
        selectedIndex = 0;
        renderCommands(event.target.value);
    });
    commandInput?.addEventListener("keydown", event => {
        const matches = renderCommands(commandInput.value);
        if (event.key === "ArrowDown") {
            event.preventDefault();
            selectedIndex = (selectedIndex + 1) % Math.max(1, matches.length);
            renderCommands(commandInput.value);
        } else if (event.key === "ArrowUp") {
            event.preventDefault();
            selectedIndex = (selectedIndex - 1 + Math.max(1, matches.length)) % Math.max(1, matches.length);
            renderCommands(commandInput.value);
        } else if (event.key === "Enter" && matches[selectedIndex]) {
            location.href = matches[selectedIndex].href;
        } else if (event.key === "Escape") {
            closeCommands();
        }
    });
    addEventListener("keydown", event => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
            event.preventDefault();
            commandDialog?.hidden === false ? closeCommands() : openCommands();
        }
        if (event.key === "Escape") {
            setSidebar(false);
            closeCommands();
        }
    });

    function escapeHtml(value) {
        return String(value).replace(/[&<>"']/g, char => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;"
        })[char]);
    }

    function enhanceForms() {
        document.querySelectorAll("form").forEach(form => {
            if (form.closest(".app-sidebar") || form.closest(".profile-menu")) return;
            form.querySelectorAll("input[required], select[required], textarea[required]").forEach(control => {
                const label = control.id ? form.querySelector(`label[for="${CSS.escape(control.id)}"]`) : null;
                label?.classList.add("required");
                control.setAttribute("aria-required", "true");
            });
            const validationMessages = new Map([...form.querySelectorAll("[data-valmsg-for]")]
                .map(item => [item.dataset.valmsgFor, item]));
            form.querySelectorAll(".form-control, .form-select").forEach(control => {
                const message = validationMessages.get(control.name);
                if (message && control.id) {
                    message.id ||= `${control.id}-feedback`;
                    const descriptions = new Set((control.getAttribute("aria-describedby") || "").split(/\s+/).filter(Boolean));
                    descriptions.add(message.id);
                    control.setAttribute("aria-describedby", [...descriptions].join(" "));
                }
                if (control.classList.contains("input-validation-error")) control.setAttribute("aria-invalid", "true");
                const reflectValidity = () => {
                    if (form.classList.contains("was-validated")) control.setAttribute("aria-invalid", String(!control.validity.valid));
                };
                control.addEventListener("input", reflectValidity);
                control.addEventListener("change", reflectValidity);
            });

            form.querySelectorAll('input[type="password"]').forEach(input => {
                if (input.parentElement?.classList.contains("password-field")) return;
                const wrapper = document.createElement("div");
                wrapper.className = "password-field";
                input.parentNode.insertBefore(wrapper, input);
                wrapper.append(input);
                const button = document.createElement("button");
                button.type = "button";
                button.className = "password-toggle";
                button.setAttribute("aria-label", "Show password");
                button.innerHTML = '<i class="fa-regular fa-eye" aria-hidden="true"></i>';
                button.addEventListener("click", () => {
                    const show = input.type === "password";
                    input.type = show ? "text" : "password";
                    button.setAttribute("aria-label", show ? "Hide password" : "Show password");
                    button.innerHTML = `<i class="fa-regular fa-eye${show ? "-slash" : ""}" aria-hidden="true"></i>`;
                });
                wrapper.append(button);
            });

            form.querySelectorAll("textarea[maxlength], input[maxlength]").forEach(input => {
                if (input.nextElementSibling?.classList.contains("character-counter")) return;
                const counter = document.createElement("small");
                counter.className = "character-counter";
                const refresh = () => counter.textContent = `${input.value.length} / ${input.maxLength}`;
                input.addEventListener("input", refresh);
                refresh();
                input.insertAdjacentElement("afterend", counter);
            });

            form.addEventListener("submit", event => {
                if (!form.checkValidity()) {
                    event.preventDefault();
                    form.classList.add("was-validated");
                    form.querySelectorAll(".form-control, .form-select").forEach(control =>
                        control.setAttribute("aria-invalid", String(!control.validity.valid)));
                    form.querySelector(":invalid")?.focus();
                    showToast("Please review the highlighted fields.", "danger");
                    return;
                }
                const submitter = event.submitter;
                if (submitter && !submitter.dataset.noLoading) {
                    setTimeout(() => {
                        submitter.classList.add("is-loading");
                        submitter.disabled = true;
                    }, 0);
                }
            });
        });

        document.querySelectorAll('input[type="file"]').forEach(input => {
            input.addEventListener("change", () => {
                input.parentElement?.querySelector(".upload-preview")?.remove();
                const file = input.files?.[0];
                if (!file) return;
                const preview = document.createElement("div");
                preview.className = "upload-preview";
                const icon = document.createElement("span");
                icon.className = "activity-icon";
                icon.innerHTML = '<i class="fa-solid fa-file" aria-hidden="true"></i>';
                const text = document.createElement("span");
                text.innerHTML = `<strong>${escapeHtml(file.name)}</strong><small class="d-block text-muted">${formatBytes(file.size)}</small>`;
                preview.append(icon, text);
                if (file.type.startsWith("image/")) {
                    const image = document.createElement("img");
                    image.alt = "Selected image preview";
                    image.src = URL.createObjectURL(file);
                    preview.replaceChild(image, icon);
                }
                input.insertAdjacentElement("afterend", preview);
            });
        });
    }

    function formatBytes(bytes) {
        if (!bytes) return "0 B";
        const units = ["B", "KB", "MB", "GB"];
        const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
        return `${(bytes / Math.pow(1024, index)).toFixed(index ? 1 : 0)} ${units[index]}`;
    }

    function enhanceTables() {
        // Server-paged tables must keep server-side search, paging and export.
        // Client enhancement is opt-in so a page subset is never presented as
        // the complete dataset.
        document.querySelectorAll("table.table[data-client-table]").forEach((table, tableIndex) => {
            if (table.dataset.enhanced === "true") return;
            table.dataset.enhanced = "true";
            table.setAttribute("role", "table");
            table.querySelectorAll("thead th").forEach(th => th.setAttribute("scope", "col"));

            let responsive = table.closest(".table-responsive");
            if (!responsive) {
                responsive = document.createElement("div");
                responsive.className = "table-responsive";
                table.parentNode.insertBefore(responsive, table);
                responsive.append(table);
            }
            const host = responsive.parentElement;
            if (!host) return;
            host.classList.add("table-card");

            const rows = [...table.querySelectorAll("tbody tr")];
            if (!rows.length) {
                const bodyEl = table.tBodies[0] || table.createTBody();
                const row = bodyEl.insertRow();
                const cell = row.insertCell();
                cell.colSpan = Math.max(1, table.querySelectorAll("thead th").length);
                cell.className = "data-table-empty";
                cell.innerHTML = '<div class="empty-state border-0"><span class="empty-state-icon"><i class="fa-solid fa-inbox"></i></span><h3>No records found</h3><p>There is nothing to display yet. Create a record or adjust your filters.</p></div>';
                return;
            }

            const toolbar = document.createElement("div");
            toolbar.className = "table-toolbar";
            toolbar.innerHTML = `
                <div class="table-toolbar-start">
                    <div class="table-search">
                        <i class="fa-solid fa-magnifying-glass" aria-hidden="true"></i>
                        <label class="visually-hidden" for="tableSearch${tableIndex}">Search table</label>
                        <input id="tableSearch${tableIndex}" type="search" placeholder="Search records…" autocomplete="off">
                    </div>
                </div>
                <div class="table-toolbar-end">
                    <label class="visually-hidden" for="pageSize${tableIndex}">Rows per page</label>
                    <select class="form-select" id="pageSize${tableIndex}">
                        <option value="10">10 rows</option>
                        <option value="25">25 rows</option>
                        <option value="50">50 rows</option>
                    </select>
                    <button class="btn btn-sm btn-outline-secondary" type="button" data-export title="Export CSV"><i class="fa-solid fa-download"></i><span class="d-none d-sm-inline ms-1">Export</span></button>
                    <button class="btn btn-sm btn-outline-secondary" type="button" data-print title="Print"><i class="fa-solid fa-print"></i><span class="d-none d-sm-inline ms-1">Print</span></button>
                </div>`;
            host.insertBefore(toolbar, responsive);

            const footer = document.createElement("div");
            footer.className = "table-footer";
            footer.innerHTML = '<span data-table-summary></span><nav aria-label="Table pagination"><ul class="pagination"></ul></nav>';
            host.append(footer);

            const search = toolbar.querySelector("input[type=search]");
            const sizeSelect = toolbar.querySelector("select");
            const pagination = footer.querySelector(".pagination");
            const summary = footer.querySelector("[data-table-summary]");
            let currentPage = 1;

            function visibleRows() {
                const query = search.value.trim().toLowerCase();
                return rows.filter(row => row.textContent.toLowerCase().includes(query));
            }

            function render() {
                const matches = visibleRows();
                const pageSize = Number(sizeSelect.value);
                const pageCount = Math.max(1, Math.ceil(matches.length / pageSize));
                currentPage = Math.min(currentPage, pageCount);
                const start = (currentPage - 1) * pageSize;
                rows.forEach(row => row.hidden = true);
                matches.slice(start, start + pageSize).forEach(row => row.hidden = false);
                summary.textContent = matches.length
                    ? `Showing ${start + 1}–${Math.min(start + pageSize, matches.length)} of ${matches.length}`
                    : "No matching records";
                pagination.innerHTML = `
                    <li class="page-item ${currentPage === 1 ? "disabled" : ""}"><button class="page-link" data-page="${currentPage - 1}" aria-label="Previous page"><i class="fa-solid fa-chevron-left"></i></button></li>
                    ${Array.from({ length: Math.min(5, pageCount) }, (_, index) => {
                        let page = pageCount <= 5 ? index + 1 : Math.max(1, Math.min(currentPage - 2, pageCount - 4)) + index;
                        return `<li class="page-item ${page === currentPage ? "active" : ""}"><button class="page-link" data-page="${page}">${page}</button></li>`;
                    }).join("")}
                    <li class="page-item ${currentPage === pageCount ? "disabled" : ""}"><button class="page-link" data-page="${currentPage + 1}" aria-label="Next page"><i class="fa-solid fa-chevron-right"></i></button></li>`;
            }

            let searchTimer;
            search.addEventListener("input", () => {
                clearTimeout(searchTimer);
                searchTimer = setTimeout(() => { currentPage = 1; render(); }, 180);
            });
            sizeSelect.addEventListener("change", () => { currentPage = 1; render(); });
            pagination.addEventListener("click", event => {
                const button = event.target.closest("[data-page]");
                if (!button || button.closest(".disabled")) return;
                currentPage = Number(button.dataset.page);
                render();
            });
            toolbar.querySelector("[data-print]").addEventListener("click", () => print());
            toolbar.querySelector("[data-export]").addEventListener("click", () => exportTable(table, visibleRows()));
            render();
        });
    }

    function exportTable(table, rows) {
        const headers = [...table.querySelectorAll("thead th")].map(cell => csvEscape(cell.textContent.trim()));
        const bodyRows = rows.map(row => [...row.cells].map(cell => csvEscape(cell.textContent.trim())).join(","));
        const blob = new Blob([[headers.join(","), ...bodyRows].join("\n")], { type: "text/csv;charset=utf-8" });
        const link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.download = `${document.title.split("·")[0].trim().replace(/\s+/g, "-").toLowerCase()}-${new Date().toISOString().slice(0, 10)}.csv`;
        link.click();
        URL.revokeObjectURL(link.href);
    }

    function csvEscape(value) {
        return `"${String(value).replace(/"/g, '""')}"`;
    }

    function setupConfirmations() {
        const modalElement = document.getElementById("confirmDialog");
        if (!modalElement || !window.bootstrap) return;
        const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
        const message = document.getElementById("confirmDialogMessage");
        const action = document.getElementById("confirmDialogAction");
        let callback = null;

        document.addEventListener("click", event => {
            const trigger = event.target.closest("[data-confirm], .btn-danger");
            if (!trigger || trigger.closest("#confirmDialog") || trigger.type === "button" && !trigger.dataset.confirm) return;
            const form = trigger.closest("form");
            const href = trigger.matches("a[href]") ? trigger.href : null;
            if (!form && !href) return;
            event.preventDefault();
            message.textContent = trigger.dataset.confirm || "Please confirm. This action may change or remove existing information.";
            callback = () => {
                modal.hide();
                if (form) {
                    trigger.dataset.noLoading = "true";
                    form.requestSubmit(trigger);
                } else if (href) location.href = href;
            };
            modal.show();
        });
        action.addEventListener("click", () => callback?.());
    }

    function showToast(message, type = "success") {
        const container = document.getElementById("toastContainer");
        if (!container || !window.bootstrap || !message) return;
        const icons = { success: "circle-check", danger: "circle-exclamation", warning: "triangle-exclamation", info: "circle-info" };
        const title = { success: "Success", danger: "Action required", warning: "Attention", info: "Information" };
        const element = document.createElement("div");
        element.className = "toast";
        element.dataset.tone = type;
        element.setAttribute("role", "status");
        element.innerHTML = `
            <div class="toast-header">
                <i class="fa-solid fa-${icons[type] || icons.info} text-${type} me-2"></i>
                <strong class="me-auto">${title[type] || title.info}</strong>
                <button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
            <div class="toast-body">${escapeHtml(message)}</div>`;
        container.append(element);
        const toast = new bootstrap.Toast(element, { delay: 4500 });
        element.addEventListener("hidden.bs.toast", () => element.remove());
        toast.show();
    }

    document.querySelectorAll(".app-toast-source").forEach(source => showToast(source.dataset.toastMessage, source.dataset.toastType));
    document.querySelectorAll(".alert").forEach(alert => {
        if (!alert.querySelector("i")) {
            const icon = document.createElement("i");
            icon.className = `fa-solid ${alert.classList.contains("alert-danger") ? "fa-circle-exclamation" : alert.classList.contains("alert-warning") ? "fa-triangle-exclamation" : alert.classList.contains("alert-info") ? "fa-circle-info" : "fa-circle-check"}`;
            icon.setAttribute("aria-hidden", "true");
            alert.prepend(icon);
        }
    });

    function setupPageLoading() {
        document.addEventListener("click", event => {
            const link = event.target.closest("a[href]");
            if (!link || event.defaultPrevented || event.ctrlKey || event.metaKey || link.target === "_blank" || link.hasAttribute("download")) return;
            const url = new URL(link.href, location.href);
            if (url.origin !== location.origin || url.hash && url.pathname === location.pathname) return;
            loader?.classList.add("active");
        });
        addEventListener("pageshow", () => loader?.classList.remove("active"));
    }

    function setupDependentSelects() {
        document.querySelectorAll("select[data-dependent-parent][data-dependent-url]").forEach(select => {
            const parent = document.querySelector(select.dataset.dependentParent);
            if (!parent) return;
            let requestController = null;
            parent.addEventListener("change", async () => {
                requestController?.abort();
                const parentValue = parent.value;
                const placeholder = select.dataset.placeholder || "Select an option";
                select.innerHTML = `<option value="">${escapeHtml(parentValue ? "Loading…" : placeholder)}</option>`;
                select.disabled = !parentValue;
                if (!parentValue) return;
                requestController = new AbortController();
                select.setAttribute("aria-busy", "true");
                try {
                    const url = select.dataset.dependentUrl.replace("{value}", encodeURIComponent(parentValue));
                    const response = await fetch(url, {
                        signal: requestController.signal,
                        headers: { "X-Requested-With": "XMLHttpRequest" }
                    });
                    if (!response.ok) throw new Error("Lookup request failed");
                    const options = await response.json();
                    select.innerHTML = `<option value="">${escapeHtml(options.length ? placeholder : "No options available")}</option>` +
                        options.map(option => `<option value="${escapeHtml(option.value)}">${escapeHtml(option.text)}</option>`).join("");
                    select.disabled = options.length === 0;
                } catch (error) {
                    if (error.name === "AbortError") return;
                    select.innerHTML = '<option value="">Unable to load options</option>';
                    select.disabled = true;
                    showToast("Related options could not be loaded. Please try again.", "danger");
                } finally {
                    select.removeAttribute("aria-busy");
                }
            });
        });
    }

    function setupEditForms() {
        document.querySelectorAll("form[data-track-changes]").forEach(form => {
            const initial = new FormData(form);
            const snapshot = () => [...new FormData(form).entries()]
                .map(([key, value]) => `${key}=${value instanceof File ? value.name : value}`)
                .sort().join("&");
            const initialSnapshot = [...initial.entries()]
                .map(([key, value]) => `${key}=${value instanceof File ? value.name : value}`)
                .sort().join("&");
            let dirty = false;
            let submitted = false;
            const saveButton = form.querySelector('[type="submit"]');
            const refresh = () => {
                dirty = snapshot() !== initialSnapshot;
                form.dataset.dirty = String(dirty);
                if (saveButton && !saveButton.hasAttribute("data-always-enabled"))
                    saveButton.disabled = !dirty;
            };
            form.addEventListener("input", refresh);
            form.addEventListener("change", refresh);
            form.addEventListener("reset", () => setTimeout(refresh));
            form.addEventListener("submit", () => { submitted = true; });
            refresh();

            addEventListener("beforeunload", event => {
                if (!dirty || submitted) return;
                event.preventDefault();
                event.returnValue = "";
            });

            document.addEventListener("click", event => {
                const link = event.target.closest("a[href]");
                if (!link || !dirty || submitted || link.target === "_blank") return;
                const url = new URL(link.href, location.href);
                if (url.origin !== location.origin) return;
                event.preventDefault();
                event.stopImmediatePropagation();
                const modalElement = document.getElementById("confirmDialog");
                if (!modalElement || !window.bootstrap) return;
                document.getElementById("confirmDialogMessage").textContent =
                    "You have unsaved changes. Leave this page and discard them?";
                const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
                const action = document.getElementById("confirmDialogAction");
                const leave = () => {
                    submitted = true;
                    action.removeEventListener("click", leave);
                    modal.hide();
                    location.href = url.href;
                };
                action.addEventListener("click", leave, { once: true });
                modal.show();
            }, true);
        });
    }

    function setupDetailsTabs() {
        document.querySelectorAll(".details-tabs").forEach(tabList => {
            tabList.setAttribute("role", "tablist");
            const links = [...tabList.querySelectorAll('a[href^="#"]')];
            links.forEach(link => {
                link.setAttribute("role", "tab");
                link.setAttribute("aria-controls", link.hash.slice(1));
            });
            const activate = id => links.forEach(link => {
                const selected = link.hash === `#${id}`;
                link.classList.toggle("active", selected);
                link.setAttribute("aria-selected", String(selected));
                link.tabIndex = selected ? 0 : -1;
            });
            const requested = new URLSearchParams(location.search).get("tab") || location.hash.slice(1);
            if (requested && document.getElementById(requested)) {
                activate(requested);
                requestAnimationFrame(() => document.getElementById(requested)?.scrollIntoView({ block: "start" }));
            } else if (links[0]) {
                activate(links[0].hash.slice(1));
            }
            links.forEach((link, index) => {
                link.addEventListener("click", () => {
                    const id = link.hash.slice(1);
                    activate(id);
                    const url = new URL(location.href);
                    url.searchParams.set("tab", id);
                    url.hash = id;
                    history.replaceState(null, "", url);
                });
                link.addEventListener("keydown", event => {
                    if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
                    event.preventDefault();
                    const next = event.key === "Home" ? 0 : event.key === "End" ? links.length - 1 :
                        (index + (event.key === "ArrowRight" ? 1 : -1) + links.length) % links.length;
                    links[next].focus();
                    links[next].click();
                });
            });
        });
    }

    function setupCopyActions() {
        document.querySelectorAll("[data-copy-value]").forEach(button => {
            button.addEventListener("click", async () => {
                const value = button.dataset.copyValue || "";
                const label = button.dataset.copyLabel || "Value";
                if (!value) return;
                try {
                    await navigator.clipboard.writeText(value);
                    showToast(`${label} copied.`, "success");
                } catch {
                    const field = document.createElement("textarea");
                    field.value = value;
                    field.setAttribute("readonly", "");
                    field.style.position = "fixed";
                    field.style.opacity = "0";
                    document.body.append(field);
                    field.select();
                    const copied = document.execCommand("copy");
                    field.remove();
                    showToast(copied ? `${label} copied.` : `Could not copy ${label.toLowerCase()}.`,
                        copied ? "success" : "danger");
                }
            });
        });
    }

    function arrangeMetricPageActions() {
        const routeContent = document.querySelector(".route-content");
        const pageHeader = routeContent?.querySelector(":scope > .page-header");
        if (!routeContent || !pageHeader) return;

        const metricGrid = pageHeader.nextElementSibling;
        if (!metricGrid?.classList.contains("metric-grid")) return;

        // Keep the heading, context and actions together above the statistics.
        const actionBar = pageHeader.querySelector(".page-actions") || document.createElement("div");
        actionBar.classList.add("page-actions");
        actionBar.setAttribute("aria-label", "Page actions");
        if (!actionBar.parentElement) pageHeader.append(actionBar);

        if (!actionBar.querySelector("[data-reload-page]")) {
            const refreshButton = document.createElement("button");
            refreshButton.type = "button";
            refreshButton.className = "btn btn-outline-secondary";
            refreshButton.dataset.reloadPage = "";
            refreshButton.innerHTML = '<i class="fa-solid fa-rotate-right me-1" aria-hidden="true"></i> Refresh';
            actionBar.append(refreshButton);
        }
    }

    arrangeMetricPageActions();
    enhanceForms();
    enhanceTables();
    // Keep wide data tables reachable by keyboard on touch-sized layouts.
    document.querySelectorAll(".table-responsive").forEach(region => {
        region.tabIndex = 0;
        region.setAttribute("role", "region");
        region.setAttribute("aria-label", "Data table. Scroll horizontally to see all columns.");
        region.querySelectorAll("thead th:not([scope])").forEach(th => th.setAttribute("scope", "col"));
        region.querySelectorAll("table.table").forEach(table => {
            if (!table.tHead || !table.tBodies.length || [...table.tBodies].some(body => body.rows.length)) return;
            const cell = table.tBodies[0].insertRow().insertCell();
            cell.colSpan = Math.max(1, table.tHead.rows[0]?.cells.length || 1);
            cell.className = "data-table-empty";
            cell.innerHTML = '<div class="empty-state compact border-0"><span class="empty-state-icon"><i class="fa-solid fa-inbox" aria-hidden="true"></i></span><h3>No records to display</h3><p>Records will appear here when available.</p></div>';
        });
    });
    setupConfirmations();
    setupDependentSelects();
    setupEditForms();
    setupDetailsTabs();
    setupCopyActions();
    setupPageLoading();
    document.querySelectorAll("[data-print-page]").forEach(button =>
        button.addEventListener("click", () => window.print()));
    document.querySelectorAll("[data-reload-page]").forEach(button =>
        button.addEventListener("click", () => window.location.reload()));
    document.querySelectorAll("[data-history-back]").forEach(button =>
        button.addEventListener("click", event => {
            event.preventDefault();
            const fallbackUrl = button.dataset.fallbackUrl || "/";
            const hasSameOriginReferrer = document.referrer &&
                new URL(document.referrer, window.location.href).origin === window.location.origin;
            if (window.history.length > 1 && hasSameOriginReferrer) window.history.back();
            else window.location.assign(fallbackUrl);
        }));
    document.querySelectorAll("[data-submit-on-change]").forEach(control =>
        control.addEventListener("change", () => control.form?.requestSubmit()));

    if (!reduceMotion) {
        document.querySelectorAll(".metric-card, .section-card").forEach((card, index) => {
            card.animate(
                [{ opacity: 0, transform: "translateY(8px)" }, { opacity: 1, transform: "translateY(0)" }],
                { duration: 260, delay: Math.min(index * 35, 280), easing: "ease-out", fill: "both" }
            );
        });
    }

    window.smsUi = { showToast };
})();
