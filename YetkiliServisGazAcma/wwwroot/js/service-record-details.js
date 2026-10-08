(() => {
    const records = Array.from(document.querySelectorAll("[data-service-record]"));
    const interactive = "a, button, input, select, textarea, label, form, details, summary, [contenteditable], [role='button'], [role='link']";

    const setExpanded = (record, expanded) => {
        const toggle = record.querySelector("[data-service-expand]");
        const detail = record.querySelector(".df-service-record-detail");
        if (!toggle || !detail) return;

        record.classList.toggle("is-expanded", expanded);
        toggle.setAttribute("aria-expanded", String(expanded));
        toggle.title = expanded ? "Satır ayrıntılarını kapat" : "Satır ayrıntılarını aç";
        toggle.querySelector(".visually-hidden").textContent = toggle.title;
        detail.hidden = !expanded;
        detail.setAttribute("aria-hidden", String(!expanded));
    };

    records.forEach(record => {
        const row = record.querySelector(".df-service-row");
        if (!row) return;

        row.addEventListener("click", event => {
            const target = event.target;
            if (!(target instanceof Element)) return;
            if (target.closest(interactive) && !target.closest("[data-service-expand]")) return;

            const expanded = !record.classList.contains("is-expanded");
            records.forEach(other => setExpanded(other, other === record && expanded));
        });
    });
})();
