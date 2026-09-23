(function () {
    "use strict";

    var sheet = document.querySelector("[data-admin-form-sheet]");
    if (!sheet) return;

    var sheetTitle = sheet.querySelector("[data-admin-form-sheet-title]");
    var sheetBody = sheet.querySelector("[data-admin-form-sheet-body]");
    var closeButton = sheet.querySelector("[data-admin-form-sheet-close]");
    var activeRequest = 0;
    var closeTimer = 0;

    function setLoading() {
        sheetBody.innerHTML = '<div class="df-directory-sheet-loading" role="status"><span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span>Form hazırlanıyor</span></div>';
    }

    function showError(message) {
        sheetBody.innerHTML = '<div class="df-directory-sheet-error"><i class="bi bi-exclamation-circle" aria-hidden="true"></i><strong>Form açılamadı.</strong><p></p><button type="button" class="df-btn df-btn-secondary df-btn-sm" data-admin-form-sheet-retry><i class="bi bi-arrow-clockwise" aria-hidden="true"></i> Yeniden dene</button></div>';
        sheetBody.querySelector("p").textContent = message || "Bağlantıyı kontrol edip yeniden deneyin.";
    }

    function closeSheet() {
        if (!sheet.open) return;
        activeRequest += 1;
        sheet.classList.remove("is-visible");
        document.documentElement.classList.remove("df-sheet-open");
        window.clearTimeout(closeTimer);
        closeTimer = window.setTimeout(function () {
            closeTimer = 0;
            if (sheet.open) sheet.close();
            sheetBody.replaceChildren();
        }, 180);
    }

    function initializeRoleFields(container) {
        var role = container.querySelector("#rolSelect");
        var companyGroup = container.querySelector("#sirketGroup");
        var companyLabel = container.querySelector("#sirketLabel");
        var company = container.querySelector("#sirketIdSelect");
        var help = container.querySelector("#roleHelp");
        if (!role || !companyGroup || !companyLabel || !company || !help) return;

        function updateRoleFields() {
            var value = (role.value || "").trim();
            var isSystemAdmin = value === "GenelSistemAdmin";
            var isCompanyAccount = value === "YetkiliServis" || value === "SertifikaliFirma";
            var requiresCompany = !isSystemAdmin;

            companyGroup.hidden = !requiresCompany;
            company.disabled = !requiresCompany;
            company.required = requiresCompany;
            if (!requiresCompany) company.value = "";

            companyLabel.textContent = isCompanyAccount ? "Bağlı Dağıtım Şirketi *" : "Şirket *";
            if (isCompanyAccount) {
                help.textContent = "Firma hesabı oluşturulur ve seçilen dağıtım şirketine bağlanır.";
            } else if (value === "Personel") {
                help.textContent = "Personelin ek işlem yetkileri Yetkiler ekranından belirlenir.";
            } else if (value === "SirketAdmin" || value === "SuperAdmin") {
                help.textContent = "Şirket yöneticisi yalnızca bağlı şirketteki kayıtları yönetir.";
            } else {
                help.textContent = "Sistem genelindeki yönetim hesabıdır.";
            }
        }

        role.addEventListener("change", updateRoleFields);
        updateRoleFields();
    }

    function initializePermissionFields(container) {
        var companySelect = container.querySelector("[data-permission-company-select]");
        var companies = Array.from(container.querySelectorAll("[data-permission-company]"));

        function showSelectedCompany() {
            if (!companySelect) return;
            companies.forEach(function (company) {
                var current = company.dataset.permissionCompany === companySelect.value;
                company.classList.toggle("is-current", current);
                if (current) company.open = true;
            });
        }

        if (companySelect) companySelect.addEventListener("change", showSelectedCompany);
        showSelectedCompany();

        companies.forEach(function (company) {
            var companyToggle = company.querySelector(".company-enable");
            var options = Array.from(company.querySelectorAll(".permission-option"));
            var fullAccess = company.querySelector('[data-permission="TAM_YETKI"]');
            var requestView = company.querySelector('[data-permission="YKC_TALEP_GOR"]');
            var ykcDependent = company.querySelectorAll('[data-permission="YKC_ATAMA_YAP"], [data-permission="YKC_FR265_IMZA_ISLEM"], [data-permission="YKC_RAPOR_GOR"]');
            if (!companyToggle) return;

            function refreshSummary() {
                var state = company.querySelector(".company-permission-state");
                if (!state) return;
                var selectedCount = options.filter(function (option) { return option.checked; }).length;
                state.textContent = !companyToggle.checked
                    ? "Yetki kapalı"
                    : fullAccess && fullAccess.checked ? "Tam yetki" : selectedCount + " yetki seçili";
            }

            function refreshCompanyState() {
                options.forEach(function (option) { option.disabled = !companyToggle.checked; });
                refreshSummary();
            }

            function syncYkcPermissions(changed) {
                if (!requestView) return;
                if (changed === requestView && !requestView.checked) {
                    ykcDependent.forEach(function (option) { option.checked = false; });
                } else if (Array.from(ykcDependent).some(function (option) { return option.checked; })) {
                    requestView.checked = true;
                }
            }

            function syncFullAccess(changed) {
                if (!fullAccess) return;
                if (changed === fullAccess && fullAccess.checked) {
                    options.forEach(function (option) { if (option !== fullAccess) option.checked = false; });
                }
                if (changed !== fullAccess && changed && changed.checked) fullAccess.checked = false;
            }

            companyToggle.addEventListener("change", refreshCompanyState);
            options.forEach(function (option) {
                option.addEventListener("change", function () {
                    syncFullAccess(option);
                    syncYkcPermissions(option);
                    refreshSummary();
                });
            });
            refreshCompanyState();
            syncFullAccess(fullAccess);
            syncYkcPermissions();
            refreshSummary();
        });
    }

    function mountableForm(parsed) {
        return parsed.querySelector(".df-admin-form-body form, .permission-editor-body form");
    }

    function mountForm(parsed, sourceUrl) {
        var sourceContainer = parsed.querySelector(".df-admin-form-body, .permission-editor-body");
        var sourceForm = sourceContainer ? sourceContainer.querySelector("form") : null;
        if (!sourceForm) throw new Error("Form içeriği bulunamadı.");

        var form = document.importNode(sourceForm, true);
        var sourceAlert = sourceContainer.querySelector(".df-alert");
        var sourcePermissionHead = parsed.querySelector(".permission-editor-head");
        if (!form.getAttribute("action")) form.setAttribute("action", sourceUrl);
        form.classList.add("df-directory-sheet-form");
        if (sourcePermissionHead) {
            var permissionHead = document.importNode(sourcePermissionHead, true);
            permissionHead.classList.add("df-permission-sheet-context");
            form.prepend(permissionHead);
        }
        if (sourceAlert) form.prepend(document.importNode(sourceAlert, true));
        sheetBody.replaceChildren(form);
        initializeRoleFields(sheetBody);
        initializePermissionFields(sheetBody);

        var firstField = form.querySelector("select:not([hidden]), input:not([type='hidden']):not(:disabled), textarea:not(:disabled)");
        if (firstField) firstField.focus();
    }

    async function loadForm(url, requestId) {
        var response = await fetch(url, {
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) throw new Error("Form sunucudan alınamadı.");
        var html = await response.text();
        if (requestId !== activeRequest) return;
        mountForm(new DOMParser().parseFromString(html, "text/html"), url);
    }

    async function openSheet(trigger) {
        var url = trigger.getAttribute("data-sheet-url") || trigger.getAttribute("href");
        if (!url) return;

        window.clearTimeout(closeTimer);
        closeTimer = 0;
        var requestId = ++activeRequest;
        sheet.dataset.activeUrl = url;
        sheet.dataset.variant = trigger.getAttribute("data-sheet-variant") || "account";
        sheet.classList.toggle("df-permission-sheet", sheet.dataset.variant === "permission");
        sheetTitle.textContent = trigger.getAttribute("data-sheet-title") || "Kayıt İşlemi";
        setLoading();

        if (!sheet.open) sheet.showModal();
        document.documentElement.classList.add("df-sheet-open");
        window.requestAnimationFrame(function () { sheet.classList.add("is-visible"); });

        try {
            await loadForm(url, requestId);
        } catch (error) {
            if (requestId !== activeRequest) return;
            showError(error instanceof Error ? error.message : "Form yüklenemedi.");
        }
    }

    async function submitForm(form, submitter) {
        if (!form.reportValidity()) return;

        var submitButton = submitter || form.querySelector('[type="submit"]');
        var originalHtml = submitButton ? submitButton.innerHTML : "";
        if (submitButton) {
            submitButton.disabled = true;
            submitButton.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Kaydediliyor';
        }

        try {
            var response = await fetch(form.action || sheet.dataset.activeUrl, {
                method: (form.method || "post").toUpperCase(),
                body: new FormData(form),
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok) throw new Error("Kayıt işlemi tamamlanamadı.");

            var html = await response.text();
            var parsed = new DOMParser().parseFromString(html, "text/html");
            if (response.redirected && !mountableForm(parsed)) {
                window.location.assign(response.url);
                return;
            }

            if (mountableForm(parsed)) {
                mountForm(parsed, form.action || sheet.dataset.activeUrl);
                return;
            }

            window.location.reload();
        } catch (error) {
            var alert = form.querySelector(".df-alert");
            if (!alert) {
                alert = document.createElement("div");
                alert.className = "df-alert df-alert-danger";
                form.prepend(alert);
            }
            alert.textContent = error instanceof Error ? error.message : "Kayıt işlemi tamamlanamadı.";
            if (submitButton) {
                submitButton.disabled = false;
                submitButton.innerHTML = originalHtml;
            }
        }
    }

    document.addEventListener("click", function (event) {
        var trigger = event.target.closest(".js-admin-form-sheet-trigger");
        if (!trigger) return;
        event.preventDefault();
        openSheet(trigger);
    });

    sheetBody.addEventListener("click", function (event) {
        var cancel = event.target.closest(".df-admin-form-actions a, .permission-editor-actions a");
        if (cancel) {
            event.preventDefault();
            closeSheet();
            return;
        }

        var retry = event.target.closest("[data-admin-form-sheet-retry]");
        if (!retry || !sheet.dataset.activeUrl) return;
        setLoading();
        var requestId = ++activeRequest;
        loadForm(sheet.dataset.activeUrl, requestId).catch(function (error) {
            if (requestId === activeRequest) showError(error instanceof Error ? error.message : "Form yüklenemedi.");
        });
    });

    sheetBody.addEventListener("submit", function (event) {
        var form = event.target.closest("form");
        if (!form) return;
        event.preventDefault();
        submitForm(form, event.submitter);
    });

    closeButton.addEventListener("click", closeSheet);
    sheet.addEventListener("cancel", function (event) {
        event.preventDefault();
        closeSheet();
    });
    sheet.addEventListener("click", function (event) {
        if (event.target === sheet) closeSheet();
    });
})();
