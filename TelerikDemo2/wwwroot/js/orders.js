// Shared by the offset and cursor Orders pages. Expects window.ordersConfig.detailsUrl (set in
// _OrdersChrome) and a Telerik Grid named "grid".

function forgeryToken() {
    return kendo.antiForgeryTokens();
}

function getGrid() {
    return $("#grid").data("kendoGrid");
}

function filterValues() {
    var combo = $("#customerFilter").data("kendoComboBox");
    return {
        search: $("#searchBox").val(),
        customerId: combo ? combo.value() : ""
    };
}

function showMessage(title, html) {
    var dlg = $("#messageDialog").data("kendoDialog");
    dlg.title(title);
    dlg.content(html);
    dlg.open();
}

function showDetails(e) {
    e.preventDefault();
    var dataItem = this.dataItem($(e.currentTarget).closest("tr"));
    var win = $("#detailsWindow").data("kendoWindow");
    win.title("Order #" + dataItem.OrderId);
    refreshDetails(dataItem.OrderId);
    win.center().open();
}

function refreshDetails(id) {
    $("#detailsWindow").data("kendoWindow").refresh({ url: window.ordersConfig.detailsUrl, data: { id: id }, type: "GET" });
}

var STATUS_NAMES = ["New", "Processing", "Shipped", "Cancelled"];
var STATUS_COLORS = ["var(--st-new)", "var(--st-processing)", "var(--st-shipped)", "var(--st-cancelled)"];

function statusName(status) {
    return STATUS_NAMES[status] || status;
}

function loadSummary() {
    fetch('/api/orders/summary', { credentials: 'same-origin' })
        .then(function (r) { return r.json(); })
        .then(function (rows) {
            var total = rows.reduce(function (a, x) { return a + x.count; }, 0) || 1;
            $("#summary").html(rows.map(function (x) {
                var pct = Math.round(x.count / total * 100);
                return '<div class="stat" style="--c:' + STATUS_COLORS[x.status] + '">' +
                       '<div class="stat__label"><span class="stat__dot"></span>' + STATUS_NAMES[x.status] + '</div>' +
                       '<div class="stat__value">' + x.count.toLocaleString() + '</div>' +
                       '<div class="stat__sub">' + kendo.toString(x.totalValue, "c0") + ' &middot; ' + pct + '% of orders</div>' +
                       '<div class="stat__bar"><span style="width:' + pct + '%"></span></div></div>';
            }).join(""));
        });
}

function toast(message, type) {
    $("#toast").data("kendoNotification").show(message, type || "success");
}

function notifyResult(e) {
    if (!e.response || e.response.Errors) return;
    var text = { create: "Order created", update: "Order saved", destroy: "Order deleted" }[e.type];
    if (text) toast(text, "success");
}

// Shows server-side rule errors under the matching fields of the open popup.
// Returns the messages that have no field so the caller can show them elsewhere.
function showFieldErrors(container, errors) {
    var orphans = [];
    $.each(errors, function (key, value) {
        var messages = (value && value.errors) || [];
        if (!messages.length) return;
        var input = container.find('[name="' + key + '"]').first();
        var field = input.closest(".k-edit-field");
        if (!field.length) {
            orphans = orphans.concat(messages);
            return;
        }
        var holder = field.find('[data-valmsg-for="' + key + '"]');
        if (!holder.length) holder = $('<span class="field-validation-error" data-valmsg-for="' + key + '"></span>').appendTo(field);
        holder.text(messages.join(" ")).removeClass("k-hidden field-validation-valid").addClass("field-validation-error");
        field.find(".k-input, .k-picker, .k-textarea").addClass("k-invalid");

    });
    return orphans;
}

// Server errors. With the popup open, cancel the rebind Telerik does after a failed save (it would
// close the popup) and show the errors on the fields; otherwise (e.g. Delete) show a dialog.
function onGridError(e) {
    var grid = getGrid();
    if (e.errors) {
        var conflict = Object.keys(e.errors).filter(function (k) { return k.toLowerCase() === "rowversion"; })[0];
        if (conflict) {
            // Someone else changed the order: discard the stale edit and reload.
            if (grid.editable) grid.cancelRow();
            showMessage("Order changed", e.errors[conflict].errors.join("<br/>"));
            grid.dataSource.read();
        } else if (grid.editable) {
            grid.one("dataBinding", function (ev) { ev.preventDefault(); });
            var popup = grid.editable.element;
            // Save stays disabled after the cancelled rebind, so re-enable it.
            setTimeout(function () {
                popup.find(".k-disabled").removeClass("k-disabled").removeAttr("disabled").attr("aria-disabled", "false");
                popup.closest(".k-window").find(".k-disabled").removeClass("k-disabled").attr("aria-disabled", "false");
            }, 0);
            var orphans = showFieldErrors(popup, e.errors);
            if (orphans.length) showMessage("Cannot save", orphans.join("<br/>"));
        } else {
            var messages = [];
            $.each(e.errors, function (key, value) {
                if (value && value.errors) messages = messages.concat(value.errors);
            });
            showMessage("Cannot complete action", messages.join("<br/>"));
            grid.dataSource.read();
        }
    } else if (e.xhr && (e.xhr.status === 401 || e.xhr.status === 403)) {
        showMessage("Not allowed", "You do not have permission to do that.");
        grid.dataSource.read();
    }
}

function initOrdersFilterBar(reload) {
    loadSummary();
    // Clear the previous attempt's server errors when saving again.
    getGrid().bind("save", function (e) {
        if (!e.container) return;
        e.container.find(".field-validation-error[data-valmsg-for]").text("").removeClass("field-validation-error");
        e.container.find(".k-invalid").removeClass("k-invalid");
    });
    $("#applyFilter").on("click", reload);
    $("#searchBox").on("keydown", function (e) { if (e.key === "Enter") reload(); });
    $("#customerFilter").data("kendoComboBox").bind("change", reload);
    $("#clearFilter").on("click", function () {
        $("#searchBox").val("");
        $("#customerFilter").data("kendoComboBox").value("");
        reload();
    });
    getGrid().dataSource.bind("sync", loadSummary);
}

function onRemove(e) {
    e.preventDefault();
    var model = e.model;
    var dlg = $("<div></div>").kendoConfirm({
        title: "Delete order #" + model.OrderId + "?",
        content: "This permanently removes the order for <strong>" + kendo.htmlEncode(model.CustomerName || "this customer") + "</strong>. This cannot be undone.",
        messages: { okText: "Delete order", cancel: "Keep order" },
        close: function () { this.destroy(); }
    }).data("kendoConfirm");
    dlg.wrapper.addClass("is-danger");
    dlg.open().result.done(function () {
        var ds = getGrid().dataSource;
        ds.remove(model);
        ds.sync();
    });
}

// Kendo windows are positioned in page coordinates, so they scroll away with the page.
// Lock page scrolling while a modal overlay is showing.
(function () {
    var queued = false;
    function sync() {
        queued = false;
        var open = Array.prototype.some.call(document.querySelectorAll(".k-overlay"), function (o) {
            return o.getClientRects().length > 0 && getComputedStyle(o).visibility !== "hidden";
        });
        document.documentElement.classList.toggle("modal-open", open);
    }
    new MutationObserver(function () {
        if (!queued) { queued = true; requestAnimationFrame(sync); }
    }).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ["style", "class"] });
})();
