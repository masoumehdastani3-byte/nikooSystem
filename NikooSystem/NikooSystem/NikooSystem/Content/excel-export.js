/* Export admin HTML tables to Excel-friendly .xls (UTF-8 + RTL). */
(function () {
  function cellText(node) {
    if (!node) return '';
    var clone = node.cloneNode(true);
    clone.querySelectorAll('script, style, form, button, .row-actions, .acc-line-remove, .pos-remove').forEach(function (el) {
      el.parentNode.removeChild(el);
    });
    return String(clone.innerText || clone.textContent || '')
      .replace(/\u00a0/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();
  }

  function skipColumn(th, index, row) {
    if (!th) return false;
    if (th.classList && th.classList.contains('row-actions')) return true;
    var label = cellText(th);
    if (label.length === 0 && row && row.cells[index]) {
      var body = row.cells[index];
      if (body.querySelector('.row-actions, button, form, a.is-edit, a.is-delete, a.is-view')) return true;
    }
    return false;
  }

  function tableToHtml(table) {
    var headRow = table.tHead && table.tHead.rows[0] ? table.tHead.rows[0] : null;
    var bodyRows = table.tBodies && table.tBodies[0] ? Array.prototype.slice.call(table.tBodies[0].rows) : [];
    var sample = bodyRows[0] || headRow;
    var colCount = headRow ? headRow.cells.length : (sample ? sample.cells.length : 0);
    var skip = {};
    for (var c = 0; c < colCount; c++) {
      if (skipColumn(headRow ? headRow.cells[c] : null, c, sample)) skip[c] = true;
    }

    var html = ['<table border="1">'];
    if (headRow) {
      html.push('<thead><tr>');
      for (var h = 0; h < headRow.cells.length; h++) {
        if (skip[h]) continue;
        html.push('<th>' + escapeHtml(cellText(headRow.cells[h])) + '</th>');
      }
      html.push('</tr></thead>');
    }
    html.push('<tbody>');
    bodyRows.forEach(function (row) {
      if (row.classList && row.classList.contains('admin-empty-row')) return;
      var cells = [];
      for (var i = 0; i < row.cells.length; i++) {
        if (skip[i]) continue;
        cells.push('<td>' + escapeHtml(cellText(row.cells[i])) + '</td>');
      }
      if (cells.length) html.push('<tr>' + cells.join('') + '</tr>');
    });
    html.push('</tbody></table>');
    return html.join('');
  }

  function escapeHtml(text) {
    return String(text)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function fileName() {
    var title = (document.title || 'export').split('-')[0].trim() || 'export';
    return title.replace(/[\\/:*?"<>|]+/g, ' ').replace(/\s+/g, ' ').trim() + '.xls';
  }

  function findTable(from) {
    if (!from) return null;
    var card = from.closest ? from.closest('.panel-card') : null;
    if (card) {
      var t = card.querySelector('table.table');
      if (t) return t;
    }
    var next = from.parentElement;
    while (next) {
      var table = next.querySelector && next.querySelector('table.table');
      if (table) return table;
      next = next.nextElementSibling;
    }
    return document.querySelector('main table.table, .admin-main table.table, .panel-card table.table');
  }

  function download(table) {
    if (!table) return;
    var sheet = tableToHtml(table);
    if (!sheet || sheet.indexOf('<td>') < 0 && sheet.indexOf('<th>') < 0) {
      alert('ردیفی برای خروجی نیست.');
      return;
    }
    var doc = '<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">'
      + '<head><meta charset="UTF-8">'
      + '<!--[if gte mso 9]><xml><x:ExcelWorkbook><x:ExcelWorksheets><x:ExcelWorksheet>'
      + '<x:Name>Sheet1</x:Name><x:WorksheetOptions><x:DisplayRightToLeft/></x:WorksheetOptions>'
      + '</x:ExcelWorksheet></x:ExcelWorksheets></x:ExcelWorkbook></xml><![endif]-->'
      + '</head><body dir="rtl">' + sheet + '</body></html>';
    var blob = new Blob(['\ufeff' + doc], { type: 'application/vnd.ms-excel;charset=utf-8;' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = fileName();
    document.body.appendChild(a);
    a.click();
    setTimeout(function () {
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }, 0);
  }

  function exportButton() {
    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'js-excel-export';
    btn.innerHTML = '<i class="bi bi-file-earmark-excel"></i> خروجی اکسل';
    btn.title = 'خروجی اکسل از همین جدول';
    return btn;
  }

  function ensureButton(card) {
    if (!card || card.querySelector('.js-excel-export')) return;
    var table = card.querySelector('table.table');
    if (!table) return;
    var toolbar = card.querySelector('.grid-toolbar');
    if (toolbar) {
      toolbar.appendChild(exportButton());
      return;
    }
    var bar = document.createElement('div');
    bar.className = 'grid-toolbar excel-only-toolbar';
    bar.appendChild(exportButton());
    var anchor = card.querySelector('.table-responsive') || table;
    card.insertBefore(bar, anchor);
  }

  document.addEventListener('click', function (e) {
    var btn = e.target.closest && e.target.closest('.js-excel-export');
    if (!btn) return;
    e.preventDefault();
    download(findTable(btn));
  });

  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.panel-card').forEach(ensureButton);
  });
})();
