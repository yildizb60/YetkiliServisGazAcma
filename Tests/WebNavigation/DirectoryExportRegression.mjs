import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import assert from 'node:assert/strict';

const source = fs.readFileSync(path.resolve(import.meta.dirname, '../../YetkiliServisGazAcma/wwwroot/js/commissioning-history.js'), 'utf8');
let passed = 0;
function check(name, test) {
    test();
    passed++;
    console.log(`PASS: ${name}`);
}

function directory({ ids = [41, 42], selected = [], all = false, view = 'list', formPresent = true,
    url = '/export/pdf?sirketId=7&q=000943&kaynak=talepler&sayfa=3&ids=999' } = {}) {
    const element = () => ({ hidden: true, dataset: {}, listeners: {}, classList: { add() {}, toggle() {} },
        setAttribute() {}, addEventListener(event, handler) { this.listeners[event] = handler; } });
    const nodes = Object.fromEntries(['form', 'panel', 'toggle', 'popover', 'body', 'close', 'clear', 'viewGroup',
        'filterCount', 'selectionCount', 'selectAll', 'button', 'exportForm', 'exportFields'].map(key => [key, element()]));
    nodes.form.id = 'commissioningFilters';
    nodes.form.querySelectorAll = () => [];
    nodes.popover.querySelector = selector => selector.includes('body') ? nodes.body : selector.includes('close') ? nodes.close : nodes.clear;
    nodes.button.dataset.exportUrl = url;
    nodes.exportFields.children = [];
    nodes.exportFields.replaceChildren = () => { nodes.exportFields.children = []; };
    nodes.exportFields.append = child => nodes.exportFields.children.push(child);
    nodes.exportForm.method = 'post';
    nodes.exportForm.querySelector = () => nodes.exportFields;
    const token = { name: '__RequestVerificationToken', value: 'fixture-token' };
    const submissions = [];
    nodes.exportForm.requestSubmit = () => submissions.push({ action: nodes.exportForm.action,
        method: nodes.exportForm.method, body: new URLSearchParams([token, ...nodes.exportFields.children].map(input => [input.name, input.value])) });
    const selections = ids.map((_, index) => Object.assign(element(), { checked: selected.includes(index) }));
    const records = ids.map((id, index) => ({ dataset: { exportId: String(id) },
        querySelector: () => selections[index], hasAttribute: () => false }));
    const root = element();
    root.querySelector = selector => ({ '#commissioningFilters': nodes.form, '#commissioningFiltersPanel': nodes.panel,
        '[data-history-filter-toggle]': nodes.toggle, '[data-history-column-popover]': nodes.popover,
        '[data-history-view-group]': nodes.viewGroup, '[data-history-filter-count]': nodes.filterCount,
        '[data-directory-selection-count]': nodes.selectionCount,
        '[data-commissioning-select-all],[data-directory-select-all]': nodes.selectAll,
        '[data-directory-export-form]': formPresent ? nodes.exportForm : null })[selector] || null;
    root.querySelectorAll = selector => ({ '[data-commissioning-record],[data-directory-record]': records,
        '[data-commissioning-select],[data-directory-select]': selections,
        '[data-commissioning-bulk-export],[data-directory-export]': [nodes.button] })[selector] || [];
    root.hasAttribute = () => all;
    vm.runInNewContext(source, { document: { querySelector: () => root, addEventListener() {}, createElement: element },
        window: { addEventListener() {}, location: { origin: 'https://fixture.test', assign() { throw new Error('GET navigation is forbidden for bulk export'); } } },
        localStorage: { getItem: () => view }, URL });
    return { submit: () => nodes.button.listeners.click(), submissions, selections, nodes };
}

