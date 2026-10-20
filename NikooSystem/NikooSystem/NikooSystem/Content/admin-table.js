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

  function stack(table) {
    if (table.getAttribute('data-stack') === 'off' || table.classList.contains('is-stack')) return;
    var labels = headers(table);
    if (!labels || labels.length < 3) return;
    table.classList.add('is-stack');
    label(table, labels);
    if (window.MutationObserver) {
      new MutationObserver(function () { label(table, labels); }).observe(table, { childList: true, subtree: true });
    }
  }

  function init(root) {
    Array.prototype.forEach.call((root || document).querySelectorAll('table.table'), stack);
  }

  window.adminTables = init;
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { init(); });
  else init();
})();
