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
        if (!toggle.dataset.recordLabel) {
            toggle.dataset.recordLabel = (toggle.getAttribute("aria-label") || "")
                .replace(/\s*işlem ayrıntılarını (görüntüle|kapat)$/i, "")
                .trim();
        }
        const action = expanded ? "kapat" : "görüntüle";
        toggle.setAttribute("aria-label", toggle.dataset.recordLabel
            ? `${toggle.dataset.recordLabel} işlem ayrıntılarını ${action}`
            : `İşlem ayrıntılarını ${action}`);

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

    const linkedDetail = document.getElementById(window.location.hash.slice(1));
    if (linkedDetail?.matches(".df-commissioning-detail-row")) {
        const record = records.find(item => item.querySelector("[data-commissioning-expand]")?.getAttribute("aria-controls") === linkedDetail.id);
        if (record) {
            setRecordState(record, true);
            record.scrollIntoView({ block: "start" });
        }
    }
})();
