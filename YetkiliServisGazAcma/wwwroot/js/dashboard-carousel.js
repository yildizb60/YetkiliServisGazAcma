(function () {
    document.querySelectorAll('[data-carousel-track]').forEach(function (track) {
        var dashboard = track.closest('[data-dashboard]') || track.closest('.df-dashboard');
        var controls = dashboard && dashboard.querySelector('[data-carousel-controls]');
        if (!controls) return;

        var previous = controls.querySelector('[data-carousel-prev]');
        var next = controls.querySelector('[data-carousel-next]');
        var update = function () {
            var maxScroll = track.scrollWidth - track.clientWidth;
            controls.hidden = maxScroll <= 2;
            previous.disabled = track.scrollLeft <= 2;
            next.disabled = track.scrollLeft >= maxScroll - 2;
        };
        var move = function (direction) {
            var firstCard = track.querySelector('[data-carousel-card], .df-atile');
            if (!firstCard) return;
            var gap = parseFloat(getComputedStyle(track).columnGap) || 0;
            track.scrollBy({ left: direction * (firstCard.getBoundingClientRect().width + gap), behavior: 'smooth' });
        };

        previous.addEventListener('click', function () { move(-1); });
        next.addEventListener('click', function () { move(1); });
        track.addEventListener('scroll', update, { passive: true });
        window.addEventListener('resize', update);
        update();
    });
})();
