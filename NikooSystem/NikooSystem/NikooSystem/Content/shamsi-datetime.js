// Shamsi date and time fields rendered by Html.ShamsiDateTime and Html.ShamsiDate. The hidden input keeps the Gregorian
// "yyyy-MM-ddTHH:mm" (or "yyyy-MM-dd" without the time selects) value the server parses; the button opens a Shamsi
// month calendar. A field with data-placeholder may be left empty.
(function () {
    'use strict';

    var MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];
    var WEEK = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'];

    // ---- Calendar conversion (the jalaali-js algorithm, based on the 33-year leap cycles)

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

    // ---- Formatting

    function fa(value) {
        return String(value).replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.charAt(+d); });
    }

    function pad(n, width) {
        n = String(n);
        while (n.length < width) n = '0' + n;
        return n;
    }

    function todayJdn() {
        var now = new Date();
        return g2d(now.getFullYear(), now.getMonth() + 1, now.getDate());
    }

    // ---- Field

    function init(root) {
        var hidden = root.querySelector('input[type=hidden]');
        var button = root.querySelector('.shamsi-dt-date');
        var label = button.querySelector('span');
        var hour = root.querySelector('[data-part=hour]');
        var minute = root.querySelector('[data-part=minute]');

        var placeholder = root.getAttribute('data-placeholder');

        var m = /^(\d{4})-(\d{2})-(\d{2})/.exec(hidden.value);
        var selected = m ? g2d(+m[1], +m[2], +m[3]) : placeholder !== null ? null : todayJdn();
        var view = d2j(selected === null ? todayJdn() : selected);
        var pop = null;

        function write() {
            if (selected === null) {
                hidden.value = '';
                label.textContent = placeholder;
                return;
            }
            var g = d2g(selected), j = d2j(selected);
            hidden.value = pad(g.gy, 4) + '-' + pad(g.gm, 2) + '-' + pad(g.gd, 2)
                + (hour ? 'T' + pad(hour.value, 2) + ':' + pad(minute.value, 2) : '');
            label.textContent = fa(j.jd + ' ' + MONTHS[j.jm - 1] + ' ' + j.jy);
        }

        function footButton(text, jdn) {
            var b = document.createElement('button');
            b.type = 'button';
            b.textContent = text;
            b.addEventListener('click', function () {
                selected = jdn;
                if (jdn !== null) view = d2j(jdn);
                write();
                close();
                button.focus();
            });
            return b;
        }

        function close() {
            if (!pop) return;
            pop.remove();
            pop = null;
            button.setAttribute('aria-expanded', 'false');
        }

        function shift(months) {
            var index = view.jy * 12 + (view.jm - 1) + months;
            view = { jy: div(index, 12), jm: mod(index, 12) + 1 };
            render();
        }

        function navButton(icon, title, months) {
            var b = document.createElement('button');
            b.type = 'button';
            b.className = 'shamsi-pop-nav';
            b.title = title;
            b.setAttribute('aria-label', title);
            b.innerHTML = '<i class="bi ' + icon + '" aria-hidden="true"></i>';
            b.addEventListener('click', function () { shift(months); });
            return b;
        }

        function render() {
            pop.innerHTML = '';

            var head = document.createElement('div');
            head.className = 'shamsi-pop-head';
            var title = document.createElement('strong');
            title.textContent = fa(MONTHS[view.jm - 1] + ' ' + view.jy);
            head.appendChild(navButton('bi-chevron-double-right', 'سال قبل', -12));
            head.appendChild(navButton('bi-chevron-right', 'ماه قبل', -1));
            head.appendChild(title);
            head.appendChild(navButton('bi-chevron-left', 'ماه بعد', 1));
            head.appendChild(navButton('bi-chevron-double-left', 'سال بعد', 12));
            pop.appendChild(head);

            var grid = document.createElement('div');
            grid.className = 'shamsi-pop-grid';
            WEEK.forEach(function (w) {
                var cell = document.createElement('span');
                cell.className = 'shamsi-pop-week';
                cell.textContent = w;
                grid.appendChild(cell);
            });

            var first = j2d(view.jy, view.jm, 1);
            var g = d2g(first);
            var offset = (new Date(g.gy, g.gm - 1, g.gd).getDay() + 1) % 7;
            for (var i = 0; i < offset; i++) grid.appendChild(document.createElement('span'));

            var today = todayJdn();
            var days = monthLength(view.jy, view.jm);
            for (var d = 1; d <= days; d++) {
                (function (jdn, day) {
                    var b = document.createElement('button');
                    b.type = 'button';
                    b.className = 'shamsi-pop-day' + (jdn === selected ? ' is-selected' : '') + (jdn === today ? ' is-today' : '');
                    b.textContent = fa(day);
                    b.addEventListener('click', function () {
                        selected = jdn;
                        write();
                        close();
                        button.focus();
                    });
                    grid.appendChild(b);
                })(first + d - 1, d);
            }
            pop.appendChild(grid);

            var foot = document.createElement('div');
            foot.className = 'shamsi-pop-foot';
            foot.appendChild(footButton('امروز', todayJdn()));
            if (placeholder !== null) foot.appendChild(footButton('پاک کردن', null));
            pop.appendChild(foot);
        }

        function open() {
            if (pop) return close();
            view = d2j(selected === null ? todayJdn() : selected);
            pop = document.createElement('div');
            pop.className = 'shamsi-pop';
            pop.setAttribute('role', 'dialog');
            pop.setAttribute('aria-label', 'انتخاب تاریخ');
            // Re-rendering detaches the clicked button, so the outside-click check below must not see these clicks.
            pop.addEventListener('click', function (e) { e.stopPropagation(); });
            root.appendChild(pop);
            button.setAttribute('aria-expanded', 'true');
            render();
            var current = pop.querySelector('.is-selected') || pop.querySelector('.shamsi-pop-day');
            if (current) current.focus();
        }

        button.addEventListener('click', open);
        if (hour) {
            hour.addEventListener('change', write);
            minute.addEventListener('change', write);
        }
        document.addEventListener('click', function (e) {
            if (pop && !root.contains(e.target)) close();
        });
        root.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && pop) {
                close();
                button.focus();
            }
        });
        write();
    }

    Array.prototype.forEach.call(document.querySelectorAll('[data-shamsi-dt]'), init);
})();
