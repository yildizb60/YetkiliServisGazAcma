(() => {
    const form = document.getElementById('ykcCreateForm');
    if (!form) return;
    const field = id => document.getElementById(id);
    const query = field('ykcTesisatSorgulaBtn');
    const selector = field('ykcServisCihazSecimi');
    const alert = field('ykcSorguAlert');
    const deviceFields = field('ykcDeviceFields');
    const pendingState = field('ykcRequestPending');
    const reference = field('SorguReferansi');
    let pendingQuery = null;

    function setQueryStatus(state, message) {
        const status = field('ykcQueryStatus');
        status.className = 'ykc-query-status is-' + state;
        status.hidden = !message;
        field('ykcQueryStatusText').textContent = message;
        field('ykcQueryStatusIcon').className = 'bi ' + ({ loading: 'bi-arrow-repeat', success: 'bi-check2-circle', error: 'bi-exclamation-circle' }[state] || 'bi-search');
        form.setAttribute('aria-busy', String(state === 'loading'));
    }

    function updateSelectedDevice() {
        reference.value = selector.value;
        field('ykcSelectedDeviceName').value = selector.value ? selector.selectedOptions[0].textContent.trim() : '';
    }

    function invalidate() {
        pendingQuery?.abort();
        pendingQuery = null;
        reference.value = '';
        field('ykcSelectedDeviceName').value = '';
        field('MusteriAdi').value = '';
        field('Adres').value = '';
        deviceFields.hidden = true;
        deviceFields.disabled = true;
        pendingState.hidden = false;
        selector.replaceChildren();
        query.disabled = false;
        query.classList.add('df-btn-primary');
        query.classList.remove('df-btn-secondary');
        alert.hidden = true;
        setQueryStatus('idle', '');
    }

    ['TesisatNo', 'SozlesmeNo'].forEach(id => {
        field(id).addEventListener('input', invalidate);
        field(id).addEventListener('keydown', event => {
            if (event.key === 'Enter') {
                event.preventDefault();
                if (!query.disabled) query.click();
            }
        });
    });
    selector.addEventListener('change', updateSelectedDevice);

    query.addEventListener('click', async () => {
        if (!field('TesisatNo').reportValidity() || !field('SozlesmeNo').reportValidity()) return;
        invalidate();
        const controller = new AbortController();
        pendingQuery = controller;
        const timeout = setTimeout(() => controller.abort(), 45000);
        query.disabled = true;
        setQueryStatus('loading', 'Sorgulanıyor');
        try {
            const body = new FormData();
            ['TesisatNo', 'SozlesmeNo'].forEach(id => body.append(id, field(id).value.trim()));
            body.append('__RequestVerificationToken', form.querySelector('[name="__RequestVerificationToken"]').value);
            const response = await fetch('/ykc/tesisat-sorgula', {
                method: 'POST', body, signal: controller.signal,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (response.status === 401 || response.redirected) throw new Error('Oturumunuz sona erdi. Lütfen yeniden giriş yapın.');
            if (response.status === 403) throw new Error('Bu işlem için yetkiniz bulunmuyor.');
            if (!response.headers.get('content-type')?.includes('application/json')) throw new Error('Tesisat sorgusu alınamadı. Lütfen yeniden deneyin.');
            const data = await response.json();
            if (pendingQuery !== controller) return;
            if (!response.ok || !data.basarili || !Array.isArray(data.cihazlar) || !data.cihazlar.length)
                throw new Error(data.mesaj || 'Tesisatın cihaz kaydı bulunamadı.');
            if (data.cihazlar.some(device => !device.sorguReferansi)) throw new Error('Cihaz bilgileri doğrulanamadı. Lütfen yeniden sorgulayın.');

            field('MusteriAdi').value = data.musteriAdi || '';
            field('Adres').value = data.adres || '';
            field('ykcCustomerName').textContent = data.musteriAdi || 'Bilgi bulunmuyor';
            field('ykcCustomerAddress').textContent = data.adres || 'Bilgi bulunmuyor';
            if (data.cihazlar.length > 1) selector.add(new Option('Değiştirilecek cihazı seçin', ''));
            data.cihazlar.forEach((device, index) => {
                const type = device.cihazTipi?.trim() || 'Cihaz';
                const duplicate = data.cihazlar.filter(item => item.cihazTipi?.trim() === device.cihazTipi?.trim()).length > 1;
                selector.add(new Option(duplicate ? type + ' (' + (index + 1) + '. kayıt)' : type, device.sorguReferansi));
            });
            updateSelectedDevice();
            deviceFields.hidden = false;
            deviceFields.disabled = false;
            pendingState.hidden = true;
            setQueryStatus('success', 'Tesisat bulundu');
            query.classList.remove('df-btn-primary');
            query.classList.add('df-btn-secondary');
            (data.cihazlar.length === 1 ? field('YeniCihazTipi') : selector).focus();
        } catch (error) {
            if (pendingQuery === controller) {
                alert.hidden = false;
                alert.textContent = error.name === 'AbortError'
                    ? 'Sorgu zaman aşımına uğradı. Lütfen yeniden deneyin.'
                    : error instanceof TypeError ? 'Bağlantı kurulamadı. Lütfen yeniden deneyin.' : error.message;
                setQueryStatus('error', 'Sorgu tamamlanamadı');
            }
        } finally {
            clearTimeout(timeout);
            if (pendingQuery === controller) {
                pendingQuery = null;
                query.disabled = false;
                form.setAttribute('aria-busy', 'false');
            }
        }
    });

    form.querySelectorAll('[name="IkinciElCihazMi"]').forEach(radio => radio.addEventListener('change', () => {
        field('ykcSecondhandNote').hidden = form.querySelector('[name="IkinciElCihazMi"]:checked').value !== 'true';
    }));
    form.addEventListener('submit', event => {
        if (!reference.value) {
            event.preventDefault();
            alert.hidden = false;
            alert.textContent = 'Tesisatı sorgulayın ve değiştirilecek cihazı seçin.';
            (deviceFields.hidden ? field('TesisatNo') : selector).focus();
            return;
        }
        field('ykcCreateButton').disabled = true;
    });
    window.addEventListener('pageshow', () => { field('ykcCreateButton').disabled = false; });
})();
