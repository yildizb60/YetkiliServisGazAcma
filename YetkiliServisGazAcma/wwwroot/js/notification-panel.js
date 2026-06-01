(function () {
    if (window.__dfDropdownInit) return;
    window.__dfDropdownInit = true;

    function closeAll(except) {
        document.querySelectorAll(".df-dropdown-menu.open, .js-notif-panel.open").forEach(function (menu) {
            if (menu !== except) menu.classList.remove("open");
        });
    }

    function initDropdown(dropdown) {
        if (!dropdown || dropdown.dataset.dfInit === "1") return;
        dropdown.dataset.dfInit = "1";

        var btn = dropdown.querySelector("[data-dropdown-toggle]");
        var menu = dropdown.querySelector(".df-dropdown-menu");
        if (!btn || !menu) return;

        btn.addEventListener("click", function (e) {
            e.preventDefault();
            e.stopPropagation();
            var isOpen = menu.classList.contains("open");
            closeAll(menu);
            menu.classList.toggle("open", !isOpen);
        });

        menu.addEventListener("click", function (e) {
            e.stopPropagation();
        });
    }

    function initLegacyNotification(wrap) {
        if (!wrap || wrap.dataset.ntfInit === "1") return;
        wrap.dataset.ntfInit = "1";

        var btn = wrap.querySelector(".js-notif-btn");
        var panel = wrap.querySelector(".js-notif-panel");
        if (!btn || !panel) return;

        btn.addEventListener("click", function (e) {
            e.stopPropagation();
            var isOpen = panel.classList.contains("open");
            closeAll(panel);
            panel.classList.toggle("open", !isOpen);
        });

        panel.addEventListener("click", function (e) {
            e.stopPropagation();
        });
    }

    function initFilters() {
        document.querySelectorAll("#filterContainer").forEach(function (container) {
            if (container.dataset.filterInit === "1") return;
            container.dataset.filterInit = "1";

            container.addEventListener("click", function (e) {
                var btn = e.target.closest("[data-filter]");
                if (!btn) return;

                var filter = btn.getAttribute("data-filter");
                container.querySelectorAll("[data-filter]").forEach(function (item) {
                    item.classList.toggle("active", item === btn);
                    item.classList.toggle("df-btn-primary", item === btn);
                    item.classList.toggle("df-btn-secondary", item !== btn);
                });

                var menu = container.closest(".df-dropdown-menu");
                if (!menu) return;

                menu.querySelectorAll(".notification-item").forEach(function (item) {
                    item.style.display = filter === "all" || item.getAttribute("data-type") === filter ? "" : "none";
                });
            });
        });
    }

    function initMarkAll() {
        document.querySelectorAll(".btn-bildirim-all").forEach(function (btn) {
            if (btn.dataset.readInit === "1") return;
            btn.dataset.readInit = "1";

            btn.addEventListener("click", function () {
                document.querySelectorAll(".red-bildirim-count").forEach(function (badge) {
                    badge.remove();
                });
                document.querySelectorAll(".noti-quantity").forEach(function (badge) {
                    badge.remove();
                });
                document.querySelectorAll(".notification-item").forEach(function (item) {
                    item.classList.add("read");
                });
                btn.remove();
            });
        });
    }

    function initMobileMenu() {
        var btn = document.querySelector(".mob-burger");
        var sidebar = document.querySelector(".sidebar");
        if (!btn || !sidebar || btn.dataset.mobInit === "1") return;
        btn.dataset.mobInit = "1";

        btn.addEventListener("click", function (e) {
            e.stopPropagation();
            sidebar.classList.toggle("open");
        });
    }

    function initAll() {
        document.querySelectorAll(".df-dropdown").forEach(initDropdown);
        document.querySelectorAll(".js-notif-wrap").forEach(initLegacyNotification);
        initFilters();
        initMarkAll();
        initMobileMenu();
    }

    document.addEventListener("click", function () {
        closeAll();
        var sidebar = document.querySelector(".sidebar.open");
        if (sidebar && window.innerWidth <= 900) sidebar.classList.remove("open");
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initAll);
    } else {
        initAll();
    }
})();
