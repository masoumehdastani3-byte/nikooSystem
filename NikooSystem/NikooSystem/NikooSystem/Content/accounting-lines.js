/* Dynamic invoice / journal rows for ShopAdmin accounting forms. */
(function (global) {
  function digits(value) {
    var n = parseInt(String(value || '').replace(/[^\d۰-۹٠-٩]/g, '')
      .replace(/[۰-۹]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.indexOf(d); })
      .replace(/[٠-٩]/g, function (d) { return '٠١٢٣٤٥٦٧٨٩'.indexOf(d); }), 10);
    return isNaN(n) ? 0 : n;
  }

  function bindRemove(row, tbody, minRows) {
    var btn = row.querySelector('[data-acc-remove]');
    if (!btn) return;
    btn.addEventListener('click', function () {
      if (tbody.querySelectorAll('tr').length <= minRows) {
        row.querySelectorAll('input, select').forEach(function (el) {
          if (el.tagName === 'SELECT') el.selectedIndex = 0;
          else el.value = '';
        });
        return;
      }
      row.remove();
      if (typeof global.accRecalc === 'function') global.accRecalc();
    });
  }

  function fillFromStock(row, option) {
    if (!row || !option) return;
    var title = row.querySelector('[name="lineTitle"]');
    var price = row.querySelector('[name="linePrice"]');
    var qty = row.querySelector('[name="lineQty"]');
    var name = option.getAttribute('data-title') || option.textContent || '';
    var unit = option.getAttribute('data-price') || '';
    if (title && (!title.value || title.dataset.accAutofill === '1')) {
      title.value = name;
      title.dataset.accAutofill = '1';
    }
    if (price && unit !== '' && (!price.value || price.dataset.accAutofill === '1')) {
      price.value = unit;
      price.dataset.accAutofill = '1';
    }
    if (qty && !qty.value) qty.value = '1';
  }

  function bindStockSelect(row) {
    var select = row.querySelector('[data-acc-stock]');
    if (!select || select.dataset.accBound) return;
    select.dataset.accBound = '1';
    select.addEventListener('change', function () {
      var option = select.options[select.selectedIndex];
      if (!option || !option.value) return;
      fillFromStock(row, option);
      if (typeof global.accRecalc === 'function') global.accRecalc();
    });
    var title = row.querySelector('[name="lineTitle"]');
    var price = row.querySelector('[name="linePrice"]');
    if (title) title.addEventListener('input', function () { title.dataset.accAutofill = '0'; });
    if (price) price.addEventListener('input', function () { price.dataset.accAutofill = '0'; });
  }

  function catalogFromTable(tbody) {
    var map = {};
    var first = tbody.querySelector('[data-acc-stock]');
    if (!first) return [];
    Array.prototype.forEach.call(first.options, function (opt) {
      if (!opt.value) return;
      map[opt.value] = {
        value: opt.value,
        title: opt.getAttribute('data-title') || opt.textContent || '',
        sku: opt.getAttribute('data-sku') || '',
        price: opt.getAttribute('data-price') || '0',
        label: (opt.textContent || '').trim()
      };
    });
    return Object.keys(map).map(function (k) { return map[k]; });
  }

  function bindStockSearch(opts, tbody, addBtn, maxRows) {
    var search = opts.stockSearchId ? document.getElementById(opts.stockSearchId) : null;
    var results = opts.stockResultsId ? document.getElementById(opts.stockResultsId) : null;
    if (!search || !results) return;
    var catalog = catalogFromTable(tbody);

    function hide() {
      results.hidden = true;
      results.innerHTML = '';
    }

    function applyStock(item) {
      var rows = tbody.querySelectorAll('tr');
      var target = null;
      for (var i = 0; i < rows.length; i++) {
        var sel = rows[i].querySelector('[data-acc-stock]');
        var title = rows[i].querySelector('[name="lineTitle"]');
        if (sel && !sel.value && title && !title.value) { target = rows[i]; break; }
      }
      if (!target && addBtn && rows.length < maxRows) {
        addBtn.click();
        rows = tbody.querySelectorAll('tr');
        target = rows[rows.length - 1];
      }
      if (!target) {
        target = rows[rows.length - 1];
        if (addBtn && rows.length < maxRows) {
          addBtn.click();
          rows = tbody.querySelectorAll('tr');
          target = rows[rows.length - 1];
        }
      }
      if (!target) return;
      var select = target.querySelector('[data-acc-stock]');
      if (!select) return;
      select.value = item.value;
      var option = select.options[select.selectedIndex];
      if (option && option.value === item.value) fillFromStock(target, option);
      else {
        var titleEl = target.querySelector('[name="lineTitle"]');
        var priceEl = target.querySelector('[name="linePrice"]');
        var qtyEl = target.querySelector('[name="lineQty"]');
        if (titleEl) { titleEl.value = item.title; titleEl.dataset.accAutofill = '1'; }
        if (priceEl) { priceEl.value = item.price; priceEl.dataset.accAutofill = '1'; }
        if (qtyEl && !qtyEl.value) qtyEl.value = '1';
      }
      search.value = '';
      hide();
      if (typeof global.accRecalc === 'function') global.accRecalc();
      var focusTitle = target.querySelector('[name="lineTitle"]');
      if (focusTitle) focusTitle.focus();
    }

    function showMatches(q) {
      q = (q || '').trim().toLowerCase();
      if (!q) { hide(); return; }
      var hits = catalog.filter(function (item) {
        return (item.title + ' ' + item.sku + ' ' + item.label).toLowerCase().indexOf(q) >= 0;
      }).slice(0, 12);
      if (!hits.length) {
        results.innerHTML = '<div class="acc-stock-empty">کالایی پیدا نشد.</div>';
        results.hidden = false;
        return;
      }
      results.innerHTML = hits.map(function (item) {
        return '<button type="button" class="acc-stock-hit" data-value="' + item.value.replace(/"/g, '&quot;') + '">' +
          '<strong></strong><span></span></button>';
      }).join('');
      Array.prototype.forEach.call(results.querySelectorAll('.acc-stock-hit'), function (btn, idx) {
        var item = hits[idx];
        btn.querySelector('strong').textContent = item.title;
        btn.querySelector('span').textContent = item.sku ? item.sku : (item.price > 0 ? (Number(item.price).toLocaleString('fa-IR') + ' تومان') : '');
        btn.addEventListener('click', function () { applyStock(item); });
      });
      results.hidden = false;
    }

    search.addEventListener('input', function () { showMatches(search.value); });
    search.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') hide();
      if (e.key === 'Enter') {
        e.preventDefault();
        var first = results.querySelector('.acc-stock-hit');
        if (first) first.click();
      }
    });
    document.addEventListener('click', function (e) {
      if (!results.contains(e.target) && e.target !== search) hide();
    });
  }

  global.accInitLines = function (opts) {
    var table = document.getElementById(opts.tableId);
    if (!table) return;
    var tbody = table.querySelector('tbody');
    var template = document.getElementById(opts.templateId);
    var addBtn = document.getElementById(opts.addBtnId);
    var maxRows = opts.maxRows || 24;
    var minRows = opts.minRows || 1;
    var totalEl = opts.totalId ? document.getElementById(opts.totalId) : null;
    var subtotalEl = opts.subtotalId ? document.getElementById(opts.subtotalId) : null;
    var netEl = opts.netId ? document.getElementById(opts.netId) : null;
    var vatEl = opts.vatId ? document.getElementById(opts.vatId) : null;
    var discountEl = opts.discountId ? document.getElementById(opts.discountId) : null;
    var vatPercentEl = opts.vatPercentId ? document.getElementById(opts.vatPercentId) : null;
    var qtyName = opts.qtyName || 'lineQty';
    var priceName = opts.priceName || 'linePrice';

    function toman(n) { return Math.round(Number(n) || 0).toLocaleString('fa-IR') + ' تومان'; }

    function recalc() {
      var rows = tbody.querySelectorAll('tr');
      var sum = 0;
      rows.forEach(function (row) {
        var qty = digits((row.querySelector('[name="' + qtyName + '"]') || {}).value);
        var price = digits((row.querySelector('[name="' + priceName + '"]') || {}).value);
        sum += qty * price;
      });
      var discount = discountEl ? Math.min(digits(discountEl.value), sum) : 0;
      var vatPercent = vatPercentEl ? Math.min(100, digits(vatPercentEl.value)) : 0;
      var net = sum - discount;
      var vat = Math.round(net * vatPercent / 100);
      if (subtotalEl) subtotalEl.textContent = toman(sum);
      if (netEl) netEl.textContent = toman(net);
      if (vatEl) vatEl.textContent = toman(vat);
      if (totalEl) totalEl.textContent = toman(net + vat);
    }
    global.accRecalc = recalc;

    tbody.querySelectorAll('tr').forEach(function (row) {
      bindRemove(row, tbody, minRows);
      bindStockSelect(row);
    });
    table.addEventListener('input', recalc);
    if (discountEl) discountEl.addEventListener('input', recalc);
    if (vatPercentEl) vatPercentEl.addEventListener('input', recalc);

    if (addBtn && template) {
      addBtn.addEventListener('click', function () {
        if (tbody.querySelectorAll('tr').length >= maxRows) return;
        var html = template.innerHTML.trim();
        var wrap = document.createElement('tbody');
        wrap.innerHTML = html;
        var row = wrap.firstElementChild;
        tbody.appendChild(row);
        bindRemove(row, tbody, minRows);
        bindStockSelect(row);
        if (opts.onRowAdded) opts.onRowAdded(row);
        recalc();
      });
    }

    bindStockSearch(opts, tbody, addBtn, maxRows);
    recalc();
  };
})(window);
