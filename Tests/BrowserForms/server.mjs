// Run: node Tests/BrowserForms/server.mjs, then open the printed URL.
// Fixtures use production scripts but never connect to the application or its API.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';

const app = new URL('../../YetkiliServisGazAcma/', import.meta.url);
const files = new Map([
    ['/toast.js', 'wwwroot/js/operation-toast.js'],
    ['/toast.css', 'wwwroot/css/operation-toast.css'],
    ['/bootstrap-icons.css', 'wwwroot/lib/bootstrap-icons/font/bootstrap-icons.min.css'],
    ['/drawer.js', 'wwwroot/js/admin-form-drawer.js'],
    ['/branch.js', 'wwwroot/js/branch-sheet.js'],
    ['/branch.css', 'wwwroot/css/branch-editor.css'],
    ['/login.css', 'wwwroot/css/login.css'],
    ['/registration.css', 'wwwroot/css/registration.css'],
    ['/permission.js', 'Views/AdminPanel/YetkiDuzenle.cshtml'],
    ['/appointment.js', 'Views/Ykc/Detay.cshtml'],
    ['/report.js', 'Views/Ykc/Raporlar.cshtml'],
    ['/report.css', 'wwwroot/css/ykc-operations.css'],
    ['/request.js', 'wwwroot/js/ykc-request.js'],
    ['/request.css', 'wwwroot/css/ykc-request.css'],
    ['/dropdown.js', 'wwwroot/js/notification-panel.js'],
    ['/commissioning.js', 'wwwroot/js/commissioning-row-details.js'],
    ['/panel-layout.css', 'wwwroot/css/panel-layout.css'],
    ['/panel-unify.css', 'wwwroot/css/panel-unify.css'],
    ['/service-editor.css', 'wwwroot/css/service-editor.css'],
    ['/home-calendar.css', 'wwwroot/css/home-calendar.css'],
    ['/personnel-home.css', 'wwwroot/css/personnel-home.css'],
    ['/operations-directory.css', 'wwwroot/css/operations-directory.css']
]);

createServer(async (request, response) => {
    try {
        const path = new URL(request.url, 'http://localhost').pathname;
        response.setHeader('Cache-Control', 'no-store');
        if (request.method !== 'GET') {
            response.writeHead(405).end();
        } else if (path === '/frame.html') {
            response.setHeader('Content-Type', 'text/html; charset=utf-8');
            response.end('<!doctype html><html><head><meta charset="utf-8"></head><body></body></html>');
        } else if (path === '/') {
            response.setHeader('Content-Type', 'text/html; charset=utf-8');
            response.end(await readFile(new URL('regression.html', import.meta.url)));
        } else if (path === '/request-view.html') {
            const view = await readFile(new URL('Views/Ykc/Yeni.cshtml', app), 'utf8');
            response.setHeader('Content-Type', 'text/html; charset=utf-8');
            response.end(view);
        } else if (path === '/commissioning-table.json') {
            const source = await readFile(new URL('Views/Shared/_DevreyeAlmaTable.cshtml', app), 'utf8');
            const headers = [...source.matchAll(/<th scope="col" class="df-commissioning-col-([a-z]+)">([^<]+)<\/th>/g)]
                .map(([, key, label]) => ({ key, label }));
            const views = await Promise.all(['PersonelPanel/DevreyeAlmalar', 'AdminPanel/DevreyeAlmalar', 'DevreyeAlma/Gecmis']
                .map(async name => ({ name, source: await readFile(new URL(`Views/${name}.cshtml`, app), 'utf8') })));
            response.setHeader('Content-Type', 'application/json');
            response.end(JSON.stringify({ headers, source, sharedViews: views.every(view => view.source.includes('<partial name="_DevreyeAlmaTable" model="Model" />') && !view.source.includes('df-commissioning-table')) }));
        } else if (path === '/commissioning-entry.json') {
            const view = await readFile(new URL('Views/DevreyeAlma/Index.cshtml', app), 'utf8');
            const markup = view.match(/<div class="ykc-request ys-commissioning-request">[\s\S]*?(?=@section Scripts)/)?.[0]
                .replace('@Url.Action("Kaydet", "DevreyeAlma")', '#')
                .replace('@Html.AntiForgeryToken()', '<input type="hidden" name="__RequestVerificationToken" value="test-token">');
            const script = view.match(/<script>\s*([\s\S]*?)<\/script>/)?.[1];
            if (!markup || !script) throw new Error('Commissioning entry fixture source changed');
            response.setHeader('Content-Type', 'application/json');
            response.end(JSON.stringify({ markup, script }));
        } else if (path === '/fonts/bootstrap-icons.woff2') {
            response.setHeader('Content-Type', 'font/woff2');
            response.end(await readFile(new URL('wwwroot/lib/bootstrap-icons/font/fonts/bootstrap-icons.woff2', app)));
        } else if (path === '/appointment-limits.json') {
            const view = await readFile(new URL(files.get('/appointment.js'), app), 'utf8');
            const input = view.match(/<input id="randevuSaati"[^>]+>/)?.[0];
            const limits = Object.fromEntries(['min', 'max', 'step'].map(key => {
                const value = input?.match(new RegExp(` ${key}="([^"]+)"`))?.[1];
                if (!value) throw new Error(`Appointment ${key} missing`);
                return [key, value];
            }));
            response.setHeader('Content-Type', 'application/json');
            response.end(JSON.stringify(limits));
        } else if (path === '/permission-fixture.json') {
            const view = await readFile(new URL('Views/AdminPanel/Yetkiler.cshtml', app), 'utf8');
            const editor = await readFile(new URL(files.get('/permission.js'), app), 'utf8');
            const overview = view.match(/<section class="df-permission-overview"[\s\S]*?<\/details>/)?.[0]
                .replace(/@(toplamPersonel|yetkiTanimliPersonel|tamYetkiliKapsam|sirketKapsami)\b/g, '12');
            const options = [...editor.matchAll(/new \{ Kod = "([^"]+)", Ad = "([^"]+)", Ikon = "([^"]+)" \}/g)]
                .map(([, code, label, icon]) => ({ code, label, icon }));
            if (!overview || options.length !== 9) throw new Error('Permission fixture source changed');
            const groups = [...editor.matchAll(/Baslik = "([^"]+)"/g)].map(([, label]) => label).filter(label => label !== 'Gelişmiş');
            response.setHeader('Content-Type', 'application/json');
            response.end(JSON.stringify({ overview, options, groups, hasDescriptions: editor.includes('permission-option-description') }));
        } else if (files.has(path)) {
            let source = await readFile(new URL(files.get(path), app), 'utf8');
            if (path === '/permission.js' || path === '/appointment.js' || path === '/report.js') {
                source = source.match(/<script>\s*([\s\S]*?)<\/script>/)?.[1];
                if (!source) throw new Error(`Inline script missing: ${path}`);
            }
            if (path === '/report.js') source = source.replace('@icOperasyonGorsun.ToString().ToLowerInvariant()', 'true');
            response.setHeader('Content-Type', path.endsWith('.css') ? 'text/css; charset=utf-8' : 'text/javascript; charset=utf-8');
            response.end(source);
        } else {
            response.writeHead(404).end();
        }
    } catch (error) {
        response.writeHead(500).end(String(error));
    }
}).listen(0, '127.0.0.1', function () {
    console.log(`Browser form regression tests: http://127.0.0.1:${this.address().port}/`);
});
