// Loader used by every page (see site.css for the look).
// Full-page loader for anything that talks to the server. It only appears if the work takes longer than
// a moment, so quick calls don't flash it, and it blocks clicks so a save or delete can't be sent twice.
var loader = (function () {
    var active = 0, timer = null, element = null;

    function ensure() {
        if (!element) {
            element = document.createElement("div");
            element.className = "app-loader";
            element.setAttribute("role", "status");
            element.setAttribute("aria-live", "polite");
            element.hidden = true;
            element.innerHTML = '<div class="app-loader__box"><span class="app-loader__spinner"></span><span>Working…</span></div>';
            document.body.appendChild(element);
        }
        return element;
    }

    // Returns a function that marks this piece of work as finished (safe to call more than once).
    function start() {
        var finished = false;
        if (++active === 1) {
            timer = setTimeout(function () { ensure().hidden = false; }, 150);
        }
        return function () {
            if (finished) return;
            finished = true;
            if (--active === 0) {
                clearTimeout(timer);
                if (element) element.hidden = true;
            }
        };
    }

    return { start: start };
})();

// Plain form posts (sign in, sign out) navigate away, so the loader simply stays up until the next page loads.
// Skipped when something else (a widget, validation) has already taken over the submit.
document.addEventListener("submit", function (event) {
    setTimeout(function () {
        if (event.defaultPrevented) return;
        loader.start();
        var button = event.target.querySelector && event.target.querySelector("button[type=submit]");
        if (button) button.disabled = true;   // no double submit
    }, 0);
});

// Coming back with the browser's Back button can restore a page that still shows the loader.
window.addEventListener("pageshow", function (event) {
    if (event.persisted) {
        var el = document.querySelector(".app-loader");
        if (el) el.hidden = true;
        document.querySelectorAll("button[type=submit]").forEach(function (b) { b.disabled = false; });
    }
});
