(() => {
    const root = document.querySelector('.service-editor');
    if (!root) return;
    const search = root.querySelector('#service-brand-search');
    const all = root.querySelector('#service-all-brands');
    const items = Array.from(root.querySelectorAll('[data-brand-name]'));
    const boxes = items.map(item => item.querySelector('input'));
    const count = root.querySelector('#service-brand-count');
    function update() {
        const selected = boxes.filter(box => box.checked).length;
        all.checked = selected > 0 && selected === boxes.length;
        all.indeterminate = selected > 0 && selected < boxes.length;
        all.disabled = boxes.length === 0;
        count.textContent = selected + ' seçili';
    }
    search.addEventListener('input', () => {
        const query = search.value.trim().toLocaleLowerCase('tr-TR');
        items.forEach(item => { item.hidden = !item.dataset.brandName.toLocaleLowerCase('tr-TR').includes(query); });
        root.querySelector('#service-brand-empty').hidden = items.length === 0 || items.some(item => !item.hidden);
    });
    all.addEventListener('change', () => { boxes.forEach(box => { box.checked = all.checked; }); update(); });
    boxes.forEach(box => box.addEventListener('change', update));
    update();
})();
