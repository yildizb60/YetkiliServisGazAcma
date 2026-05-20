(function () {
    if (window.__ntfPanelInit) return;
    window.__ntfPanelInit = true;

    function closeAll() {
        document.querySelectorAll(".js-notif-panel.open").forEach(function (x) {
            x.classList.remove("open");
        });
    }

    function initWrap(wrap) {
        if (!wrap || wrap.dataset.ntfInit === "1") return;
        wrap.dataset.ntfInit = "1";

        var btn = wrap.querySelector(".js-notif-btn");
        var panel = wrap.querySelector(".js-notif-panel");
        if (!btn || !panel) return;

        btn.addEventListener("click", function (e) {
            e.stopPropagation();
            var isOpen = panel.classList.contains("open");
            closeAll();
            if (!isOpen) panel.classList.add("open");
        });

        panel.addEventListener("click", function (e) {
            e.stopPropagation();
        });
    }

    function initAll() {
        document.querySelectorAll(".js-notif-wrap").forEach(initWrap);
    }

    document.addEventListener("click", function () {
        closeAll();
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initAll);
    } else {
        initAll();
    }
})();
