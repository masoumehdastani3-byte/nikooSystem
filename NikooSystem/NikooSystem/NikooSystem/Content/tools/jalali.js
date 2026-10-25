// Jalali ↔ Gregorian (jalaali-js algorithm). Shared by site tools.
(function (global) {
    'use strict';

    var MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];
    var WEEK = ['یکشنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه', 'شنبه'];
    var BREAKS = [-61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181, 1210, 1635, 2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178];

    function div(a, b) { return ~~(a / b); }
    function mod(a, b) { return a - ~~(a / b) * b; }

    function jalCal(jy) {
        var gy = jy + 621, leapJ = -14, jp = BREAKS[0], jump = 0, jm, n, i;
        for (i = 1; i < BREAKS.length; i++) {
            jm = BREAKS[i];
            jump = jm - jp;
            if (jy < jm) break;
            leapJ += div(jump, 33) * 8 + div(mod(jump, 33), 4);
            jp = jm;
        }
        n = jy - jp;
        leapJ += div(n, 33) * 8 + div(mod(n, 33) + 3, 4);
        if (mod(jump, 33) === 4 && jump - n === 4) leapJ++;
        var leapG = div(gy, 4) - div((div(gy, 100) + 1) * 3, 4) - 150;
        if (jump - n < 6) n = n - jump + div(jump + 4, 33) * 33;
        var leap = mod(mod(n + 1, 33) - 1, 4);
        if (leap === -1) leap = 4;
        return { leap: leap, gy: gy, march: 20 + leapJ - leapG };
    }

    function g2d(gy, gm, gd) {
        var d = div((gy + div(gm - 8, 6) + 100100) * 1461, 4) + div(153 * mod(gm + 9, 12) + 2, 5) + gd - 34840408;
        return d - div(div(gy + 100100 + div(gm - 8, 6), 100) * 3, 4) + 752;
    }

    function d2g(jdn) {
        var j = 4 * jdn + 139361631;
        j = j + div(div(4 * jdn + 183187720, 146097) * 3, 4) * 4 - 3908;
        var i = div(mod(j, 1461), 4) * 5 + 308;
        var gd = div(mod(i, 153), 5) + 1;
        var gm = mod(div(i, 153), 12) + 1;
        return { gy: div(j, 1461) - 100100 + div(8 - gm, 6), gm: gm, gd: gd };
    }

    function j2d(jy, jm, jd) {
        var r = jalCal(jy);
        return g2d(r.gy, 3, r.march) + (jm - 1) * 31 - div(jm, 7) * (jm - 7) + jd - 1;
    }

    function d2j(jdn) {
        var gy = d2g(jdn).gy, jy = gy - 621, r = jalCal(jy), k = jdn - g2d(gy, 3, r.march);
        if (k >= 0) {
            if (k <= 185) return { jy: jy, jm: 1 + div(k, 31), jd: mod(k, 31) + 1 };
            k -= 186;
        } else {
            jy--;
            k += 179;
            if (r.leap === 1) k++;
        }
        return { jy: jy, jm: 7 + div(k, 30), jd: mod(k, 30) + 1 };
    }

    function monthLength(jy, jm) {
        return jm < 12 ? j2d(jy, jm + 1, 1) - j2d(jy, jm, 1) : j2d(jy + 1, 1, 1) - j2d(jy, 12, 1);
    }

    function fa(value) {
        return String(value).replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.charAt(+d); });
    }

    function en(value) {
        return String(value).replace(/[۰-۹]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.indexOf(d); });
    }

    function pad(n, w) {
        n = String(n);
        while (n.length < w) n = '0' + n;
        return n;
    }

    function formatJ(j) {
        return fa(j.jd + ' ' + MONTHS[j.jm - 1] + ' ' + j.jy);
    }

    function formatG(g) {
        return fa(g.gy + '/' + pad(g.gm, 2) + '/' + pad(g.gd, 2));
    }

    function weekday(jdn) {
        return WEEK[mod(jdn + 1, 7)];
    }

    function todayJdn() {
        var now = new Date();
        return g2d(now.getFullYear(), now.getMonth() + 1, now.getDate());
    }

    function isValidJ(jy, jm, jd) {
        if (jm < 1 || jm > 12 || jd < 1) return false;
        return jd <= monthLength(jy, jm);
    }

    function isValidG(gy, gm, gd) {
        if (gm < 1 || gm > 12 || gd < 1) return false;
        var jdn = g2d(gy, gm, gd);
        var g = d2g(jdn);
        return g.gy === gy && g.gm === gm && g.gd === gd;
    }

    global.NkJalali = {
        MONTHS: MONTHS,
        WEEK: WEEK,
        g2d: g2d,
        d2g: d2g,
        j2d: j2d,
        d2j: d2j,
        monthLength: monthLength,
        fa: fa,
        en: en,
        formatJ: formatJ,
        formatG: formatG,
        weekday: weekday,
        todayJdn: todayJdn,
        isValidJ: isValidJ,
        isValidG: isValidG
    };
})(typeof window !== 'undefined' ? window : this);
