(() => {
    const shell = document.querySelector('.login-shell');
    if (!shell) return;
    let busy = false;

    function initShowcase() {
        const showcase = shell.querySelector('[data-login-showcase]');
        if (!showcase) return;
        const slides = Array.from(showcase.querySelectorAll('[data-login-slide]'));
        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        let activeIndex = Math.max(0, slides.findIndex(slide => slide.classList.contains('is-active')));
        let timer = null;
        let cleanupTimer = null;
        let phaseTimers = [];
        let transitioning = false;

        const schedule = (callback, delay) => {
            const phaseTimer = window.setTimeout(callback, delay);
            phaseTimers.push(phaseTimer);
        };

        const transitionTo = nextIndex => {
            const normalizedIndex = (nextIndex + slides.length) % slides.length;
            if (normalizedIndex === activeIndex || transitioning) return;

            const current = slides[activeIndex];
            const next = slides[normalizedIndex];
            transitioning = true;
            window.clearTimeout(cleanupTimer);
            phaseTimers.forEach(window.clearTimeout);
            phaseTimers = [];
            next.classList.remove('is-leaving');
            next.classList.add('is-entering');
            next.setAttribute('aria-hidden', 'false');
            current.classList.add('is-device-leaving');

            schedule(() => current.classList.add('is-overlays-leaving'), 230);
            schedule(() => {
                current.classList.remove('is-active');
                current.classList.remove('is-device-leaving', 'is-overlays-leaving');
                current.classList.add('is-leaving');
                current.setAttribute('aria-hidden', 'true');
                next.classList.remove('is-entering');
                next.classList.add('is-active');
                activeIndex = normalizedIndex;

                cleanupTimer = window.setTimeout(() => {
                    current.classList.remove('is-leaving');
                    transitioning = false;
                }, 640);
            }, 650);
        };
        const stop = () => {
            if (timer !== null) window.clearInterval(timer);
            timer = null;
        };
        const start = () => {
            stop();
            if (!reduceMotion && slides.length > 1) {
                timer = window.setInterval(() => transitionTo(activeIndex + 1), 5200);
            }
        };
        document.addEventListener('visibilitychange', () => document.hidden ? stop() : start());
        slides.forEach((slide, index) => {
            const active = index === activeIndex;
            slide.classList.toggle('is-active', active);
            slide.classList.remove('is-entering', 'is-leaving', 'is-device-leaving', 'is-overlays-leaving');
            slide.setAttribute('aria-hidden', String(!active));
        });
        start();
    }

    function selectMode(mode, fillDemo) {
        const tabs = Array.from(shell.querySelectorAll('[data-login-mode]'));
        const tab = tabs.find(item => item.dataset.loginMode === mode);
        if (!tab) return;
        tabs.forEach(item => { item.setAttribute('aria-selected', String(item === tab)); item.tabIndex = item === tab ? 0 : -1; });
        shell.querySelector('#login-panel').setAttribute('aria-labelledby', tab.id);
        const personel = mode === 'personel';
        shell.querySelector('label[for="kullanici-adi"]').textContent = personel ? 'E-posta Adresi' : mode === 'servis' ? 'VKN, T.C. Kimlik No veya E-posta' : 'VKN veya E-posta';
        shell.querySelector('#kullanici-adi').placeholder = personel ? 'E-posta adresiniz' : mode === 'servis' ? 'Kayıtlı giriş bilginiz' : 'VKN veya e-posta adresiniz';
        shell.querySelector('.login-registration').hidden = mode !== 'servis';
        if (fillDemo && tab.dataset.demoUser) {
            shell.querySelector('#kullanici-adi').value = tab.dataset.demoUser;
            shell.querySelector('#sifre').value = tab.dataset.demoPassword || '';
        }
    }
    shell.addEventListener('click', event => {
        const tab = event.target.closest('[data-login-mode]');
        if (tab) selectMode(tab.dataset.loginMode, true);
        const demo = event.target.closest('.demo-login-btn');
        if (demo) {
            event.preventDefault();
            const email = demo.dataset.demoUser || '';
            selectMode(email.includes('sertifikalifirma') ? 'firma' : email.includes('test.servis') ? 'servis' : 'personel', false);
            const userInput = shell.querySelector('#kullanici-adi');
            const passwordInput = shell.querySelector('#sifre');
            if (userInput) {
                userInput.value = email;
                userInput.dispatchEvent(new Event('input', { bubbles: true }));
            }
            if (passwordInput) {
                passwordInput.value = demo.dataset.demoPassword || '';
                passwordInput.dispatchEvent(new Event('input', { bubbles: true }));
            }
            demo.closest('details')?.removeAttribute('open');
            userInput?.focus({ preventScroll: true });
        }
    });
    document.addEventListener('click', event => {
        shell.querySelectorAll('.demo-logins[open]').forEach(details => {
            if (!details.contains(event.target)) details.removeAttribute('open');
        });
    });
    shell.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        const details = event.target.closest('.demo-logins[open]');
        if (!details) return;
        details.removeAttribute('open');
        details.querySelector('summary')?.focus({ preventScroll: true });
    });
    shell.addEventListener('keydown', event => {
        const tab = event.target.closest('[data-login-mode]');
        if (!tab) return;
        const tabs = Array.from(shell.querySelectorAll('[data-login-mode]'));
        const index = tabs.indexOf(tab);
        const next = { ArrowRight: (index + 1) % tabs.length, ArrowLeft: (index + tabs.length - 1) % tabs.length, Home: 0, End: tabs.length - 1 }[event.key];
        if (next === undefined) return;
        event.preventDefault();
        tabs[next].focus();
        selectMode(tabs[next].dataset.loginMode, true);
    });
    shell.addEventListener('change', event => {
        if (!event.target.matches('[name="sirketId"]')) return;
        shell.querySelectorAll('.company-select-option').forEach(option => option.classList.toggle('is-selected', option.querySelector('input').checked));
    });
    // Keep login, OTP and company selection in the same surface; server POSTs retain all authorization checks.
    shell.addEventListener('submit', async event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.method.toLowerCase() !== 'post') return;
        event.preventDefault();
        if (busy) return;
        const action = new URL(form.action);
        if (action.origin !== location.origin) return;
        busy = true;
        const submit = form.querySelector('[type="submit"]');
        if (submit) submit.disabled = true;
        form.setAttribute('aria-busy', 'true');
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 60000);
        const mode = shell.querySelector('[data-login-mode][aria-selected="true"]')?.dataset.loginMode;
        try {
            const response = await fetch(action, { method: 'POST', body: new FormData(form), signal: controller.signal, credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) throw new Error('İşlem tamamlanamadı. Lütfen sayfayı yenileyip yeniden deneyin.');
            const destination = new URL(response.url);
            if (destination.origin !== location.origin) throw new Error('Güvenli giriş yönlendirmesi alınamadı.');
            const html = await response.text();
            const documentResult = new DOMParser().parseFromString(html, 'text/html');
            const nextShell = documentResult.querySelector('.login-shell');
            const nextCard = nextShell?.querySelector('.login-card');
            if (!nextCard) { location.assign(destination.href); return; }
            if (nextShell.classList.contains('company-select-shell')) {
                shell.className = nextShell.className;
                shell.querySelector('.login-card')?.replaceWith(document.importNode(nextCard, true));
            } else {
                shell.querySelector('.login-card').replaceWith(document.importNode(nextCard, true));
            }
            // Scripts in returned HTML are deliberately not executed.
            selectMode(mode || 'firma', false);
            const heading = shell.querySelector('.login-card h1');
            if (heading) {
                heading.tabIndex = -1;
                heading.classList.add('login-programmatic-focus');
                requestAnimationFrame(() => heading.focus({ preventScroll: true }));
            }
        } catch (error) {
            let message = shell.querySelector('[data-login-network-error]');
            if (!message) {
                message = document.createElement('p');
                message.className = 'login-message is-error';
                message.dataset.loginNetworkError = '';
                message.setAttribute('role', 'alert');
                shell.querySelector('.login-card').append(message);
            }
            message.textContent = error.name === 'AbortError' ? 'İşlem zaman aşımına uğradı. Lütfen sayfayı yenileyin.' : error.message;
        } finally {
            clearTimeout(timeout);
            busy = false;
            if (submit) submit.disabled = false;
            form.removeAttribute('aria-busy');
        }
    });
    initShowcase();
    selectMode('firma', true);
})();
