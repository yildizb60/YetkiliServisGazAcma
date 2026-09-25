(() => {
    const shell = document.querySelector('[data-registration-shell]');
    const form = shell?.querySelector('[data-registration-form]');
    if (!shell || !form) return;

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
                current.classList.remove('is-active', 'is-device-leaving', 'is-overlays-leaving');
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
            if (!reduceMotion && slides.length > 1) timer = window.setInterval(() => transitionTo(activeIndex + 1), 5200);
        };

        document.addEventListener('visibilitychange', () => document.hidden ? stop() : start());
        start();
    }

    const panels = Array.from(form.querySelectorAll('[data-registration-step]'));
    const stepButtons = Array.from(form.querySelectorAll('[data-registration-step-target]'));
    const tcInput = form.querySelector('#TcKimlikNo');
    const tcError = form.querySelector('#tc-kimlik-hata');
    let tcTouched = false;

    function validateTc(showMessage = false) {
        if (!tcInput) return;
        const valid = /^[0-9]{11}$/.test(tcInput.value.trim());
        const message = valid ? '' : 'T.C. kimlik no 11 haneli ve sayısal olmalıdır.';
        tcInput.setCustomValidity(message);
        const visible = !valid && (showMessage || tcTouched || tcInput.value.length >= 11);
        tcError.textContent = visible ? message : '';
        tcError.hidden = !visible;
        tcInput.setAttribute('aria-invalid', String(visible));
    }

    tcInput?.addEventListener('blur', () => { tcTouched = true; validateTc(); });
    tcInput?.addEventListener('input', () => validateTc());
    validateTc();

    function firstInvalid(panel) {
        return Array.from(panel.querySelectorAll('input, select')).find(control => !control.checkValidity());
    }

    function showStep(step, validatePrevious = false) {
        const previousPanel = panels.find(panel => panel.dataset.registrationStep === '1');
        if (step === 2 && validatePrevious) {
            validateTc(true);
            const invalid = firstInvalid(previousPanel);
            if (invalid) {
                invalid.reportValidity();
                return false;
            }
        }

        panels.forEach(panel => {
            const active = Number(panel.dataset.registrationStep) === step;
            panel.hidden = !active;
        });
        stepButtons.forEach(button => {
            const buttonStep = Number(button.dataset.registrationStepTarget);
            if (buttonStep === step) button.setAttribute('aria-current', 'step');
            else button.removeAttribute('aria-current');
            button.classList.toggle('is-complete', buttonStep < step);
        });
        return true;
    }

    form.querySelector('[data-registration-next]')?.addEventListener('click', () => showStep(2, true));
    form.querySelector('[data-registration-back]')?.addEventListener('click', () => showStep(1));
    stepButtons.forEach(button => button.addEventListener('click', () => {
        const step = Number(button.dataset.registrationStepTarget);
        showStep(step, step === 2);
    }));

    form.addEventListener('submit', event => {
        validateTc(true);
        const invalid = Array.from(form.elements).find(control => typeof control.checkValidity === 'function' && !control.checkValidity());
        if (!invalid) return;
        event.preventDefault();
        const panel = invalid.closest('[data-registration-step]');
        showStep(Number(panel?.dataset.registrationStep || 1));
        window.requestAnimationFrame(() => invalid.reportValidity());
    });

    const citySelect = form.querySelector('[data-firma-kodu-select]');
    const cityCode = form.querySelector('[data-firma-kodu-target]');
    const updateCompanyCode = () => {
        const option = citySelect?.options[citySelect.selectedIndex];
        const code = option?.dataset.firmaKodu || '';
        const names = {
            CORUMGAZ: 'Çorumgaz Doğalgaz A.Ş.',
            KARGAZ: 'Kargaz',
            SURMELIGAZ: 'Sürmeligaz',
            MARMARAGAZ_YALOVA: 'Marmaragaz',
            MARMARAGAZ_CORLU: 'Marmaragaz'
        };
        cityCode.textContent = code ? `Dağıtım şirketi: ${names[code] || option.textContent}` : '';
        cityCode.hidden = !code;
    };
    citySelect?.addEventListener('change', updateCompanyCode);
    updateCompanyCode();

    const brandInputs = Array.from(form.querySelectorAll('.marka-cb'));
    const selectAll = form.querySelector('#tumMarkalar');
    const updateSelectAll = () => {
        if (!selectAll) return;
        selectAll.checked = brandInputs.length > 0 && brandInputs.every(input => input.checked);
        selectAll.indeterminate = brandInputs.some(input => input.checked) && !selectAll.checked;
    };
    selectAll?.addEventListener('change', () => {
        brandInputs.forEach(input => { input.checked = selectAll.checked; });
        updateSelectAll();
    });
    brandInputs.forEach(input => input.addEventListener('change', updateSelectAll));
    updateSelectAll();

    form.querySelector('#markaArama')?.addEventListener('input', event => {
        const query = event.target.value.toLocaleLowerCase('tr-TR').trim();
        form.querySelectorAll('.marka-item').forEach(item => {
            item.hidden = !item.textContent.toLocaleLowerCase('tr-TR').includes(query);
        });
    });

    form.querySelectorAll('[data-password-toggle]').forEach(button => {
        button.addEventListener('click', () => {
            const input = form.querySelector(`#${button.dataset.passwordToggle}`);
            if (!input) return;
            const visible = input.type === 'text';
            input.type = visible ? 'password' : 'text';
            button.setAttribute('aria-label', visible ? 'Şifreyi göster' : 'Şifreyi gizle');
            button.querySelector('i')?.classList.toggle('bi-eye', visible);
            button.querySelector('i')?.classList.toggle('bi-eye-slash', !visible);
        });
    });

    showStep(Number(form.dataset.initialStep || 1));
    initShowcase();
})();
