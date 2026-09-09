(() => {
    const root = document.querySelector('.personnel-account');
    if (!root) return;
    const tabs = Array.from(root.querySelectorAll('[role=tab]'));
    function activate(tab, focus) {
        tabs.forEach(item => {
            const selected = item === tab;
            item.setAttribute('aria-selected', String(selected));
            item.tabIndex = selected ? 0 : -1;
            document.getElementById(item.getAttribute('aria-controls')).hidden = !selected;
        });
        if (focus) tab.focus();
    }
    tabs.forEach((tab, index) => {
        tab.addEventListener('click', () => activate(tab, false));
        tab.addEventListener('keydown', event => {
            let target;
            if (event.key === 'ArrowRight') target = (index + 1) % tabs.length;
            if (event.key === 'ArrowLeft') target = (index + tabs.length - 1) % tabs.length;
            if (event.key === 'Home') target = 0;
            if (event.key === 'End') target = tabs.length - 1;
            if (target === undefined) return;
            event.preventDefault();
            activate(tabs[target], true);
        });
    });
    const initial = root.querySelector('.account-tabs').dataset.initialTab;
    activate(document.getElementById('tab-' + initial) || tabs[0], false);
})();