for (const view of ['list', 'cards']) {
    check(`${view}: unselected commissioning POST contains all displayed IDs and the token, not a long action URL`, () => {
        const fixture = directory({ view });
        fixture.submit();
        const submission = fixture.submissions[0];
        assert.equal(submission.method, 'post');
        assert.equal(submission.action, 'https://fixture.test/export/pdf');
        assert.deepEqual(submission.body.getAll('ids'), ['41', '42']);
        assert.equal(submission.body.get('__RequestVerificationToken'), 'fixture-token');
        assert.equal(submission.body.get('sirketId'), '7');
        assert.equal(submission.body.get('q'), '000943');
        assert.equal(submission.body.get('kaynak'), 'talepler');
        assert.equal(submission.body.get('sayfa'), '3');
    });
    check(`${view}: selected commissioning/YKC exports replace stale URL IDs and keep return context`, () => {
        for (const all of [false, true]) {
            const fixture = directory({ view, all, selected: [1] });
            fixture.submit();
            assert.deepEqual(fixture.submissions[0].body.getAll('ids'), ['42']);
            assert.equal(new URL(fixture.submissions[0].action).search, '');
        }
    });
    check(`${view}: unselected YKC export omits IDs but preserves effective filters, source and page`, () => {
        const fixture = directory({ view, all: true,
            url: '/ykc/raporlar/excel?sirketId=7&aboneNo=00077&kontrolNo=2&bekleyenIs=randevu&sozlesmeNo=000943&randevuBas=2026-10-01&randevuBit=2026-10-31&kaynak=talepler&sayfa=3&sayfaBoyutu=10' });
        fixture.submit();
        const submission = fixture.submissions[0];
        assert.equal(submission.action, 'https://fixture.test/ykc/raporlar/excel');
        assert.equal(submission.body.has('ids'), false);
        assert.equal(submission.body.get('sirketId'), '7');
        assert.equal(submission.body.get('aboneNo'), '00077');
        assert.equal(submission.body.get('kontrolNo'), '2');
        assert.equal(submission.body.get('bekleyenIs'), 'randevu');
        assert.equal(submission.body.get('sozlesmeNo'), '000943');
        assert.equal(submission.body.get('randevuBas'), '2026-10-01');
        assert.equal(submission.body.get('randevuBit'), '2026-10-31');
        assert.equal(submission.body.get('kaynak'), 'talepler');
        assert.equal(submission.body.get('sayfa'), '3');
        assert.equal(submission.body.get('sayfaBoyutu'), '10');
    });
}
for (const count of [1000, 5000, 5001]) {
    check(`${count} commissioning IDs stay in the POST body with a short request target`, () => {
        const fixture = directory({ ids: Array.from({ length: count }, (_, i) => 1000000 + i) });
        fixture.submit();
        const submission = fixture.submissions[0];
        assert.equal(submission.body.getAll('ids').length, count);
        assert.equal(submission.body.getAll('ids').at(-1), String(1000000 + count - 1));
        assert.ok(Buffer.byteLength(submission.action) < 100);
        assert.ok(Buffer.byteLength(submission.body.toString()) > 8192);
    });
}
check('Repeated export clears old generated fields but retains one token and deduplicates IDs', () => {
    const fixture = directory({ ids: [41, 41, 42] });
    fixture.submit();
    fixture.selections[2].checked = true;
    fixture.submit();
    assert.deepEqual(fixture.submissions[0].body.getAll('ids'), ['41', '42']);
    assert.deepEqual(fixture.submissions[1].body.getAll('ids'), ['42']);
    assert.deepEqual(fixture.submissions[1].body.getAll('__RequestVerificationToken'), ['fixture-token']);
});
check('Intentional all-company report does not acquire a company field', () => {
    const fixture = directory({ all: true, url: '/ykc/raporlar/pdf?kaynak=rapor&sayfa=2&sayfaBoyutu=25' });
    fixture.submit();
    assert.equal(fixture.submissions[0].body.has('sirketId'), false);
    assert.equal(fixture.submissions[0].body.has('ids'), false);
});
check('Empty directory and missing form cannot initiate a bulk download', () => {
    for (const options of [{ ids: [] }, { formPresent: false }]) {
        const fixture = directory(options);
        fixture.submit();
        assert.equal(fixture.submissions.length, 0);
    }
});
console.log(`${passed} directory export JS regression checks passed.`);
