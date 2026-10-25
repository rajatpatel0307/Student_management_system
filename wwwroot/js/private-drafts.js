(() => {
    "use strict";
    const owner = document.body.dataset.draftUser;
    const prefix = "eduflow-draft:";
    const key = scope => `${prefix}${owner}:${scope}`;
    const maxAge = 4 * 60 * 60 * 1000;
    // Retire the old unscoped, persistent drafts; never restore another account's
    // student data on a shared device. Preferences and unrelated storage survive.
    try {
        for (const name of Object.keys(localStorage)) {
            if (/^(marks|attendance):/.test(name)) localStorage.removeItem(name);
        }
    } catch { /* Storage may be disabled. */ }
    function clear(scope) {
        try { sessionStorage.removeItem(key(scope)); } catch { /* Optional storage. */ }
    }
    if (owner && document.body.dataset.clearDraft) clear(document.body.dataset.clearDraft);
    window.EduFlowDrafts = Object.freeze({
        save(scope, roster, values) {
            if (!owner) return false;
            try {
                sessionStorage.setItem(key(scope), JSON.stringify({ roster, values, savedAt: Date.now() }));
                return true;
            } catch { return false; }
        },
        load(scope, roster) {
            if (!owner) return null;
            try {
                const draft = JSON.parse(sessionStorage.getItem(key(scope)));
                if (!draft) return null;
                if (draft.roster !== roster || !Number.isFinite(draft.savedAt) || Date.now() - draft.savedAt > maxAge || draft.savedAt > Date.now()) {
                    clear(scope); return null;
                }
                return draft.values;
            } catch { clear(scope); return null; }
        },
        clear
    });
    document.querySelectorAll('form[action$="/Logout"]').forEach(form => form.addEventListener("submit", () => {
        try {
            for (const name of Object.keys(sessionStorage)) {
                if (name.startsWith(prefix)) sessionStorage.removeItem(name);
            }
        } catch { /* Logout must still work without browser storage. */ }
    }));
})();
