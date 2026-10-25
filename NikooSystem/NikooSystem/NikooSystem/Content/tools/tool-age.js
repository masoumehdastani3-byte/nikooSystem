(function () {
    'use strict';

    var J = window.NkJalali;
    var U = window.NkTools;
    var root = document.getElementById('tool-age');
    if (!root) return;

    function readInt(el) {
        var n = parseInt(U.parseNum(el.value), 10);
        return isNaN(n) ? null : n;
    }

    function birthJdn() {
        var cal = U.qs(root, 'input[name="birthCal"]:checked').value;
        if (cal === 'shamsi') {
            var jy = readInt(U.qs(root, '[data-b-jy]'));
            var jm = readInt(U.qs(root, '[data-b-jm]'));
            var jd = readInt(U.qs(root, '[data-b-jd]'));
            if (!jy || !jm || !jd || !J.isValidJ(jy, jm, jd)) return null;
            return J.j2d(jy, jm, jd);
        }
        var gy = readInt(U.qs(root, '[data-b-gy]'));
        var gm = readInt(U.qs(root, '[data-b-gm]'));
        var gd = readInt(U.qs(root, '[data-b-gd]'));
        if (!gy || !gm || !gd || !J.isValidG(gy, gm, gd)) return null;
        return J.g2d(gy, gm, gd);
    }

    function ageParts(fromJdn, toJdn) {
        var gFrom = J.d2g(fromJdn);
        var gTo = J.d2g(toJdn);
        var y = gTo.gy - gFrom.gy;
        var m = gTo.gm - gFrom.gm;
        var d = gTo.gd - gFrom.gd;
        if (d < 0) {
            m--;
            var prev = J.d2g(J.g2d(gTo.gy, gTo.gm, 1) - 1);
            d += prev.gd;
        }
        if (m < 0) {
            y--;
            m += 12;
        }
        var totalDays = toJdn - fromJdn;
        return { years: y, months: m, days: d, totalDays: totalDays };
    }

    function nextBirthday(fromJdn, todayJdn) {
        var j = J.d2j(fromJdn);
        var tj = J.d2j(todayJdn);
        var year = tj.jy;
        var target = J.j2d(year, j.jm, j.jd);
        if (target < todayJdn) {
            if (!J.isValidJ(year + 1, j.jm, j.jd)) {
                target = J.j2d(year + 1, j.jm, Math.min(j.jd, J.monthLength(year + 1, j.jm)));
            } else {
                target = J.j2d(year + 1, j.jm, j.jd);
            }
        }
        return target - todayJdn;
    }

    function toggleCalPanels() {
        var cal = U.qs(root, 'input[name="birthCal"]:checked').value;
        U.qs(root, '[data-birth-shamsi]').hidden = cal !== 'shamsi';
        U.qs(root, '[data-birth-greg]').hidden = cal !== 'gregorian';
    }

    U.qsa(root, 'input[name="birthCal"]').forEach(function (r) {
        r.addEventListener('change', toggleCalPanels);
    });
    toggleCalPanels();

    U.qs(root, '[data-age-calc]').addEventListener('click', function () {
        var out = U.qs(root, '[data-age-out]');
        var err = U.qs(root, '[data-age-err]');
        err.textContent = '';
        out.innerHTML = '';
        var birth = birthJdn();
        if (birth == null) {
            err.textContent = 'تاریخ تولد را درست وارد کنید.';
            return;
        }
        var today = J.todayJdn();
        if (birth > today) {
            err.textContent = 'تاریخ تولد نمی‌تواند در آینده باشد.';
            return;
        }
        var age = ageParts(birth, today);
        var j = J.d2j(birth);
        var g = J.d2g(birth);
        var until = nextBirthday(birth, today);
        out.innerHTML =
            '<p class="nk-age-main">' + J.fa(age.years) + ' سال، ' + J.fa(age.months) + ' ماه و ' + J.fa(age.days) + ' روز</p>'
            + '<p><strong>تولد شمسی:</strong> ' + J.formatJ(j) + ' · <strong>میلادی:</strong> ' + J.formatG(g) + '</p>'
            + '<p><strong>روز هفته تولد:</strong> ' + J.weekday(birth) + '</p>'
            + '<p class="text-muted mb-0"><strong>تا تولد بعدی:</strong> '
            + (until === 0 ? 'امروز تولدتان مبارک!' : J.fa(until) + ' روز')
            + ' · <strong>کل روزهای زندگی:</strong> ' + J.fa(age.totalDays) + '</p>';
    });
})();
