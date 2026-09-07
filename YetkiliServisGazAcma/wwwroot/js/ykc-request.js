(() => {
    const form = document.getElementById('ykcCreateForm');
    if (!form) return;
    const field = id => document.getElementById(id);
    const query = field('ykcTesisatSorgulaBtn');
    const selector = field('ykcServisCihazSecimi');
    const alert = field('ykcSorguAlert');
    let generation = 0;
    function invalidate() {
        generation++;
        field('SorguReferansi').value = '';
        field('ykcDeviceFields').hidden = true;
        field('ykcDeviceFields').disabled = true;
        field('ykcCustomer').hidden = true;
        selector.replaceChildren();
        query.classList.add('df-btn-primary');
        query.classList.remove('df-btn-secondary');
    }
    ['TesisatNo', 'SozlesmeNo'].forEach(id => field(id).addEventListener('input', invalidate));
    selector.addEventListener('change', () => { field('SorguReferansi').value = selector.value; });
    query.addEventListener('click', async () => {
        if (!field('TesisatNo').reportValidity() || !field('SozlesmeNo').reportValidity()) return;
        invalidate();
        const current = generation;
        query.disabled = true;
        alert.hidden = false;
        alert.textContent = 'Sorgulanıyor...';
        try {
            const body = new FormData();
            ['TesisatNo', 'SozlesmeNo'].forEach(id => body.append(id, field(id).value.trim()));
            body.append('__RequestVerificationToken', form.querySelector('[name="__RequestVerificationToken"]').value);
            const response = await fetch('/ykc/tesisat-sorgula', { method: 'POST', body, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) throw new Error('Tesisat sorgusu alınamadı. Lütfen yeniden deneyin.');
            const data = await response.json();
            if (current !== generation) return;
            if (!data.basarili || !data.cihazlar?.length) throw new Error(data.mesaj || 'Tesisatın cihaz kaydı bulunamadı.');
            field('MusteriAdi').value = data.musteriAdi || '';
            field('Adres').value = data.adres || '';
            field('ykcCustomerName').textContent = data.musteriAdi || '';
            field('ykcCustomerAddress').textContent = data.adres || '';
            if (data.cihazlar.length > 1) selector.add(new Option('Cihaz seçin', ''));
            data.cihazlar.forEach((device, index) => selector.add(new Option((index + 1) + '. Cihaz' + (device.cihazTipi ? ' · ' + device.cihazTipi : ''), device.sorguReferansi)));
            field('SorguReferansi').value = selector.value;
            field('ykcDeviceFields').hidden = false;
            field('ykcDeviceFields').disabled = false;
            field('ykcCustomer').hidden = false;
            alert.hidden = true;
            query.classList.remove('df-btn-primary');
            query.classList.add('df-btn-secondary');
            (data.cihazlar.length === 1 ? field('YeniCihazTipi') : selector).focus({ preventScroll: true });
        } catch (error) {
            if (current === generation) alert.textContent = error.message;
        } finally { query.disabled = false; }
    });
    form.querySelectorAll('[name="IkinciElCihazMi"]').forEach(radio => radio.addEventListener('change', () => {
        field('ykcSecondhandNote').hidden = form.querySelector('[name="IkinciElCihazMi"]:checked').value !== 'true';
    }));
    form.addEventListener('submit', event => {
        if (!field('SorguReferansi').value) {
            event.preventDefault();
            alert.hidden = false;
            alert.textContent = 'Tesisatı sorgulayın ve değiştirilecek cihazı seçin.';
            return;
        }
        field('ykcCreateButton').disabled = true;
    });
})();
