(() => {
    "use strict";

    const sheet = document.querySelector("[data-branch-sheet]");
    const body = sheet?.querySelector("[data-branch-sheet-body]");
    const title = sheet?.querySelector("[data-branch-sheet-title]");
    const addTemplate = document.querySelector("[data-branch-add-template]");
    if (!sheet || !body || !title || !addTemplate) return;
    let requestId = 0;
    let closeTimer = 0;
    let activeTrigger = null;

    function focusField() {
        body.querySelector("select:not(:disabled), input:not([type='hidden']):not(:disabled), textarea:not(:disabled)")?.focus();
    }

    function closeSheet() {
        requestId++;
        sheet.classList.remove("is-visible");
        document.documentElement.classList.remove("df-sheet-open");
        window.clearTimeout(closeTimer);
        closeTimer = window.setTimeout(() => {
            if (sheet.open) sheet.close();
            body.replaceChildren();
            activeTrigger?.focus();
        }, 180);
    }

    function showSheet(trigger) {
        window.clearTimeout(closeTimer);
        activeTrigger = trigger;
        if (!sheet.open) sheet.showModal();
        document.documentElement.classList.add("df-sheet-open");
        const openingRequest = requestId;
        requestAnimationFrame(() => { if (sheet.open && openingRequest === requestId) sheet.classList.add("is-visible"); });
    }

    document.querySelector("[data-branch-add]")?.addEventListener("click", event => {
        requestId++;
        title.textContent = "Yeni Şube Ekle";
        body.replaceChildren(addTemplate.content.cloneNode(true));
        showSheet(event.currentTarget);
        focusField();
    });

    async function openSheet(trigger) {
        const currentRequest = ++requestId;
        title.textContent = "Şubeyi Düzenle";
        body.innerHTML = '<div class="df-directory-sheet-loading" role="status">Form hazırlanıyor</div>';
        showSheet(trigger);
        try {
            const response = await fetch(trigger.href, { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (currentRequest !== requestId) return;
            if (response.redirected) {
                const redirect = new URL(response.url, window.location.href);
                if (redirect.origin === window.location.origin
                    && (redirect.pathname === "/giris" || redirect.pathname === "/yetkisiz-erisim")) {
                    window.location.assign(redirect.href);
                    return;
                }
                throw new Error("Unexpected redirect");
            }
            if (!response.ok) throw new Error("Form unavailable");
            const parsed = new DOMParser().parseFromString(await response.text(), "text/html");
            const form = parsed.querySelector(".ys-branch-editor");
            if (!form) throw new Error("Form missing");
            if (currentRequest !== requestId) return;
            body.replaceChildren(document.importNode(form, true));
            focusField();
        } catch {
            if (currentRequest === requestId) {
                body.innerHTML = '<div class="df-directory-sheet-error" role="alert"><strong>Şube formu açılamadı.</strong><p>Bağlantıyı kontrol edip yeniden deneyin.</p><button type="button" class="df-btn df-btn-secondary" data-branch-sheet-retry><i class="bi bi-arrow-clockwise" aria-hidden="true"></i> Yeniden Dene</button></div>';
            }
        }
    }

    document.querySelectorAll(".js-branch-sheet-trigger").forEach(trigger => {
        trigger.addEventListener("click", event => {
            if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            openSheet(trigger);
        });
    });
    sheet.querySelector("[data-branch-sheet-close]")?.addEventListener("click", closeSheet);
    sheet.addEventListener("cancel", event => { event.preventDefault(); closeSheet(); });
    sheet.addEventListener("click", event => { if (event.target === sheet) closeSheet(); });
    body.addEventListener("click", event => {
        if (event.target.closest("[data-branch-sheet-cancel]")) closeSheet();
        if (event.target.closest("[data-branch-sheet-retry]") && activeTrigger) openSheet(activeTrigger);
    });

    const params = new URLSearchParams(window.location.search);
    const editId = params.get("duzenle");
    if (editId && /^\d+$/.test(editId)) {
        const trigger = Array.from(document.querySelectorAll(".js-branch-sheet-trigger"))
            .find(item => item.dataset.branchId === editId);
        params.delete("duzenle");
        history.replaceState(history.state, document.title, window.location.pathname + (params.size ? `?${params}` : "") + window.location.hash);
        if (trigger) openSheet(trigger);
    }
})();
