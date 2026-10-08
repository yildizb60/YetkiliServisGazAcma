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
    const deviceType = field('YeniCihazTipi');
    const selectedDeviceType = field('ykcSelectedDeviceType');
    const legacyComparison = field('ykcDeviceComparison');
    const comparisonEnabled = (form.hasAttribute('data-device-comparison') || legacyComparison !== null)
        && typeof window.operationToast?.warning === 'function';
    legacyComparison?.remove();
    const comparisonFields = ['SorguReferansi', 'TesisatNo', 'SozlesmeNo', 'YeniCihazTipi', 'YeniMarka', 'YeniBacaTipi', 'YeniKapasite'];
    let pendingQuery = null;
    let pendingComparison = null;
    let comparisonTimer = null;
    let checkedComparisonKey = '';
    let submittingComparison = false;
    let comparisonToasts = [];
    let hasComparisonMessages = false;

    function comparisonKey() {
        return JSON.stringify(comparisonFields.map(id => field(id).value.trim()));
    }

    function clearComparison() {
        clearTimeout(comparisonTimer);
        pendingComparison?.abort();
        pendingComparison = null;
        checkedComparisonKey = '';
        hasComparisonMessages = false;
        dismissComparison();
    }

    function dismissComparison() {
        comparisonToasts.forEach(toast => toast.querySelector('[data-operation-toast-close]')?.click());
        comparisonToasts = [];
    }

    function showComparison(messages) {
        dismissComparison();
        hasComparisonMessages = messages.length > 0;
        if (!hasComparisonMessages) return;
        comparisonToasts = messages.map((message, index) => {
            // Later warnings need time for the messages above them to be read first.
            const readingTime = Math.max(12000, message.trim().split(/\s+/).length * 400) + index * 4000;
            const toast = window.operationToast.warning(message, readingTime);
            toast.classList.add('ykc-device-comparison-toast');
            return toast;
        });
    }

    async function checkComparison() {
        clearTimeout(comparisonTimer);
        pendingComparison?.abort();
        if (!reference.value || !deviceType.value) return;
        const key = comparisonKey();
        const controller = new AbortController();
        pendingComparison = controller;
        const timeout = setTimeout(() => controller.abort(), 10000);
        try {
            const body = new FormData();
            comparisonFields.forEach(id => body.append(id, field(id).value.trim()));
            body.append('__RequestVerificationToken', form.querySelector('[name="__RequestVerificationToken"]').value);
            const response = await fetch('/ykc/cihaz-karsilastir', {
                method: 'POST', body, signal: controller.signal,
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (response.status === 401 || response.redirected) throw new Error('Oturumunuz sona erdi. Lütfen yeniden giriş yapın.');
            if (response.status === 403) throw new Error('Cihaz karşılaştırma yetkiniz bulunmuyor.');
            if (!response.ok || !response.headers.get('content-type')?.includes('application/json'))
                throw new Error('Cihaz karşılaştırması şu anda yapılamıyor. Talebiniz incelemede kontrol edilecektir.');
            const data = await response.json();
            if (pendingComparison !== controller || comparisonKey() !== key) return;
            if (data.basarili !== true || !Array.isArray(data.uyarilar))
                throw new Error(data.mesaj || 'Cihaz bilgileri karşılaştırılamadı. Lütfen tesisatı yeniden sorgulayın.');
            showComparison(data.uyarilar);
        } catch (error) {
            if (pendingComparison !== controller || comparisonKey() !== key) return;
            showComparison([error.name === 'AbortError' || error instanceof TypeError
                ? 'Cihaz karşılaştırması şu anda yapılamıyor. Talebiniz incelemede kontrol edilecektir.'
                : error.message]);
        } finally {
            clearTimeout(timeout);
            if (pendingComparison === controller && comparisonKey() === key) {
                pendingComparison = null;
                checkedComparisonKey = key;
            }
        }
    }

    function scheduleComparison() {
        if (!comparisonEnabled) return;
        clearComparison();
        if (reference.value && deviceType.value
            && ['YeniMarka', 'YeniBacaTipi', 'YeniKapasite'].some(id => field(id).value.trim()))
            comparisonTimer = setTimeout(checkComparison, 450);
    }

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
        const type = selector.value ? selector.selectedOptions[0].dataset.deviceType?.trim() || '' : '';
        selectedDeviceType.value = type;
        deviceType.value = Array.from(deviceType.options).some(option => option.value === type) ? type : '';
        scheduleComparison();
    }

    function fillDeviceTypes(devices) {
        const types = [];
        devices.forEach(device => {
            const type = device.cihazTipi?.trim() || '';
            if (type && !types.some(existing => existing.localeCompare(type, 'tr', { sensitivity: 'accent' }) === 0)) {
                types.push(type);
            }
        });
        if (!types.length) {
            throw new Error('Servisten yakıcı cihaz tipi alınamadı. Lütfen yeniden sorgulayın.');
        }
        deviceType.replaceChildren(new Option('Cihaz tipi seçin', ''));
        types.forEach(type => deviceType.add(new Option(type, type)));
    }

    function invalidate() {
        clearComparison();
        pendingQuery?.abort();
        pendingQuery = null;
        reference.value = '';
        field('ykcSelectedDeviceName').value = '';
        selectedDeviceType.value = '';
        deviceType.replaceChildren(new Option('Önce tesisatı sorgulayın', ''));
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
    ['YeniCihazTipi', 'YeniMarka', 'YeniBacaTipi', 'YeniKapasite'].forEach(id => {
        field(id).addEventListener('input', scheduleComparison);
        field(id).addEventListener('change', scheduleComparison);
    });

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
            fillDeviceTypes(data.cihazlar);
            if (data.cihazlar.length > 1) selector.add(new Option('Değiştirilecek cihazı seçin', ''));
            data.cihazlar.forEach((device, index) => {
                const type = device.cihazTipi?.trim() || '';
                const duplicate = data.cihazlar.filter(item => item.cihazTipi?.trim() === device.cihazTipi?.trim()).length > 1;
                const option = new Option(duplicate ? (type || 'Cihaz türü belirtilmedi') + ' (' + (index + 1) + '. kayıt)' : type || 'Cihaz türü belirtilmedi', device.sorguReferansi);
                option.dataset.deviceType = type;
                selector.add(option);
            });
            updateSelectedDevice();
            deviceFields.hidden = false;
            deviceFields.disabled = false;
            pendingState.hidden = true;
            setQueryStatus('success', 'Tesisat bulundu');
            query.classList.remove('df-btn-primary');
            query.classList.add('df-btn-secondary');
            (data.cihazlar.length === 1 ? field('YeniMarka') : selector).focus();
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

    form.addEventListener('submit', async event => {
        if (!reference.value) {
            event.preventDefault();
            alert.hidden = false;
            alert.textContent = 'Tesisatı sorgulayın ve değiştirilecek cihazı seçin.';
            (deviceFields.hidden ? field('TesisatNo') : selector).focus();
            return;
        }
        if (submittingComparison) {
            event.preventDefault();
            return;
        }
        if (comparisonEnabled && checkedComparisonKey !== comparisonKey()) {
            event.preventDefault();
            submittingComparison = true;
            field('ykcCreateButton').setAttribute('aria-busy', 'true');
            const key = comparisonKey();
            try {
                await checkComparison();
            } finally {
                submittingComparison = false;
                field('ykcCreateButton').removeAttribute('aria-busy');
            }
            if (checkedComparisonKey !== key || comparisonKey() !== key) return;
            // A warning returned during submission must be visible before the next submit.
            if (hasComparisonMessages) return;
            form.requestSubmit(event.submitter || field('ykcCreateButton'));
            return;
        }
        field('ykcCreateButton').disabled = true;
    });
    window.addEventListener('pageshow', () => {
        field('ykcCreateButton').disabled = false;
        scheduleComparison();
    });
    scheduleComparison();
})();
