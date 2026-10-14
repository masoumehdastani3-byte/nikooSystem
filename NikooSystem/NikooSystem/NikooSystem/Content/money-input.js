/* Amount fields (input[data-money]) show thousands separators while typing and accept Persian digits.
   The server receives plain digits: separators are removed from the submitted form data, not from what the user sees. */
(function () {
    'use strict';

    function latin(text) {
        return String(text || '').replace(/[۰-۹٠-٩]/g, function (d) {
            var c = d.charCodeAt(0);
            return String.fromCharCode(48 + (c >= 0x6F0 ? c - 0x6F0 : c - 0x660));
        });
    }

    function raw(text) {
        return latin(text).replace(/\D/g, '');
    }

    function group(digits) {
        return digits.replace(/^0+(?=\d)/, '').replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function isMoney(el) {
        return el && el.tagName === 'INPUT' && el.hasAttribute('data-money');
    }

    function format(input) {
        var before = input.value;
        var focused = document.activeElement === input;
        var digitsBeforeCaret = focused ? raw(before.slice(0, input.selectionStart || 0)).length : 0;
        var after = group(raw(before));
        if (after === before) return;
        input.value = after;
        if (!focused) return;
        var pos = 0, seen = 0;
        while (pos < after.length && seen < digitsBeforeCaret) {
            if (after.charAt(pos) !== ',') seen++;
            pos++;
        }
        input.setSelectionRange(pos, pos);
    }

    function formatAll() {
        Array.prototype.forEach.call(document.querySelectorAll('input[data-money]'), format);
    }

    document.addEventListener('input', function (e) {
        if (isMoney(e.target)) format(e.target);
    });

    if (typeof window.FormDataEvent === 'function') {
        document.addEventListener('formdata', function (e) {
            var names = {};
            Array.prototype.forEach.call(e.target.elements || [], function (el) {
                if (isMoney(el) && el.name) names[el.name] = true;
            });
            Object.keys(names).forEach(function (name) {
                var values = e.formData.getAll(name);
                e.formData.delete(name);
                values.forEach(function (v) { e.formData.append(name, typeof v === 'string' ? raw(v) : v); });
            });
        }, true);
    } else {
        document.addEventListener('submit', function (e) {
            Array.prototype.forEach.call(e.target.elements || [], function (el) {
                if (isMoney(el)) el.value = raw(el.value);
            });
        }, true);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', formatAll);
    else formatAll();
    window.addEventListener('pageshow', formatAll);
})();
