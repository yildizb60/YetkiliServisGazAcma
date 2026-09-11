const viewer = document.querySelector('[data-pdf-viewer]');
if (viewer) {
    const message = viewer.querySelector('[data-pdf-message]');
    const canvas = viewer.querySelector('[data-pdf-canvas]');
    const scroll = viewer.querySelector('[data-pdf-scroll]');
    const previous = viewer.querySelector('[data-pdf-prev]');
    const next = viewer.querySelector('[data-pdf-next]');
    const zoom = viewer.querySelector('[data-pdf-zoom]');
    const pageLabel = viewer.querySelector('[data-pdf-page]');
    let pdf, pageNumber = 1, rendering = false, queued = false;
    function showError() {
        message.textContent = 'Form görüntülenemedi. Sayfayı yenileyin veya PDF indirme düğmesini kullanın.';
        message.setAttribute('role', 'alert');
        message.hidden = false;
    }
    // Render one page at a time so large documents and rapid resizing stay bounded.
    async function render() {
        if (!pdf) return;
        if (rendering) { queued = true; return; }
        rendering = true;
        viewer.setAttribute('aria-busy', 'true');
        try {
            do {
                queued = false;
                const current = pageNumber;
                const page = await pdf.getPage(current);
                const base = page.getViewport({ scale: 1 });
                const padding = parseFloat(getComputedStyle(scroll).paddingLeft) * 2;
                const scale = zoom.value === 'fit'
                    ? Math.min(2, Math.max(0.25, (scroll.clientWidth - padding - 2) / base.width))
                    : Number(zoom.value) / 75;
                const viewport = page.getViewport({ scale });
                const ratio = Math.min(window.devicePixelRatio || 1, 2, Math.sqrt(12000000 / (viewport.width * viewport.height)));
                canvas.width = Math.floor(viewport.width * ratio);
                canvas.height = Math.floor(viewport.height * ratio);
                canvas.style.width = `${Math.floor(viewport.width)}px`;
                canvas.style.height = `${Math.floor(viewport.height)}px`;
                await page.render({ canvasContext: canvas.getContext('2d'), viewport, transform: [ratio, 0, 0, ratio, 0, 0] }).promise;
                const content = await page.getTextContent();
                viewer.querySelector('[data-pdf-text]').textContent = content.items.map(item => item.str || '').join(' ');
                canvas.setAttribute('aria-label', `Form, ${current}. sayfa`);
                canvas.hidden = false;
                message.hidden = true;
                pageLabel.textContent = `${current} / ${pdf.numPages}`;
                previous.disabled = current <= 1;
                next.disabled = current >= pdf.numPages;
                zoom.disabled = false;
                page.cleanup();
            } while (queued);
        } catch {
            showError();
        } finally {
            rendering = false;
            viewer.removeAttribute('aria-busy');
        }
    }
    previous.addEventListener('click', () => { if (pageNumber > 1) { pageNumber--; scroll.scrollTop = 0; render(); } });
    next.addEventListener('click', () => { if (pdf && pageNumber < pdf.numPages) { pageNumber++; scroll.scrollTop = 0; render(); } });
    zoom.addEventListener('change', render);
    let resizeTimer;
    const observer = new ResizeObserver(() => { clearTimeout(resizeTimer); resizeTimer = setTimeout(() => { if (zoom.value === 'fit') render(); }, 120); });
    observer.observe(scroll);
    try {
        const lib = await import('/lib/pdfjs/pdf.mjs');
        lib.GlobalWorkerOptions.workerSrc = '/lib/pdfjs/pdf.worker.mjs';
        pdf = await lib.getDocument({
            url: viewer.dataset.pdfUrl,
            cMapUrl: '/lib/pdfjs/cmaps/', cMapPacked: true,
            standardFontDataUrl: '/lib/pdfjs/standard_fonts/', wasmUrl: '/lib/pdfjs/wasm/',
            isEvalSupported: false
        }).promise;
        await render();
    } catch {
        showError();
    }
    window.addEventListener('pagehide', event => {
        if (event.persisted) return;
        observer.disconnect();
        clearTimeout(resizeTimer);
        pdf?.destroy();
    });
}
