(function () {
    if (window.__dfDropdownInit) return;
    window.__dfDropdownInit = true;

    function closeAll(except) {
        document.querySelectorAll(".df-dropdown-menu.open, .js-notif-panel.open").forEach(function (menu) {
            if (menu === except) return;
            menu.classList.remove("open");
            var toggle = menu.parentElement?.querySelector("[data-dropdown-toggle], .js-notif-btn");
            toggle?.setAttribute("aria-expanded", "false");
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
            btn.setAttribute("aria-expanded", String(!isOpen));
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
            btn.setAttribute("aria-expanded", String(!isOpen));
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

    function notificationStorageKey(wrap) {
        return "panel-notifications-read:" + (wrap?.dataset.notificationScope || "anonymous");
    }

    function readNotificationKeys(wrap) {
        try {
            var parsed = JSON.parse(window.localStorage.getItem(notificationStorageKey(wrap)) || "[]");
            return new Set(Array.isArray(parsed) ? parsed : []);
        } catch {
            return new Set();
        }
    }

    function saveNotificationKeys(wrap, keys) {
        try {
            window.localStorage.setItem(notificationStorageKey(wrap), JSON.stringify(Array.from(keys).slice(-100)));
        } catch { }
    }

    function updateNotificationCount(wrap) {
        if (!wrap) return;
        var unread = wrap.querySelectorAll(".notification-item:not(.read)").length;
        var topBadge = wrap.querySelector(".red-bildirim-count");
        var quantity = wrap.querySelector(".noti-quantity");
        var count = wrap.querySelector(".bildirim-count");
        var markAll = wrap.querySelector(".btn-bildirim-all");

        if (topBadge) {
            topBadge.textContent = unread > 99 ? "99+" : String(unread);
            topBadge.hidden = unread === 0;
        }
        if (count) count.textContent = unread > 99 ? "99+" : String(unread);
        if (quantity) quantity.hidden = unread === 0;
        if (markAll) markAll.hidden = unread === 0;
    }

    function markNotificationRead(item, wrap, keys) {
        if (!item || item.classList.contains("read")) return;
        item.classList.add("read");
        var key = item.dataset.notificationKey;
        if (key) keys.add(key);
        saveNotificationKeys(wrap, keys);
        updateNotificationCount(wrap);
    }

    function initNotifications() {
        document.querySelectorAll("[data-notification-scope]").forEach(function (wrap) {
            if (wrap.dataset.readInit === "1") return;
            wrap.dataset.readInit = "1";
            var keys = readNotificationKeys(wrap);
            wrap.querySelectorAll(".notification-item").forEach(function (item) {
                if (keys.has(item.dataset.notificationKey)) item.classList.add("read");
                item.querySelector(".js-notif-redirect")?.addEventListener("click", function () {
                    markNotificationRead(item, wrap, keys);
                });
            });

            wrap.querySelector(".btn-bildirim-all")?.addEventListener("click", function () {
                wrap.querySelectorAll(".notification-item").forEach(function (item) {
                    item.classList.add("read");
                    if (item.dataset.notificationKey) keys.add(item.dataset.notificationKey);
                });
                saveNotificationKeys(wrap, keys);
                updateNotificationCount(wrap);
            });
            updateNotificationCount(wrap);
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

    function initSidebarCollapse() {
        var button = document.querySelector("[data-sidebar-collapse]");
        var sidebar = document.querySelector(".sidebar");
        if (!button || !sidebar || button.dataset.collapseInit === "1") return;
        button.dataset.collapseInit = "1";

        var storageKey = "panel-sidebar-collapsed";
        var isCollapsed = function () {
            return document.body.classList.contains("sidebar-collapsed");
        };
        var setHoverExpanded = function (expanded) {
            document.body.classList.toggle("sidebar-hover-expanded", isCollapsed() && expanded);
        };
        var applyState = function (collapsed) {
            document.body.classList.toggle("sidebar-collapsed", collapsed);
            if (!collapsed) document.body.classList.remove("sidebar-hover-expanded");
            button.setAttribute("aria-expanded", collapsed ? "false" : "true");
            button.title = collapsed ? "Menüyü genişlet" : "Menüyü daralt";
            var icon = button.querySelector("i");
            if (icon) icon.className = collapsed ? "bi bi-chevron-right" : "bi bi-chevron-left";
        };

        var collapsed = false;
        try { collapsed = window.localStorage.getItem(storageKey) === "1"; } catch { }
        applyState(collapsed);

        sidebar.addEventListener("mouseenter", function () {
            setHoverExpanded(true);
        });

        sidebar.addEventListener("mouseleave", function () {
            setHoverExpanded(false);
        });

        sidebar.addEventListener("focusin", function () {
            setHoverExpanded(true);
        });

        sidebar.addEventListener("focusout", function () {
            window.setTimeout(function () {
                if (!sidebar.contains(document.activeElement)) setHoverExpanded(false);
            }, 0);
        });

        sidebar.addEventListener("click", function (event) {
            if (isCollapsed() && event.target.closest("a[href]")) setHoverExpanded(false);
        });

        button.addEventListener("click", function () {
            collapsed = !isCollapsed();
            applyState(collapsed);
            try { window.localStorage.setItem(storageKey, collapsed ? "1" : "0"); } catch { }
        });
    }

    function initAll() {
        document.querySelectorAll(".df-dropdown").forEach(initDropdown);
        document.querySelectorAll(".js-notif-wrap").forEach(initLegacyNotification);
        initFilters();
        initNotifications();
        initMobileMenu();
        initSidebarCollapse();
    }

    document.addEventListener("click", function (event) {
        closeAll();
        var sidebar = document.querySelector(".sidebar.open");
        if (sidebar && window.innerWidth <= 900
            && (!sidebar.contains(event.target) || event.target.closest("a[href]"))) sidebar.classList.remove("open");
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initAll);
    } else {
        initAll();
    }
})();
