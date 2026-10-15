import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import assert from 'node:assert/strict';

const sourcePath = path.resolve(import.meta.dirname, '../../YetkiliServisGazAcma/wwwroot/js/commissioning-history.js');
const source = fs.readFileSync(sourcePath, 'utf8');
let passed = 0;
function check(name, test) {
    test();
    passed++;
    console.log(`PASS: ${name}`);
}

function directory({ ids = [41, 42], selected = [], all = false, view = 'list', formPresent = true,
    exportIds = null, paged = false, detailPresent = false, href = 'https://fixture.test/list',
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
    root.dataset.historyPaged = paged ? 'true' : undefined;
    const template = exportIds === null ? null : { content: { textContent: exportIds.join(',') } };
    root.querySelector = selector => ({ '#commissioningFilters': nodes.form, '#commissioningFiltersPanel': nodes.panel,
        '[data-history-filter-toggle]': nodes.toggle, '[data-history-column-popover]': nodes.popover,
        '[data-history-view-group]': nodes.viewGroup, '[data-history-filter-count]': nodes.filterCount,
        '[data-directory-selection-count]': nodes.selectionCount,
        '[data-commissioning-select-all],[data-directory-select-all]': nodes.selectAll,
        '[data-directory-export-form]': formPresent ? nodes.exportForm : null,
        'template[data-directory-export-ids]': template })[selector] || null;
    root.querySelectorAll = selector => ({ '[data-commissioning-record],[data-directory-record]': records,
        '[data-commissioning-select],[data-directory-select]': selections,
        '[data-commissioning-bulk-export],[data-directory-export]': [nodes.button] })[selector] || [];
    root.hasAttribute = name => name === 'data-directory-export-all' && all;
    const navigations = [], windowListeners = {};
    const window = { location: Object.assign(new URL(href), { replace: url => navigations.push(url),
        assign() { throw new Error('GET navigation is forbidden for bulk export'); } }),
        addEventListener(event, listener) { windowListeners[event] = listener; } };
    // This fixed repository-owned browser IIFE needs fresh DOM globals per fixture, not Node's cached module globals.
    // Fixture values are context data, never source code. VM isolation is not a security boundary for untrusted scripts.
    vm.runInNewContext(source, { document: { querySelector: () => root, getElementById: () => detailPresent ? {} : null,
        addEventListener() {}, createElement: element }, window,
        localStorage: { getItem: () => view }, URL },
        { filename: sourcePath, timeout: 1000, contextCodeGeneration: { strings: false, wasm: false } });
    return { submit: () => nodes.button.listeners.click(), submissions, selections, nodes, navigations, window, windowListeners, template };
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
check('Separate VM fixtures retain their own DOM globals when export handlers are interleaved', () => {
    const first = directory({ ids: [41], url: '/export/pdf?sirketId=7' });
    const second = directory({ ids: [85], url: '/export/excel?sirketId=8' });
    first.submit();
    second.submit();
    first.submit();
    assert.equal(first.submissions.length, 2);
    assert.equal(second.submissions.length, 1);
    for (const submission of first.submissions) {
        assert.deepEqual(submission.body.getAll('ids'), ['41']);
        assert.equal(submission.body.get('sirketId'), '7');
        assert.equal(submission.action, 'https://fixture.test/export/pdf');
    }
    assert.deepEqual(second.submissions[0].body.getAll('ids'), ['85']);
    assert.equal(second.submissions[0].body.get('sirketId'), '8');
    assert.equal(second.submissions[0].action, 'https://fixture.test/export/excel');
});
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
for (const view of ['list', 'cards']) {
    for (const count of [85, 5000, 5001]) {
        check(`${view}: no selection exports all ${count} metadata IDs, not the two visible records`, () => {
            const exportIds = Array.from({ length: count }, (_, i) => i + 1);
            const fixture = directory({ view, exportIds, paged: true });
            fixture.submit();
            assert.deepEqual(fixture.submissions[0].body.getAll('ids'), exportIds.map(String));
            assert.equal(fixture.submissions[0].action, 'https://fixture.test/export/pdf');
            assert.equal(fixture.submissions[0].body.get('sirketId'), '7');
            assert.equal(fixture.submissions[0].body.get('__RequestVerificationToken'), 'fixture-token');
        });
    }
    check(`${view}: selection overrides metadata; clearing selection restores the complete filtered list`, () => {
        const fixture = directory({ view, exportIds: [1, 2, 41, 42, 85], selected: [1] });
        fixture.submit();
        assert.deepEqual(fixture.submissions[0].body.getAll('ids'), ['42']);
        fixture.selections[1].checked = false;
        fixture.submit();
        assert.deepEqual(fixture.submissions[1].body.getAll('ids'), ['1', '2', '41', '42', '85']);
        assert.deepEqual(fixture.submissions[1].body.getAll('__RequestVerificationToken'), ['fixture-token']);
    });
}
check('Compact metadata is parsed only when the export is clicked', () => {
    const fixture = directory({ exportIds: [1] });
    fixture.template.content.textContent = '1,2,3';
    fixture.submit();
    assert.deepEqual(fixture.submissions[0].body.getAll('ids'), ['1', '2', '3']);
});
check('An empty metadata list never falls back to an unfiltered export', () => {
    const fixture = directory({ exportIds: [] });
    fixture.submit();
    assert.equal(fixture.submissions.length, 0);
});
check('Legacy missing detail uses one replacement navigation and retains the complete URL context', () => {
    const href = 'https://fixture.test/AdminPanel/devreyealmalar?marka=A%26B&q=abone&sirala=abone-artan&sirketId=7&sayfa=3#devreye-alma-detay-71';
    const fixture = directory({ href, paged: true });
    assert.equal(fixture.navigations.length, 1);
    const target = new URL(fixture.navigations[0]);
    const original = new URL(href);
    for (const [key, value] of original.searchParams) assert.equal(target.searchParams.get(key), value);
    assert.equal(target.hash, original.hash);
    assert.equal(target.searchParams.get('kayitId'), '71');
    assert.equal(directory({ href: target.href, paged: true }).navigations.length, 0);
});
check('A present detail, unresolved focus, unrelated hash and nonpaged directory never navigate', () => {
    for (const options of [
        { paged: true, detailPresent: true, href: 'https://fixture.test/list#devreye-alma-detay-71' },
        { paged: true, href: 'https://fixture.test/list?kayitId=71#devreye-alma-detay-71' },
        { paged: true, href: 'https://fixture.test/list#other' },
        { paged: false, href: 'https://fixture.test/list#devreye-alma-detay-71' }
    ]) assert.equal(directory(options).navigations.length, 0);
});
check('Back navigation to a resolved URL does not reload; another legacy hash can focus a different record', () => {
    const fixture = directory({ paged: true, href: 'https://fixture.test/list?kayitId=71#devreye-alma-detay-71' });
    fixture.windowListeners.hashchange();
    assert.equal(fixture.navigations.length, 0);
    fixture.window.location.hash = '#devreye-alma-detay-85';
    fixture.windowListeners.hashchange();
    assert.equal(new URL(fixture.navigations[0]).searchParams.get('kayitId'), '85');
});
console.log(`${passed} directory export/pagination JS regression checks passed.`);
