(function (global) {
    'use strict';

    function enDigits(s) {
        return String(s == null ? '' : s).replace(/[۰-۹]/g, function (d) {
            return '۰۱۲۳۴۵۶۷۸۹'.indexOf(d);
        }).replace(/[,٬\s]/g, '');
    }

    function parseNum(s) {
        var n = parseFloat(enDigits(s));
        return isNaN(n) ? null : n;
    }

    function formatNum(n, maxFrac) {
        if (n == null || isNaN(n)) return '—';
        var parts = Number(n).toLocaleString('en-US', {
            maximumFractionDigits: maxFrac == null ? 4 : maxFrac,
            minimumFractionDigits: 0
        }).split('.');
        parts[0] = parts[0].replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.charAt(+d); });
        if (parts[1]) parts[1] = parts[1].replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.charAt(+d); });
        return parts.join('.');
    }

    function formatBytes(bytes) {
        if (bytes < 1024) return global.NkJalali.fa(bytes) + ' بایت';
        if (bytes < 1024 * 1024) return global.NkJalali.fa((bytes / 1024).toFixed(1)) + ' کیلوبایت';
        return global.NkJalali.fa((bytes / (1024 * 1024)).toFixed(2)) + ' مگابایت';
    }

    function qs(root, sel) { return root.querySelector(sel); }
    function qsa(root, sel) { return Array.prototype.slice.call(root.querySelectorAll(sel)); }

    function bindTabs(root) {
        var tabs = qsa(root, '[data-nk-tab]');
        var panels = qsa(root, '[data-nk-panel]');
        tabs.forEach(function (tab) {
            tab.addEventListener('click', function () {
                var id = tab.getAttribute('data-nk-tab');
                tabs.forEach(function (t) {
                    var on = t.getAttribute('data-nk-tab') === id;
                    t.classList.toggle('active', on);
                    t.setAttribute('aria-selected', on ? 'true' : 'false');
                });
                panels.forEach(function (p) {
                    p.hidden = p.getAttribute('data-nk-panel') !== id;
                });
            });
        });
    }

    global.NkTools = {
        parseNum: parseNum,
        formatNum: formatNum,
        formatBytes: formatBytes,
        qs: qs,
        qsa: qsa,
        bindTabs: bindTabs
    };
})(window);
