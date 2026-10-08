(() => {
    const interactive = "a, button, input, select, textarea, label, form, details, summary, [contenteditable], [role='button'], [role='link']";

    function setExpanded(row, expanded) {
        const toggle = row.querySelector('[data-approval-expand]');
        const detail = document.getElementById(toggle?.getAttribute('aria-controls'));
        if (!detail?.matches('[data-approval-detail]') || !toggle) return;

        row.classList.toggle('is-expanded', expanded);
        detail.hidden = !expanded || row.hidden || getComputedStyle(row).display === 'none';
        toggle.setAttribute('aria-expanded', String(expanded));
        toggle.setAttribute('aria-label', `${row.dataset.firma || 'Belge'} ayrıntılarını ${expanded ? 'kapat' : 'aç'}`);
        toggle.title = expanded ? 'Satır ayrıntılarını kapat' : 'Satır ayrıntılarını aç';
    }

    document.querySelectorAll('.df-approval-table.is-compact-approval').forEach(table => {
        let rejectToggle = null;

        function closeRejection(restoreFocus = false) {
            if (!rejectToggle) return;
            const panel = document.getElementById(rejectToggle.getAttribute('aria-controls'));
            if (panel) panel.hidden = true;
            rejectToggle.setAttribute('aria-expanded', 'false');
            if (restoreFocus) rejectToggle.focus({ preventScroll: true });
            rejectToggle = null;
        }

        table.addEventListener('click', event => {
            const target = event.target;
            if (!(target instanceof Element)) return;
            if (target.closest('[data-approval-reject-close]')) {
                closeRejection(true);
                return;
            }
            const reject = target.closest('[data-approval-reject-toggle]');
            if (reject) {
                const open = reject.getAttribute('aria-expanded') !== 'true';
                closeRejection();
                const panel = document.getElementById(reject.getAttribute('aria-controls'));
                if (open && panel?.matches('[data-approval-reject-panel]') && table.contains(panel)) {
                    rejectToggle = reject;
                    reject.setAttribute('aria-expanded', 'true');
                    panel.hidden = false;
                    panel.querySelector('[name="gerekce"]')?.focus({ preventScroll: true });
                }
                return;
            }
            const row = target.closest('tr[data-approval-row]');
            if (!row || !table.contains(row)) return;
            if (target.closest(interactive) && !target.closest('[data-approval-expand]')) return;

            const expanded = !row.classList.contains('is-expanded');
            table.querySelectorAll('tr[data-approval-row].is-expanded').forEach(other => {
                if (other !== row) setExpanded(other, false);
            });
            setExpanded(row, expanded);
        });

        table.addEventListener('keydown', event => {
            if (event.key === 'Escape' && rejectToggle) {
                event.preventDefault();
                closeRejection(true);
            }
        });
    });
})();
