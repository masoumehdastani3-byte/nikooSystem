/* Panel tables: on phones each row becomes a card (see admin-table.css), so every cell needs its column title.
   Tables with fewer than three columns already fit and stay as they are; data-stack="off" keeps a table as is. */
(function () {
  function headers(table) {
    var row = table.tHead && table.tHead.rows[table.tHead.rows.length - 1];
    if (!row) return null;
    var labels = [];
    Array.prototype.forEach.call(row.cells, function (cell) {
      for (var i = 0; i < cell.colSpan; i++) labels.push(cell.colSpan === 1 ? cell.textContent.replace(/\s+/g, ' ').trim() : '');
    });
    return labels;
  }

  function label(table, labels) {
    Array.prototype.forEach.call(table.tBodies, function (body) { labelRows(body.rows, labels); });
    if (table.tFoot) labelRows(table.tFoot.rows, labels);
  }

  function labelRows(rows, labels) {
    Array.prototype.forEach.call(rows, function (row) {
      var column = 0;
      Array.prototype.forEach.call(row.cells, function (cell) {
        var text = cell.colSpan === 1 ? labels[column] : '';
        if (text) cell.setAttribute('data-label', text); else cell.removeAttribute('data-label');
        cell.classList.toggle('is-wide', cell.colSpan >= labels.length);
        column += cell.colSpan;
      });
    });
  }

  var faDigits = '۰۱۲۳۴۵۶۷۸۹';

  function toFa(n) {
    return String(n).replace(/\d/g, function (d) { return faDigits.charAt(+d); });
  }

  function fromFa(text) {
    return String(text)
      .replace(/[۰-۹]/g, function (d) { return String(faDigits.indexOf(d)); })
      .replace(/[٠-٩]/g, function (d) { return String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)); });
  }

  function pageStart(table) {
    var root = table.closest ? (table.closest('.panel-card') || table.parentElement) : table.parentElement;
    var summary = null;
    var node = table;
    while (node && node !== root) {
      var sib = node.nextElementSibling;
      while (sib) {
        if (sib.classList && sib.classList.contains('grid-footer')) {
          summary = sib.querySelector('.grid-summary');
          break;
        }
        sib = sib.nextElementSibling;
      }
      if (summary) break;
      node = node.parentElement;
    }
    if (!summary && root && root.querySelector) summary = root.querySelector('.grid-summary');
    if (!summary) return 1;
    var match = summary.textContent.match(/([۰-۹٠-٩0-9]+)/);
    var n = match ? parseInt(fromFa(match[1]), 10) : 1;
    return n > 0 ? n : 1;
  }

  function widen(cell) {
    if (!cell || cell.getAttribute('data-rownum-span') === '1') return;
    cell.colSpan = (cell.colSpan || 1) + 1;
    cell.setAttribute('data-rownum-span', '1');
  }

  function spanRow(row) {
    if (row.classList.contains('admin-empty-row')) return true;
    return row.cells.length === 1 && row.cells[0].colSpan > 1;
  }

  function rowNumbers(table) {
    if (table.getAttribute('data-rownum') === 'off' || !table.tHead || !table.tHead.rows.length) return;
    var head = table.tHead.rows[table.tHead.rows.length - 1];
    if (!head.cells.length) return;
    var first = head.cells[0];
    if (!first.classList.contains('col-rownum')) {
      if (first.textContent.replace(/\s+/g, ' ').trim() === 'ردیف') first.classList.add('col-rownum');
      else {
        var th = document.createElement('th');
        th.className = 'col-rownum';
        th.scope = 'col';
        th.textContent = 'ردیف';
        head.insertBefore(th, first);
      }
    }

    var n = pageStart(table);
    Array.prototype.forEach.call(table.tBodies, function (body) {
      Array.prototype.forEach.call(body.rows, function (row) {
        if (!row.cells.length) return;
        if (spanRow(row)) { widen(row.cells[0]); return; }
        var cell = row.cells[0].classList.contains('col-rownum') ? row.cells[0] : null;
        if (!cell) {
          cell = document.createElement('td');
          cell.className = 'col-rownum';
          row.insertBefore(cell, row.firstChild);
        }
        cell.textContent = toFa(n);
        n += 1;
      });
    });

    if (table.tFoot) {
      Array.prototype.forEach.call(table.tFoot.rows, function (row) {
        if (!row.cells.length || row.cells[0].classList.contains('col-rownum')) return;
        if (row.cells[0].colSpan > 1) widen(row.cells[0]);
        else {
          var td = document.createElement('td');
          td.className = 'col-rownum';
          row.insertBefore(td, row.firstChild);
        }
      });
    }
  }

  function refresh(table) {
    if (table._rownumBusy) return;
    table._rownumBusy = true;
    try {
      rowNumbers(table);
      if (table.classList.contains('is-stack')) {
        var labels = headers(table);
        if (labels) label(table, labels);
      }
    } finally {
      table._rownumBusy = false;
    }
  }

  function stack(table) {
    if (table.getAttribute('data-stack') === 'off' || table.classList.contains('is-stack')) return;
    var labels = headers(table);
    if (!labels || labels.length < 3) return;
    table.classList.add('is-stack');
    label(table, labels);
  }

  function watch(table) {
    if (table._rowWatch || !window.MutationObserver) return;
    table._rowWatch = new MutationObserver(function () { refresh(table); });
    table._rowWatch.observe(table, { childList: true, subtree: true });
  }

  function init(root) {
    Array.prototype.forEach.call((root || document).querySelectorAll('table.table'), function (table) {
      rowNumbers(table);
      stack(table);
      watch(table);
    });
  }

  window.adminTables = init;
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { init(); });
  else init();
})();
