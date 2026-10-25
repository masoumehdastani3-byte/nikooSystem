(function () {
    'use strict';

    var J = window.NkJalali;
    var U = window.NkTools;
    var root = document.getElementById('tool-converter');
    if (!root) return;

    U.bindTabs(root);

    var dateOut = U.qs(root, '[data-date-out]');
    var dateErr = U.qs(root, '[data-date-err]');

    function readInt(el) {
        var n = parseInt(U.parseNum(el.value), 10);
        return isNaN(n) ? null : n;
    }

    function convertDate() {
        dateErr.textContent = '';
        dateOut.innerHTML = '';
        var mode = U.qs(root, 'input[name="dateMode"]:checked').value;
        if (mode === 'j2g') {
            var jy = readInt(U.qs(root, '[data-jy]'));
            var jm = readInt(U.qs(root, '[data-jm]'));
            var jd = readInt(U.qs(root, '[data-jd]'));
            if (!jy || !jm || !jd || !J.isValidJ(jy, jm, jd)) {
                dateErr.textContent = 'تاریخ شمسی را درست وارد کنید.';
                return;
            }
            var g = J.d2g(J.j2d(jy, jm, jd));
            var jdn = J.j2d(jy, jm, jd);
            dateOut.innerHTML = '<p><strong>میلادی:</strong> ' + J.formatG(g) + '</p>'
                + '<p><strong>شمسی:</strong> ' + J.formatJ({ jy: jy, jm: jm, jd: jd }) + '</p>'
                + '<p class="text-muted mb-0"><strong>روز هفته:</strong> ' + J.weekday(jdn) + '</p>';
        } else {
            var gy = readInt(U.qs(root, '[data-gy]'));
            var gm = readInt(U.qs(root, '[data-gm]'));
            var gd = readInt(U.qs(root, '[data-gd]'));
            if (!gy || !gm || !gd || !J.isValidG(gy, gm, gd)) {
                dateErr.textContent = 'تاریخ میلادی را درست وارد کنید.';
                return;
            }
            var jdn = J.g2d(gy, gm, gd);
            var j = J.d2j(jdn);
            dateOut.innerHTML = '<p><strong>شمسی:</strong> ' + J.formatJ(j) + '</p>'
                + '<p><strong>میلادی:</strong> ' + J.formatG({ gy: gy, gm: gm, gd: gd }) + '</p>'
                + '<p class="text-muted mb-0"><strong>روز هفته:</strong> ' + J.weekday(jdn) + '</p>';
        }
    }

    U.qs(root, '[data-date-convert]').addEventListener('click', convertDate);
    U.qs(root, '[data-date-today]').addEventListener('click', function () {
        var jdn = J.todayJdn();
        var j = J.d2j(jdn);
        var g = J.d2g(jdn);
        U.qs(root, '[data-jy]').value = j.jy;
        U.qs(root, '[data-jm]').value = j.jm;
        U.qs(root, '[data-jd]').value = j.jd;
        U.qs(root, '[data-gy]').value = g.gy;
        U.qs(root, '[data-gm]').value = g.gm;
        U.qs(root, '[data-gd]').value = g.gd;
        convertDate();
    });

    // ---- Currency (rates in IRR per 1 unit) ----
    var RATE_KEY = 'nkToolFxRates';
    var defaultRates = { USD: 700000, EUR: 760000, GBP: 900000, TRY: 20000, AED: 190000, TMN: 10 };

    function loadRates() {
        try {
            var s = localStorage.getItem(RATE_KEY);
            if (s) return Object.assign({}, defaultRates, JSON.parse(s));
        } catch (e) { /* ignore */ }
        return Object.assign({}, defaultRates);
    }

    function saveRates(rates) {
        try { localStorage.setItem(RATE_KEY, JSON.stringify(rates)); } catch (e) { /* ignore */ }
    }

    var rates = loadRates();
    var rateInputs = U.qsa(root, '[data-fx-rate]');
    rateInputs.forEach(function (inp) {
        var code = inp.getAttribute('data-fx-rate');
        inp.value = rates[code] || defaultRates[code] || '';
        inp.addEventListener('change', function () {
            var v = U.parseNum(inp.value);
            if (v != null && v > 0) {
                rates[code] = v;
                saveRates(rates);
            }
        });
    });

    var fxOut = U.qs(root, '[data-fx-out]');

    function toIrr(amount, code) {
        if (code === 'IRR') return amount;
        if (code === 'TMN') return amount * 10;
        var r = rates[code];
        return r ? amount * r : null;
    }

    function fromIrr(irr, code) {
        if (code === 'IRR') return irr;
        if (code === 'TMN') return irr / 10;
        var r = rates[code];
        return r ? irr / r : null;
    }

    function convertFx() {
        var amount = U.parseNum(U.qs(root, '[data-fx-amount]').value);
        var from = U.qs(root, '[data-fx-from]').value;
        var to = U.qs(root, '[data-fx-to]').value;
        if (amount == null) {
            fxOut.textContent = 'مبلغ را وارد کنید.';
            return;
        }
        var irr = toIrr(amount, from);
        if (irr == null) {
            fxOut.textContent = 'نرخ این ارز را وارد کنید.';
            return;
        }
        var result = fromIrr(irr, to);
        if (result == null) {
            fxOut.textContent = 'نرخ ارز مقصد را وارد کنید.';
            return;
        }
        fxOut.innerHTML = '<p class="mb-1"><strong>نتیجه:</strong> ' + U.formatNum(result, 2) + ' '
            + U.qs(root, '[data-fx-to] option:checked').textContent + '</p>'
            + '<p class="text-muted small mb-0">معادل ' + U.formatNum(irr, 0) + ' ریال</p>';
    }

    U.qs(root, '[data-fx-convert]').addEventListener('click', convertFx);

    // ---- Units ----
    var units = {
        length: [
            { id: 'm', label: 'متر', factor: 1 },
            { id: 'km', label: 'کیلومتر', factor: 1000 },
            { id: 'cm', label: 'سانتی‌متر', factor: 0.01 },
            { id: 'mm', label: 'میلی‌متر', factor: 0.001 },
            { id: 'in', label: 'اینچ', factor: 0.0254 },
            { id: 'ft', label: 'فوت', factor: 0.3048 },
            { id: 'mi', label: 'مایل', factor: 1609.344 }
        ],
        weight: [
            { id: 'kg', label: 'کیلوگرم', factor: 1 },
            { id: 'g', label: 'گرم', factor: 0.001 },
            { id: 'mg', label: 'میلی‌گرم', factor: 0.000001 },
            { id: 'lb', label: 'پوند', factor: 0.45359237 },
            { id: 'oz', label: 'اونس', factor: 0.028349523125 }
        ],
        temp: [
            { id: 'c', label: 'سانتی‌گراد', kind: 'c' },
            { id: 'f', label: 'فahrenheit', kind: 'f' },
            { id: 'k', label: 'کلوین', kind: 'k' }
        ]
    };

    function fillUnitSelect(sel, list) {
        sel.innerHTML = list.map(function (u) {
            return '<option value="' + u.id + '">' + u.label + '</option>';
        }).join('');
    }

    var unitKind = U.qs(root, '[data-unit-kind]');
    var unitFrom = U.qs(root, '[data-unit-from]');
    var unitTo = U.qs(root, '[data-unit-to]');
    var unitOut = U.qs(root, '[data-unit-out]');

    function refreshUnitSelects() {
        var list = units[unitKind.value];
        fillUnitSelect(unitFrom, list);
        fillUnitSelect(unitTo, list);
        if (unitTo.options.length > 1) unitTo.selectedIndex = 1;
    }

    unitKind.addEventListener('change', refreshUnitSelects);
    refreshUnitSelects();

    function convertTemp(val, from, to) {
        var c = from === 'c' ? val : from === 'f' ? (val - 32) * 5 / 9 : val - 273.15;
        if (to === 'c') return c;
        if (to === 'f') return c * 9 / 5 + 32;
        return c + 273.15;
    }

    U.qs(root, '[data-unit-convert]').addEventListener('click', function () {
        var val = U.parseNum(U.qs(root, '[data-unit-val]').value);
        if (val == null) {
            unitOut.textContent = 'مقدار را وارد کنید.';
            return;
        }
        var kind = unitKind.value;
        var fromId = unitFrom.value;
        var toId = unitTo.value;
        var list = units[kind];
        var fromU = list.find(function (x) { return x.id === fromId; });
        var toU = list.find(function (x) { return x.id === toId; });
        var result;
        if (kind === 'temp') {
            result = convertTemp(val, fromId, toId);
        } else {
            result = val * fromU.factor / toU.factor;
        }
        unitOut.innerHTML = '<strong>' + U.formatNum(result, 4) + '</strong> ' + toU.label;
    });
})();
