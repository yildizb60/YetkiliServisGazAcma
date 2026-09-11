(() => {
    const stack = document.querySelector('[data-operation-toast-stack]');
    if (!stack) return;
    const cleanup = new WeakMap();

    function dismiss(toast) {
        if (!toast || toast.classList.contains('is-leaving')) return;
        cleanup.get(toast)?.();
        toast.classList.add('is-leaving');
        setTimeout(() => toast.remove(), 220);
    }

    function bind(toast) {
        toast.querySelector('[data-operation-toast-close]')?.addEventListener('click', () => dismiss(toast));
        const timeout = Number(toast.dataset.timeout || 9000);
        if (timeout <= 0) return;
        let timer;
        const pause = () => clearTimeout(timer);
        const resume = () => {
            pause();
            if (!document.hidden && !toast.matches(':hover, :focus-within'))
                timer = setTimeout(() => dismiss(toast), timeout);
        };
        toast.addEventListener('mouseenter', pause);
        toast.addEventListener('mouseleave', resume);
        toast.addEventListener('focusin', pause);
        toast.addEventListener('focusout', () => setTimeout(resume, 0));
        document.addEventListener('visibilitychange', resume);
        cleanup.set(toast, () => {
            pause();
            document.removeEventListener('visibilitychange', resume);
        });
        resume();
    }

    function show(message, options) {
        const settings = Object.assign({
            type: "info",
            title: "Bilgilendirme",
            icon: "bi-info-circle-fill",
            timeout: 3500
        }, options || {});
        const toast = document.createElement('div');
        toast.className = 'df-operation-toast is-' + settings.type;
        toast.dataset.operationToast = '';
        toast.dataset.timeout = String(settings.timeout);
        toast.setAttribute('role', settings.type === 'error' ? 'alert' : 'status');
        toast.innerHTML = '<span class="df-operation-toast-icon"><i class="bi ' + settings.icon + '" aria-hidden="true"></i></span>'
            + '<span class="df-operation-toast-copy"><strong></strong><span></span></span>'
            + '<button class="df-operation-toast-close" type="button" data-operation-toast-close aria-label="Bildirimi kapat"><i class="bi bi-x-lg" aria-hidden="true"></i></button>';
        toast.querySelector('.df-operation-toast-copy strong').textContent = settings.title;
        toast.querySelector('.df-operation-toast-copy span').textContent = message;
        stack.append(toast);
        bind(toast);
        return toast;
    }

    const showInfo = message => show(message, { type: 'info', title: 'İndirme başlatıldı', icon: 'bi-download', timeout: 3500 });
    const showWarning = (message, timeout) => show(message, { type: 'warning', title: 'Cihaz Bilgisi Uyarısı', icon: 'bi-exclamation-triangle-fill', timeout: timeout || 3000 });
    const showError = (message, timeout) => show(message, { type: 'error', title: 'İşlem Tamamlanamadı', icon: 'bi-exclamation-circle-fill', timeout: timeout || 9000 });

    window.operationToast = Object.freeze({ info: showInfo, warning: showWarning, error: showError });

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
