(() => {
    const interactive = "a, button, input, select, textarea, label, form, details, summary, [contenteditable], [role='button'], [role='link']";

    function setExpanded(row, expanded) {
        const detail = row.nextElementSibling;
        const toggle = row.querySelector('[data-approval-expand]');
        if (!detail?.matches('[data-approval-detail]') || !toggle) return;

        row.classList.toggle('is-expanded', expanded);
        detail.hidden = !expanded || row.hidden || getComputedStyle(row).display === 'none';
        toggle.setAttribute('aria-expanded', String(expanded));
        toggle.setAttribute('aria-label', `${row.dataset.firma || 'Belge'} ayrıntılarını ${expanded ? 'kapat' : 'aç'}`);
        toggle.title = expanded ? 'Satır ayrıntılarını kapat' : 'Satır ayrıntılarını aç';
    }

    document.querySelectorAll('.df-approval-table.is-compact-approval').forEach(table => {
        table.addEventListener('click', event => {
            const target = event.target;
            if (!(target instanceof Element)) return;
            const row = target.closest('tr[data-approval-row]');
            if (!row || !table.contains(row)) return;
            if (target.closest(interactive) && !target.closest('[data-approval-expand]')) return;

            const expanded = !row.classList.contains('is-expanded');
            table.querySelectorAll('tr[data-approval-row].is-expanded').forEach(other => {
                if (other !== row) setExpanded(other, false);
            });
            setExpanded(row, expanded);
        });
    });
})();
