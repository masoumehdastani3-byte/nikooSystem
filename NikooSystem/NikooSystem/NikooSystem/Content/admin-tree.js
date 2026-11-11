/* Expand/collapse for admin tree tables and quick-manage lists. */
(function () {
  function setIcon(btn, expanded) {
    var icon = btn.querySelector('i');
    if (!icon) {
      btn.innerHTML = expanded
        ? '<i class="bi bi-dash-lg" aria-hidden="true"></i>'
        : '<i class="bi bi-plus-lg" aria-hidden="true"></i>';
      return;
    }
    icon.className = expanded ? 'bi bi-dash-lg' : 'bi bi-plus-lg';
  }

  function depthOf(el) {
    return parseInt(el.getAttribute('data-depth'), 10) || 0;
  }

  function toggleRows(row, btn) {
    var depth = depthOf(row);
    var collapsed = row.classList.toggle('is-collapsed');
    btn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
    setIcon(btn, !collapsed);
    var next = row.nextElementSibling;
    while (next && next.classList && (next.classList.contains('nk-tree-row') || next.classList.contains('qm-item'))) {
      var d = depthOf(next);
      if (d <= depth) break;
      if (collapsed) next.hidden = true;
      else {
        var walk = next.previousElementSibling;
        var hide = false;
        var need = d;
        while (walk) {
          if (walk.classList && (walk.classList.contains('nk-tree-row') || walk.classList.contains('qm-item'))) {
            var wd = depthOf(walk);
            if (wd < need) {
              if (walk.classList.contains('is-collapsed')) { hide = true; break; }
              need = wd;
              if (need === 0) break;
            }
          }
          walk = walk.previousElementSibling;
        }
        next.hidden = hide;
      }
      next = next.nextElementSibling;
    }
  }

  document.addEventListener('click', function (e) {
    var btn = e.target.closest('.nk-tree-toggle');
    if (!btn || btn.classList.contains('is-leaf')) return;
    var row = btn.closest('tr.nk-tree-row') || btn.closest('li.qm-item');
    if (!row) return;
    e.preventDefault();
    toggleRows(row, btn);
  });
})();
