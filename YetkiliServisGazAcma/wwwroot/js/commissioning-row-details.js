(() => {
    const records = Array.from(document.querySelectorAll("[data-commissioning-record]"));
    if (!records.length) return;
    const interactive = "a, button, input, select, textarea, label, form, details, summary, [contenteditable], [role='button'], [role='link']";

    const modelFields = records.flatMap(record => Array.from(record.querySelectorAll('.df-device-model dd')));
    const modelTooltip = document.createElement('div');
    modelTooltip.className = 'df-commissioning-model-tooltip';
    modelTooltip.id = 'dfCommissioningModelTooltip';
    modelTooltip.setAttribute('role', 'tooltip');
    modelTooltip.hidden = true;
    let activeModel = null, hideTimer = 0;

    const hideModelTooltip = () => {
        window.clearTimeout(hideTimer);
        if (activeModel) {
            const descriptions = (activeModel.getAttribute('aria-describedby') || '').split(/\s+/)
                .filter(id => id && id !== modelTooltip.id);
            if (descriptions.length) activeModel.setAttribute('aria-describedby', descriptions.join(' '));
            else activeModel.removeAttribute('aria-describedby');
        }
        activeModel = null;
        modelTooltip.hidden = true;
    };

    const showModelTooltip = field => {
        hideModelTooltip();
        if (field.scrollWidth <= field.clientWidth + 1) return;
        activeModel = field;
        modelTooltip.textContent = field.textContent.trim();
        modelTooltip.hidden = false;
        field.setAttribute('aria-describedby', [field.getAttribute('aria-describedby'), modelTooltip.id].filter(Boolean).join(' '));
        const anchor = field.getBoundingClientRect(), box = modelTooltip.getBoundingClientRect();
        modelTooltip.style.left = Math.max(12, Math.min(anchor.left, window.innerWidth - box.width - 12)) + 'px';
        const below = anchor.bottom + 8;
        modelTooltip.style.top = Math.max(12, below + box.height <= window.innerHeight - 12
            ? below : anchor.top - box.height - 8) + 'px';
    };

    const refreshModelFields = () => {
        hideModelTooltip();
        modelFields.forEach(field => {
            const truncated = field.scrollWidth > field.clientWidth + 1;
            field.classList.toggle('is-truncated', truncated);
            if (truncated) field.tabIndex = 0;
            else field.removeAttribute('tabindex');
        });
    };

    if (modelFields.length) {
        document.body.append(modelTooltip);
        modelFields.forEach(field => {
            field.removeAttribute('title');
            field.addEventListener('pointerenter', event => {
                if (event.pointerType !== 'touch') showModelTooltip(field);
            });
            field.addEventListener('pointerleave', () => {
                if (document.activeElement !== field) hideTimer = window.setTimeout(hideModelTooltip, 120);
            });
            field.addEventListener('focus', () => showModelTooltip(field));
            field.addEventListener('blur', hideModelTooltip);
        });
        modelTooltip.addEventListener('pointerenter', () => window.clearTimeout(hideTimer));
        modelTooltip.addEventListener('pointerleave', () => {
            if (document.activeElement !== activeModel) hideModelTooltip();
        });
        document.addEventListener('keydown', event => {
            if (event.key === 'Escape') hideModelTooltip();
        });
        document.addEventListener('scroll', event => {
            if (event.target !== modelTooltip) hideModelTooltip();
        }, true);
        window.addEventListener('resize', refreshModelFields);
        if (window.ResizeObserver) {
            const modelResizeObserver = new ResizeObserver(refreshModelFields);
            modelFields.forEach(field => modelResizeObserver.observe(field));
        }
        refreshModelFields();
        document.fonts?.ready.then(refreshModelFields).catch(error => console.warn('Model tooltip font refresh failed.', error));
    }

    const setRecordState = (record, expanded) => {
        const toggle = record.querySelector("[data-commissioning-expand]");
        const detail = toggle ? document.getElementById(toggle.getAttribute("aria-controls")) : null;
        if (!toggle || !detail) return;

        hideModelTooltip();
        record.classList.toggle("is-expanded", expanded);
        detail.hidden = !expanded;
        toggle.setAttribute("aria-expanded", String(expanded));
        toggle.setAttribute("title", expanded ? "İşlem ayrıntılarını kapat" : "İşlem ayrıntılarını görüntüle");
        if (!toggle.dataset.recordLabel) {
            toggle.dataset.recordLabel = (toggle.getAttribute("aria-label") || "")
                .replace(/işlem ayrıntılarını (görüntüle|kapat)$/i, "")
                .trim();
        }
        const action = expanded ? "kapat" : "görüntüle";
        toggle.setAttribute("aria-label", toggle.dataset.recordLabel
            ? `${toggle.dataset.recordLabel} işlem ayrıntılarını ${action}`
            : `İşlem ayrıntılarını ${action}`);

        const icon = toggle.querySelector("i");
        if (icon) {
            icon.classList.toggle("bi-eye", !expanded);
            icon.classList.toggle("bi-eye-slash", expanded);
        }
    };

    records.forEach(record => {
        record.addEventListener("click", event => {
            const target = event.target;
            if (!(target instanceof Element)) return;
            if (target.closest(interactive) && !target.closest("[data-commissioning-expand]")) return;

            const willOpen = !record.classList.contains("is-expanded");
            records.forEach(other => setRecordState(other, false));
            setRecordState(record, willOpen);
        });
    });

    const linkedDetail = document.getElementById(window.location.hash.slice(1));
    if (linkedDetail?.matches(".df-commissioning-detail-row")) {
        const record = records.find(item => item.querySelector("[data-commissioning-expand]")?.getAttribute("aria-controls") === linkedDetail.id);
        if (record) {
            setRecordState(record, true);
            record.scrollIntoView({ block: "start" });
        }
    }
})();
