(() => {
    const root = document.querySelector('[data-history-directory]');
    if (!root) return;
    root.querySelectorAll('.df-device-type-icon').forEach(icon => {
        const fallback = icon.nextElementSibling;
        const showFallback = () => {
            icon.hidden = true;
            if (fallback?.matches('.df-device-type-fallback')) fallback.hidden = false;
        };
        icon.addEventListener('error', showFallback, { once: true });
        if (icon.complete && icon.naturalWidth === 0) showFallback();
    });
    const form = root.querySelector(root.dataset.directoryForm || '#commissioningFilters');
    const panel = root.querySelector(root.dataset.directoryPanel || '#commissioningFiltersPanel');
    const toggle = root.querySelector('[data-history-filter-toggle]');
    const popover = root.querySelector('[data-history-column-popover]');
    if (!form || !panel || !popover) return;
    const body = popover.querySelector('[data-history-column-body]');
    let currentButton = null;
    let movedFields = [];

    const columns = [
        ['customer', ['musteri']], ['installation', ['tesisat']], ['contract', ['sozlesme']],
        ['company', ['servis']], ['type', ['cihazTipi']], ['device', ['marka']],
        ['date', ['bas', 'bit']], ['status', ['durum']]
    ].map(([column, ids]) => ({ column, header: root.querySelector(`th.df-commissioning-col-${column}`),
        controls: ids.map(id => root.querySelector(`#${id}`)) }));
    root.querySelectorAll('th[data-directory-fields]').forEach(header => columns.push({
        column: header.dataset.directoryColumn,
        header,
        controls: header.dataset.directoryFields.split(' ').map(name => form.querySelector(`[name="${name}"]`))
    }));
    form.querySelectorAll('input, select').forEach(control => {
        control.setAttribute('form', form.id);
        const label = control.closest('.df-admin-field,.ykc-filter-field')?.querySelector('label');
        if (label && !label.htmlFor) {
            control.id ||= `${form.id}-${control.name}`;
            label.htmlFor = control.id;
        }
    });
    root.classList.add('is-enhanced');
    toggle.hidden = false;
    root.querySelector('[data-history-view-group]').hidden = false;

    function closeColumnFilter(restoreFocus = false) {
        if (!currentButton) return;
        const button = currentButton;
        movedFields.forEach(({ field, marker }) => marker.replaceWith(field));
        movedFields = [];
        popover.hidden = true;
        button.setAttribute('aria-expanded', 'false');
        currentButton = null;
        if (restoreFocus) button.focus({ preventScroll: true });
    }

    function placeColumnFilter() {
        const anchor = currentButton.getBoundingClientRect();
        const bounds = popover.getBoundingClientRect();
        const left = Math.max(12, Math.min(anchor.left, window.innerWidth - bounds.width - 12));
        const top = anchor.bottom + bounds.height + 8 <= window.innerHeight
            ? anchor.bottom + 8 : Math.max(12, anchor.top - bounds.height - 8);
        popover.style.left = `${left}px`;
        popover.style.top = `${top}px`;
    }

    let activeColumns = 0;
    columns.forEach(({column, header, controls}) => {
        if (!header) return;
        if (controls.some(control => !control)) return;
        const name = header.textContent.trim();
        const label = document.createElement('span');
        label.textContent = name;
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'ys-history-column-trigger';
        button.title = `${name} filtresi`;
        button.setAttribute('aria-label', button.title);
        button.setAttribute('aria-expanded', 'false');
        button.setAttribute('aria-controls', popover.id);
        button.dataset.historyColumn = column;
        const active = controls.some(control => control.value.trim());
        button.classList.toggle('is-filtered', active);
        if (active) activeColumns++;
        const icon = document.createElement('i');
        icon.className = active ? 'bi bi-funnel-fill' : 'bi bi-funnel';
        icon.setAttribute('aria-hidden', 'true');
        button.append(icon);
        const heading = document.createElement('div');
        heading.className = 'ys-history-column-heading';
        heading.append(label, button);
        header.replaceChildren(heading);
        button.addEventListener('click', () => {
            const alreadyOpen = currentButton === button;
            closeColumnFilter();
            if (alreadyOpen) return;
            currentButton = button;
            button.setAttribute('aria-expanded', 'true');
            popover.querySelector('strong').textContent = name;
            controls.forEach(control => {
                const field = control.closest('.df-admin-field,.ykc-filter-field');
                if (movedFields.some(item => item.field === field)) return;
                const marker = document.createComment('column-filter-position');
                field.before(marker);
                body.append(field);
                movedFields.push({ field, marker });
            });
            popover.hidden = false;
            placeColumnFilter();
            controls[0].focus({ preventScroll: true });
        });
    });

    const count = root.querySelector('[data-history-filter-count]');
    count.hidden = activeColumns === 0;
    count.textContent = String(activeColumns);
    const syncPanel = () => toggle.setAttribute('aria-expanded', String(panel.open));
    panel.addEventListener('toggle', syncPanel);
    syncPanel();
    toggle.addEventListener('click', () => { closeColumnFilter(); panel.open = !panel.open; syncPanel(); });
    popover.querySelector('[data-history-column-close]').addEventListener('click', () => closeColumnFilter(true));
    popover.querySelector('[data-history-column-clear]').addEventListener('click', () => {
        body.querySelectorAll('input, select').forEach(control => { control.value = ''; });
        form.requestSubmit();
    });
    const menus = Array.from(root.querySelectorAll('.ys-history-menu'));
    menus.forEach(menu => menu.addEventListener('toggle', () => {
        if (!menu.open) return;
        menus.filter(other => other !== menu).forEach(other => { other.open = false; });
        const content = menu.querySelector('.ys-history-menu-body');
        content.style.transform = '';
        const bounds = content.getBoundingClientRect();
        const viewportWidth = document.documentElement.clientWidth;
        const offset = bounds.left < 12 ? 12 - bounds.left : Math.min(0, viewportWidth - 12 - bounds.right);
        if (offset) content.style.transform = `translateX(${offset}px)`;
    }));
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        if (currentButton) { event.preventDefault(); closeColumnFilter(true); }
        menus.filter(menu => menu.open).forEach(menu => {
            event.preventDefault();
            menu.open = false;
            menu.querySelector('summary').focus({ preventScroll: true });
        });
    });
    document.addEventListener('click', event => {
        if (currentButton && !popover.contains(event.target) && !currentButton.contains(event.target)) closeColumnFilter();
        menus.filter(menu => menu.open).forEach(menu => {
            if (!menu.contains(event.target)) menu.open = false;
        });
    });
    window.addEventListener('resize', () => {
        closeColumnFilter();
        menus.forEach(menu => { menu.open = false; });
    });
    document.addEventListener('scroll', event => {
        if (!popover.contains(event.target)) closeColumnFilter();
    }, true);

    const viewStorageKey = `${root.dataset.storageKey || 'commissioning-view'}:view`;
    function setView(value, notify = false) {
        const previous = root.dataset.view;
        root.dataset.view = value === 'cards' ? 'cards' : 'list';
        root.querySelectorAll('[data-history-view]').forEach(button => {
            button.setAttribute('aria-pressed', String(button.dataset.historyView === root.dataset.view));
        });
        if (notify && previous !== root.dataset.view) {
            closeColumnFilter();
            root.dispatchEvent(new CustomEvent('commissioning:viewchange', { detail: { view: root.dataset.view } }));
        }
    }
    try { setView(localStorage.getItem(viewStorageKey)); }
    catch { setView('list'); }
    root.querySelectorAll('[data-history-view]').forEach(button => button.addEventListener('click', () => {
        setView(button.dataset.historyView, true);
        try { localStorage.setItem(viewStorageKey, root.dataset.view); } catch { /* Storage can be unavailable. */ }
    }));

    const records = Array.from(root.querySelectorAll('[data-commissioning-record],[data-directory-record]'));
    const selections = Array.from(root.querySelectorAll('[data-commissioning-select],[data-directory-select]'));
    const selectAll = root.querySelector('[data-commissioning-select-all],[data-directory-select-all]');
    const selectionCount = root.querySelector('[data-directory-selection-count]');
    function syncSelection() {
        if (!selectAll) return;
        const selectedCount = selections.filter(checkbox => checkbox.checked).length;
        if (selectionCount) {
            selectionCount.hidden = selectedCount === 0;
            selectionCount.textContent = `${selectedCount} seçili`;
        }
        selectAll.disabled = selections.length === 0;
        selectAll.checked = selections.length > 0 && selectedCount === selections.length;
        selectAll.indeterminate = selectedCount > 0 && selectedCount < selections.length;
    }
    selections.forEach(checkbox => checkbox.addEventListener('change', syncSelection));
    selectAll?.addEventListener('change', () => {
        selections.forEach(checkbox => { checkbox.checked = selectAll.checked; });
        syncSelection();
    });
    syncSelection();
    const exportForm = root.querySelector('[data-directory-export-form]');
    const exportFields = exportForm?.querySelector('[data-directory-export-fields]');
    root.querySelectorAll('[data-commissioning-bulk-export],[data-directory-export]').forEach(button => button.addEventListener('click', () => {
        const selected = records.filter(row => row.querySelector('[data-commissioning-select],[data-directory-select]')?.checked);
        const exportRecords = selected.length ? selected : root.hasAttribute('data-directory-export-all') ? [] : records;
        const ids = [...new Set(exportRecords.map(row => row.dataset.exportId).filter(Boolean))];
        if (!records.length || !exportForm || !exportFields) return;
        const url = new URL(button.dataset.exportUrl, window.location.origin);
        url.searchParams.delete('ids');
        ids.forEach(id => url.searchParams.append('ids', id));
        exportFields.replaceChildren();
        url.searchParams.forEach((value, name) => {
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = name;
            input.value = value;
            exportFields.append(input);
        });
        url.search = '';
        exportForm.action = url.toString();
        exportForm.requestSubmit();
    }));
    records.filter(row => row.hasAttribute('data-directory-record')).forEach(row => {
        row.addEventListener('click', event => {
            if (root.dataset.view !== 'cards' || event.target.closest('a,button,input,select,label')) return;
            row.querySelector('[data-directory-open]')?.click();
        });
    });
})();
