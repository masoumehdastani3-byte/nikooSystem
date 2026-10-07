(function () {
  function textOf(el) {
    return (el && (el.textContent || el.innerText) || "").replace(/\s+/g, " ").trim().toLowerCase();
  }

  function getCellValue(row, index) {
    var cells = row.querySelectorAll("td");
    if (!cells[index]) return "";
    return textOf(cells[index]);
  }

  function parseSortValue(raw) {
    if (!raw) return { num: null, str: "" };
    var cleaned = raw.replace(/[^\d\u06F0-\u06F9.-]/g, "").replace(/[\u06F0-\u06F9]/g, function (d) {
      return String(d.charCodeAt(0) - 0x06F0);
    });
    var n = parseFloat(cleaned.replace(/,/g, ""));
    if (!isNaN(n) && cleaned.length) return { num: n, str: raw };
    return { num: null, str: raw };
  }

  function compare(a, b, dir) {
    var av = parseSortValue(a);
    var bv = parseSortValue(b);
    var res = 0;
    if (av.num !== null && bv.num !== null) res = av.num - bv.num;
    else res = av.str.localeCompare(bv.str, "fa", { sensitivity: "base", numeric: true });
    return dir === "desc" ? -res : res;
  }

  function enhanceTable(table) {
    if (!table || table.dataset.toolsReady === "1") return;
    if (!table.tBodies || !table.tBodies[0] || table.tBodies[0].rows.length === 0) return;
    if (table.classList.contains("no-table-tools")) return;

    table.dataset.toolsReady = "1";
    var wrapper = document.createElement("div");
    wrapper.className = "table-tools";
    wrapper.innerHTML =
      '<div class="table-tools-bar">' +
      '<input type="search" class="form-control table-tools-search" placeholder="جستجو در جدول..." />' +
      '<span class="table-tools-meta"></span>' +
      "</div>";

    table.parentNode.insertBefore(wrapper, table);
    wrapper.appendChild(table);

    var search = wrapper.querySelector(".table-tools-search");
    var meta = wrapper.querySelector(".table-tools-meta");
    var tbody = table.tBodies[0];
    var headers = table.tHead ? table.tHead.querySelectorAll("th") : [];
    var sortState = { index: -1, dir: "asc" };

    function updateMeta() {
      var rows = Array.prototype.slice.call(tbody.rows);
      var visible = rows.filter(function (r) { return r.style.display !== "none"; }).length;
      meta.textContent = visible + " / " + rows.length + " ردیف";
    }

    function applyFilter() {
      var q = (search.value || "").trim().toLowerCase();
      Array.prototype.forEach.call(tbody.rows, function (row) {
        if (!q) {
          row.style.display = "";
          return;
        }
        row.style.display = textOf(row).indexOf(q) >= 0 ? "" : "none";
      });
      updateMeta();
    }

    function applySort(index) {
      if (sortState.index === index) sortState.dir = sortState.dir === "asc" ? "desc" : "asc";
      else {
        sortState.index = index;
        sortState.dir = "asc";
      }

      Array.prototype.forEach.call(headers, function (th, i) {
        th.classList.remove("sorted-asc", "sorted-desc");
        if (i === sortState.index) th.classList.add(sortState.dir === "asc" ? "sorted-asc" : "sorted-desc");
      });

      var rows = Array.prototype.slice.call(tbody.rows);
      rows.sort(function (ra, rb) {
        return compare(getCellValue(ra, index), getCellValue(rb, index), sortState.dir);
      });
      rows.forEach(function (r) { tbody.appendChild(r); });
      applyFilter();
    }

    Array.prototype.forEach.call(headers, function (th, index) {
      if (th.classList.contains("no-sort")) return;
      // skip empty action columns with no text
      if (!textOf(th) && index === headers.length - 1) {
        th.classList.add("no-sort");
        return;
      }
      th.classList.add("sortable-th");
      th.title = "کلیک برای مرتب‌سازی";
      th.addEventListener("click", function () { applySort(index); });
    });

    search.addEventListener("input", applyFilter);
    updateMeta();
  }

  function boot() {
    var roots = [
      document.querySelector(".admin-main"),
      document.querySelector("main"),
      document.querySelector(".container-fluid"),
      document.body
    ].filter(Boolean);

    var seen = {};
    roots.forEach(function (root) {
      Array.prototype.forEach.call(root.querySelectorAll("table.table"), function (table) {
        if (seen[table]) return;
        seen[table] = true;
        enhanceTable(table);
      });
    });
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", boot);
  else boot();
})();
