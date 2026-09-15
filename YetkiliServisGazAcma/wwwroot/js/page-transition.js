(() => {
    const overlay = document.querySelector('[data-page-transition]');
    if (!overlay) return;

    const downloadPath = /(?:^|[-_/])(pdf|excel|xlsx?|indir|download|export)(?:[-_/]|$)/i;
    let revealTimer = 0;
    let safetyTimer = 0;

    const hide = () => {
        window.clearTimeout(revealTimer);
        window.clearTimeout(safetyTimer);
        overlay.hidden = true;
        document.documentElement.classList.remove('is-page-transitioning');
    };

    const show = () => {
        window.clearTimeout(revealTimer);
        window.clearTimeout(safetyTimer);

        revealTimer = window.setTimeout(() => {
            overlay.hidden = false;
            document.documentElement.classList.add('is-page-transitioning');
            safetyTimer = window.setTimeout(hide, 20000);
        }, 220);
    };

    const ignoredElement = element => element?.closest('[data-page-transition-ignore], [aria-disabled="true"], .is-disabled');
    const ignoredUrl = url => downloadPath.test(url.pathname);

    document.addEventListener('click', event => {
        if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;

        const link = event.target.closest('a[href]');
        if (!link || ignoredElement(link) || link.hasAttribute('download') || link.target === '_blank') return;

        const href = link.getAttribute('href')?.trim();
        if (!href || href.startsWith('#') || /^(javascript:|mailto:|tel:)/i.test(href)) return;

        const url = new URL(link.href, window.location.href);
        if (url.origin !== window.location.origin || ignoredUrl(url)) return;
        if (url.pathname === window.location.pathname && url.search === window.location.search && url.hash) return;

        show();
    });

    document.addEventListener('submit', event => {
        if (event.defaultPrevented) return;

        const form = event.target;
        const submitter = event.submitter;
        if (!(form instanceof HTMLFormElement) || ignoredElement(form) || ignoredElement(submitter)) return;
        if (submitter?.formTarget === '_blank' || submitter?.hasAttribute('download')) return;
        if (!form.noValidate && !form.checkValidity()) return;

        const action = new URL(submitter?.formAction || form.action || window.location.href, window.location.href);
        if (action.origin !== window.location.origin || ignoredUrl(action)) return;

        show();
    });

    overlay.dataset.pageTransitionReady = 'true';
    window.dfPageTransition = Object.freeze({ show, hide });
    window.addEventListener('pageshow', hide);
    window.addEventListener('load', hide);
})();
