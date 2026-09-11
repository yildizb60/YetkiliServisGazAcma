(() => {
    function connect(root) {
        const tabs = [...root.querySelectorAll('[role="tab"]')];
        function select(index, focus = false) {
            tabs.forEach((tab, i) => {
                const active = i === index;
                tab.setAttribute('aria-selected', String(active));
                tab.tabIndex = active ? 0 : -1;
                document.getElementById(tab.getAttribute('aria-controls')).hidden = !active;
            });
            if (focus) tabs[index].focus();
        }
        tabs.forEach((tab, i) => {
            tab.addEventListener('click', () => select(i));
            tab.addEventListener('keydown', event => {
                let next;
                if (event.key === 'ArrowRight') next = (i + 1) % tabs.length;
                if (event.key === 'ArrowLeft') next = (i - 1 + tabs.length) % tabs.length;
                if (event.key === 'Home') next = 0;
                if (event.key === 'End') next = tabs.length - 1;
                if (next === undefined) return;
                event.preventDefault();
                select(next, true);
            });
        });
        select(0);
        return () => select(0);
    }
    document.querySelectorAll('[data-record-tabs]').forEach(connect);
    document.querySelectorAll('.ykc-report-modal-dialog, .ys-report-modal-dialog').forEach((dialog, index) => {
        const grid = dialog.querySelector('.ykc-modal-grid, .ys-modal-grid');
        const sections = grid ? [...grid.children].filter(e => e.tagName === 'SECTION') : [];
        if (sections.length < 2) return;
        const nav = document.createElement('nav');
        nav.className = 'record-tabs';
        nav.setAttribute('role', 'tablist');
        nav.setAttribute('aria-label', 'Detay b\u00f6l\u00fcmleri');
        sections.forEach((section, i) => {
            const heading = section.querySelector('.ykc-modal-section-title, h3');
            if (!heading) return;
            const id = `detail-${index}-panel-${i}`;
            section.id ||= id;
            section.setAttribute('role', 'tabpanel');
            section.setAttribute('aria-labelledby', id + '-tab');
            const tab = document.createElement('button');
            tab.type = 'button';
            tab.id = id + '-tab';
            tab.setAttribute('role', 'tab');
            tab.setAttribute('aria-controls', section.id);
            const icon = heading.querySelector('i');
            if (icon) { const copy = icon.cloneNode(true); copy.setAttribute('aria-hidden', 'true'); tab.append(copy); }
            tab.append(document.createTextNode(heading.textContent.trim()));
            nav.append(tab);
        });
        if (nav.children.length !== sections.length) return;
        grid.before(nav);
        dialog.classList.add('is-tabbed-dialog');
        const reset = connect(dialog);
        const modal = dialog.closest('.ykc-report-modal, .ys-report-modal');
        dialog.addEventListener('keydown', event => {
            if (event.key !== 'Tab') return;
            const controls = [...dialog.querySelectorAll('button:not(:disabled),a[href],input:not(:disabled),select:not(:disabled),textarea:not(:disabled),[tabindex="0"]')]
                .filter(e => e.tabIndex >= 0 && e.getClientRects().length);
            if (!controls.length) return;
            if (event.shiftKey && document.activeElement === controls[0]) {
                event.preventDefault(); controls.at(-1).focus();
            } else if (!event.shiftKey && document.activeElement === controls.at(-1)) {
                event.preventDefault(); controls[0].focus();
            }
        });
        // Every newly opened record starts at its first section, including keyboard navigation.
        new MutationObserver(() => {
            if (!modal.classList.contains('is-open')) return;
            reset();
            dialog.querySelector('button')?.focus();
        })
            .observe(modal, { attributes: true, attributeFilter: ['class'] });
    });
})();
