// Booking wizard of booking sites: service → staff → day → time → customer details.
(function () {
  var form = document.getElementById('bkForm');
  if (!form) return;

  var daysBox = document.getElementById('bkDays');
  var slotsBox = document.getElementById('bkSlots');
  var dateInput = document.getElementById('bkDate');
  var timeInput = document.getElementById('bkTime');
  var submit = document.getElementById('bkSubmit');
  var hint = document.getElementById('bkHint');
  var dayLabel = '';
  var request = 0;

  function checked(name) { return form.querySelector('input[name="' + name + '"]:checked'); }
  function setText(id, text) { document.getElementById(id).textContent = text || '—'; }

  function message(box, text, icon) {
    box.innerHTML = '';
    var div = document.createElement('div');
    div.className = 'bk-muted';
    if (icon) {
      var i = document.createElement('i');
      i.className = 'bi ' + icon;
      div.appendChild(i);
      div.appendChild(document.createTextNode(' '));
    }
    div.appendChild(document.createTextNode(text));
    box.appendChild(div);
  }

  function query(url, params) {
    var q = Object.keys(params).filter(function (k) { return params[k] !== '' && params[k] != null; })
      .map(function (k) { return encodeURIComponent(k) + '=' + encodeURIComponent(params[k]); }).join('&');
    return url + (url.indexOf('?') < 0 ? '?' : '&') + q;
  }

  // staff who do not offer the chosen service are hidden; "any" stays
  function filterStaff() {
    var service = checked('serviceId');
    var allowed = service && service.getAttribute('data-staff') ? service.getAttribute('data-staff').split(',') : null;
    form.querySelectorAll('[data-staff-option]').forEach(function (label) {
      var ok = !allowed || allowed.indexOf(label.getAttribute('data-staff-option')) >= 0;
      label.hidden = !ok;
      var input = label.querySelector('input');
      if (!ok && input.checked) {
        input.checked = false;
        form.querySelector('input[name="staffId"][value=""]').checked = true;
      }
    });
  }

  function clearTime() {
    dateInput.value = '';
    timeInput.value = '';
    dayLabel = '';
    slotsBox.innerHTML = '';
  }

  function loadDays() {
    clearTime();
    update();
    var service = checked('serviceId');
    if (!service) { message(daysBox, 'اول خدمت را انتخاب کنید.'); return; }
    var staff = checked('staffId');
    var ticket = ++request;
    message(daysBox, 'در حال پیدا کردن روزهای خالی…', 'bi-hourglass-split');
    fetch(query(daysBox.getAttribute('data-url'), { serviceId: service.value, staffId: staff ? staff.value : '' }), { credentials: 'same-origin' })
      .then(function (r) { return r.json(); })
      .then(function (data) {
        if (ticket !== request) return;
        daysBox.innerHTML = '';
        if (!data.days.length) {
          message(daysBox, 'در روزهای پیش رو زمان خالی برای این انتخاب نیست؛ متخصص دیگری را امتحان کنید یا تماس بگیرید.', 'bi-calendar-x');
          return;
        }
        data.days.forEach(function (d) {
          var b = document.createElement('button');
          b.type = 'button';
          b.className = 'bk-day';
          b.setAttribute('data-date', d.date);
          var strong = document.createElement('strong');
          strong.textContent = d.day;
          var small = document.createElement('small');
          small.textContent = d.label;
          b.appendChild(strong);
          b.appendChild(small);
          b.addEventListener('click', function () { pickDay(b, d.day + ' ' + d.label); });
          daysBox.appendChild(b);
        });
        pickDay(daysBox.querySelector('.bk-day'), data.days[0].day + ' ' + data.days[0].label);
      })
      .catch(function () { if (ticket === request) message(daysBox, 'دریافت روزها انجام نشد؛ صفحه را دوباره باز کنید.', 'bi-wifi-off'); });
  }

  function pickDay(button, label) {
    daysBox.querySelectorAll('.bk-day').forEach(function (b) { b.classList.toggle('active', b === button); });
    dateInput.value = button.getAttribute('data-date');
    timeInput.value = '';
    dayLabel = label;
    update();
    var service = checked('serviceId');
    var staff = checked('staffId');
    var ticket = ++request;
    message(slotsBox, 'در حال دریافت ساعت‌های خالی…', 'bi-hourglass-split');
    fetch(query(slotsBox.getAttribute('data-url'), { serviceId: service.value, staffId: staff ? staff.value : '', date: dateInput.value }), { credentials: 'same-origin' })
      .then(function (r) { return r.json(); })
      .then(function (data) {
        if (ticket !== request) return;
        slotsBox.innerHTML = '';
        if (!data.slots.length) { message(slotsBox, 'این روز پر شد؛ روز دیگری انتخاب کنید.', 'bi-calendar-x'); return; }
        data.slots.forEach(function (s) {
          var b = document.createElement('button');
          b.type = 'button';
          b.className = 'bk-slot';
          b.textContent = s.label;
          b.addEventListener('click', function () {
            slotsBox.querySelectorAll('.bk-slot').forEach(function (x) { x.classList.toggle('active', x === b); });
            timeInput.value = s.time;
            update(s.label);
            document.getElementById('stepInfo').scrollIntoView({ behavior: 'smooth', block: 'start' });
          });
          slotsBox.appendChild(b);
        });
      })
      .catch(function () { if (ticket === request) message(slotsBox, 'دریافت ساعت‌ها انجام نشد.', 'bi-wifi-off'); });
  }

  function update(timeLabel) {
    var service = checked('serviceId');
    var staff = checked('staffId');
    setText('sumService', service && service.getAttribute('data-title'));
    setText('sumDuration', service && service.getAttribute('data-duration'));
    setText('sumPrice', service && service.getAttribute('data-price'));
    setText('sumStaff', staff && staff.getAttribute('data-name'));
    setText('sumWhen', timeInput.value ? dayLabel + ' — ساعت ' + (timeLabel || timeInput.value) : (dayLabel || ''));
    var deposit = service && service.getAttribute('data-deposit');
    form.querySelectorAll('.bk-deposit-row').forEach(function (el) { el.hidden = !deposit; });
    setText('sumDeposit', deposit);
    submit.querySelector('span').textContent = deposit ? 'پرداخت بیعانه و ثبت نوبت' : 'ثبت نوبت';
    var ready = !!(service && dateInput.value && timeInput.value);
    submit.disabled = !ready;
    hint.textContent = ready ? 'نام و موبایل را وارد کنید و نوبت را ثبت کنید.' : 'خدمت، روز و ساعت را انتخاب کنید.';
  }

  form.querySelectorAll('input[name="serviceId"]').forEach(function (r) {
    r.addEventListener('change', function () { filterStaff(); loadDays(); });
  });
  form.querySelectorAll('input[name="staffId"]').forEach(function (r) {
    r.addEventListener('change', loadDays);
  });
  form.addEventListener('submit', function () {
    submit.disabled = true;
  });

  filterStaff();
  if (checked('serviceId')) loadDays(); else update();
})();
