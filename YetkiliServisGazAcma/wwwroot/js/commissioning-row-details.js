(() => {
    const records = Array.from(document.querySelectorAll("[data-commissioning-record]"));
    if (!records.length) return;

    const setRecordState = (record, expanded) => {
        const toggle = record.querySelector("[data-commissioning-expand]");
        const detail = toggle ? document.getElementById(toggle.getAttribute("aria-controls")) : null;
        if (!toggle || !detail) return;

        record.classList.toggle("is-expanded", expanded);
        detail.hidden = !expanded;
        toggle.setAttribute("aria-expanded", String(expanded));
        toggle.setAttribute("title", expanded ? "İşlem ayrıntılarını kapat" : "İşlem ayrıntılarını görüntüle");
        toggle.setAttribute("aria-label", expanded ? "İşlem ayrıntılarını kapat" : "İşlem ayrıntılarını görüntüle");

        const icon = toggle.querySelector("i");
        if (icon) {
            icon.classList.toggle("bi-eye", !expanded);
            icon.classList.toggle("bi-eye-slash", expanded);
        }
    };

    records.forEach(record => {
        const toggle = record.querySelector("[data-commissioning-expand]");
        toggle?.addEventListener("click", () => {
            const willOpen = !record.classList.contains("is-expanded");
            records.forEach(other => setRecordState(other, false));
            setRecordState(record, willOpen);
        });
    });
})();
