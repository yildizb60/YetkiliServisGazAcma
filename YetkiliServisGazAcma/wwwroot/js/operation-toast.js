(() => {
    const stack = document.querySelector('[data-operation-toast-stack]');
    if (!stack) return;

    function dismiss(toast) {
        if (!toast || toast.classList.contains('is-leaving')) return;
        toast.classList.add('is-leaving');
        setTimeout(() => toast.remove(), 220);
    }

    function bind(toast) {
        toast.querySelector('[data-operation-toast-close]')?.addEventListener('click', () => dismiss(toast));
        const timeout = Number(toast.dataset.timeout || 5000);
        if (timeout > 0) setTimeout(() => dismiss(toast), timeout);
    }

    function showInfo(message) {
        const toast = document.createElement('div');
        toast.className = 'df-operation-toast is-info';
        toast.dataset.operationToast = '';
        toast.dataset.timeout = '3500';
        toast.setAttribute('role', 'status');
        toast.innerHTML = '<span class="df-operation-toast-icon"><i class="bi bi-download" aria-hidden="true"></i></span>'
            + '<span class="df-operation-toast-copy"><strong>İndirme başlatıldı</strong><span></span></span>'
            + '<button class="df-operation-toast-close" type="button" data-operation-toast-close aria-label="Bildirimi kapat"><i class="bi bi-x-lg" aria-hidden="true"></i></button>';
        toast.querySelector('.df-operation-toast-copy span').textContent = message;
        stack.append(toast);
        bind(toast);
    }

    window.operationToast = Object.freeze({ info: showInfo });

    stack.querySelectorAll('[data-operation-toast]').forEach(bind);
    document.addEventListener('click', event => {
        const control = event.target.closest('a[href], button');
        if (!control || event.defaultPrevented || control.dataset.noDownloadToast !== undefined || control.disabled) return;

        const href = (control.getAttribute('href') || '').toLocaleLowerCase('tr-TR');
        const handler = (control.getAttribute('onclick') || '').toLocaleLowerCase('tr-TR');
        const formAction = (control.form?.getAttribute('action') || '').toLocaleLowerCase('tr-TR');
        const label = (control.textContent || '').trim().toLocaleLowerCase('tr-TR');
        const isDownload = control.hasAttribute('download')
            || control.classList.contains('df-btn-pdf')
            || control.classList.contains('df-btn-excel')
            || /(^|\/)pdf($|[/?-])|(^|\/)excel($|[/?-])|indir/.test(href)
            || /pdf|excel|indir/.test(handler)
            || /pdf|excel|indir/.test(formAction);
        if (isDownload && label.includes('seçili') && !document.querySelector('.row-check:checked')) return;
        if (isDownload) showInfo('Dosya hazırlanıyor; tarayıcınız indirmeyi tamamlayacaktır.');
    });
})();
