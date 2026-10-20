/* Quick-manage popup next to dropdowns (Html.QuickManage): adds, renames and deletes the items of a list and refreshes
   every control on the page marked data-qm="<list>" — dropdowns, checkbox lists, and those inside <template>s. */
(function () {
  var modalEl = document.getElementById('qmModal');
  if (!modalEl || !window.fetch) return;

  var urls = { list: modalEl.dataset.listUrl, save: modalEl.dataset.saveUrl, del: modalEl.dataset.deleteUrl };
  var addForm = modalEl.querySelector('.qm-add');
  var addTitle = addForm.querySelector('[name=title]');
  var addParent = addForm.querySelector('[name=parentId]');
  var token = addForm.querySelector('[name=__RequestVerificationToken]').value;
  var listEl = modalEl.querySelector('.qm-list');
  var msgEl = modalEl.querySelector('.qm-msg');
  var hintEl = modalEl.querySelector('.qm-hint');
  var titleEl = modalEl.querySelector('.modal-title');
  var state = { list: null, scope: null, origin: null, items: [], tree: false, busy: false };
  var modal;

  function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : s; return d.innerHTML; }

  function show(text, isError) {
    msgEl.textContent = text || '';
    msgEl.className = 'qm-msg' + (text ? (isError ? ' is-error' : ' is-ok') : '');
  }

  function read(r) {
    if (!r.ok) throw new Error(r.status);
    return r.json();
  }

  function failed() {
    busy(false);
    show('ارتباط با سرور برقرار نشد. اگر مدتی از ورودتان گذشته، صفحه را دوباره باز کنید.', true);
  }

  function busy(on) {
    state.busy = on;
    modalEl.classList.toggle('is-busy', on);
  }

  function post(url, data) {
    if (state.busy) return Promise.resolve(null);
    busy(true);
    var body = new URLSearchParams();
    body.append('__RequestVerificationToken', token);
    body.append('list', state.list);
    if (state.scope) body.append('scope', state.scope);
    Object.keys(data).forEach(function (k) { if (data[k] != null && data[k] !== '') body.append(k, data[k]); });
    return fetch(url, { method: 'POST', body: body, credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
      .then(read)
      .then(function (res) { busy(false); apply(res); return res; })
      .catch(function () { failed(); return null; });
  }

  function apply(res) {
    state.items = res.items || [];
    state.tree = !!res.tree;
    hintEl.textContent = res.hint || '';
    render();
    sync(res.created, res.renamed);
    show(res.error || res.message, !!res.error);
  }

  function find(id) {
    for (var i = 0; i < state.items.length; i++) if (state.items[i].Id === id) return state.items[i];
    return null;
  }

  function parentOptions(selected, exceptId) {
    return '<option value="">سطح اول (بدون والد)</option>' + state.items
      .filter(function (it) { return it.Depth === 0 && it.Id !== exceptId; })
      .map(function (it) { return '<option value="' + esc(it.Id) + '"' + (it.Id === selected ? ' selected' : '') + '>' + esc(it.Title) + '</option>'; })
      .join('');
  }

  function render() {
    addParent.hidden = !state.tree;
    if (state.tree) addParent.innerHTML = parentOptions(addParent.value || null, null);
    if (!state.items.length) {
      listEl.innerHTML = '<li class="qm-empty"><i class="bi bi-inbox"></i> هنوز موردی ندارید؛ اولین مورد را از بالا اضافه کنید.</li>';
      return;
    }
    listEl.innerHTML = state.items.map(function (it) {
      var name = esc(it.Title);
      return '<li class="qm-item' + (it.Depth ? ' is-child' : '') + '" data-id="' + esc(it.Id) + '" style="--depth:' + (it.Depth || 0) + '">' +
        '<div class="qm-text"><span class="qm-title">' + name + '</span>' + (it.Note ? '<small class="qm-note">' + esc(it.Note) + '</small>' : '') + '</div>' +
        '<div class="row-actions">' +
          '<button type="button" class="icon-btn is-edit" data-act="edit" title="ویرایش" aria-label="ویرایش «' + name + '»"><i class="bi bi-pencil-square" aria-hidden="true"></i></button>' +
          '<button type="button" class="icon-btn is-delete" data-act="delete" title="حذف" aria-label="حذف «' + name + '»"><i class="bi bi-trash" aria-hidden="true"></i></button>' +
          (it.EditUrl ? '<a class="icon-btn is-view" href="' + esc(it.EditUrl) + '" target="_blank" rel="noopener" title="ویرایش کامل در برگه جدید" aria-label="ویرایش کامل «' + name + '» در برگه جدید"><i class="bi bi-box-arrow-up-left" aria-hidden="true"></i></a>' : '') +
        '</div></li>';
    }).join('');
  }

  function startEdit(li, item) {
    render();
    li = listEl.querySelector('[data-id="' + CSS.escape(item.Id) + '"]');
    li.classList.add('is-editing');
    li.innerHTML =
      '<form class="qm-edit">' +
        '<input class="form-control form-control-sm" name="title" maxlength="120" value="' + esc(item.Title) + '" aria-label="نام" required />' +
        (state.tree ? '<select class="form-select form-select-sm" name="parentId" aria-label="والد">' + parentOptions(item.ParentId, item.Id) + '</select>' : '') +
        '<div class="row-actions">' +
          '<button type="submit" class="icon-btn is-ok" title="ذخیره" aria-label="ذخیره"><i class="bi bi-check-lg" aria-hidden="true"></i></button>' +
          '<button type="button" class="icon-btn" data-act="cancel" title="انصراف" aria-label="انصراف"><i class="bi bi-x-lg" aria-hidden="true"></i></button>' +
        '</div>' +
      '</form>';
    var input = li.querySelector('[name=title]');
    input.focus();
    input.select();
  }

  function remove(item) {
    var hasChildren = state.items.some(function (it) { return it.ParentId === item.Id; });
    var question = '«' + item.Title + '» حذف شود؟' +
      (hasChildren ? '\nزیرمجموعه‌های آن هم حذف می‌شوند.' : '') +
      (item.Warn ? '\n' + item.Warn : '');
    var ask = window.nk ? nk.confirm(question, { ok: 'بله، حذف شود' }) : Promise.resolve(confirm(question));
    ask.then(function (yes) { if (yes) post(urls.del, { id: item.Id }); });
  }

  /* ---- Controls on the page that show the list */

  function targets() {
    var selector = '[data-qm="' + state.list + '"]';
    var found = [].slice.call(document.querySelectorAll(selector));
    [].forEach.call(document.querySelectorAll('template'), function (t) {
      found = found.concat([].slice.call(t.content.querySelectorAll(selector)));
    });
    return found;
  }

  function offered(el) {
    var except = el.getAttribute('data-qm-except');
    var rootsOnly = el.hasAttribute('data-qm-roots');
    return state.items.filter(function (it) { return it.Id !== except && (!rootsOnly || it.Depth === 0); });
  }

  function sync(created, renamed) {
    targets().forEach(function (el) {
      var pick = created && el === state.origin ? created : null;
      if (el.tagName === 'SELECT') fillSelect(el, pick, renamed);
      else fillChecks(el, pick, renamed);
    });
  }

  function fillSelect(sel, pick, renamed) {
    var before = sel.value;
    var value = pick || (renamed && before === renamed.from ? renamed.to : before);
    var fixed = [].filter.call(sel.options, function (o) { return o.value === ''; });
    sel.innerHTML = '';
    fixed.forEach(function (o) { sel.appendChild(o); });
    offered(sel).forEach(function (it) { sel.appendChild(new Option(it.Label, it.Id)); });
    sel.value = value;
    if (sel.selectedIndex < 0 && sel.options.length) sel.selectedIndex = 0;
    if (sel.value !== before) sel.dispatchEvent(new Event('change', { bubbles: true }));
  }

  function fillChecks(box, pick, renamed) {
    var name = box.getAttribute('data-qm-name');
    var checked = [].map.call(box.querySelectorAll('input:checked'), function (i) { return renamed && i.value === renamed.from ? renamed.to : i.value; });
    if (pick) checked.push(pick);
    var items = offered(box);
    if (!items.length) {
      box.innerHTML = '<span class="text-muted small">' + esc(box.getAttribute('data-qm-empty') || 'موردی وجود ندارد.') + '</span>';
      return;
    }
    box.innerHTML = items.map(function (it) {
      var on = checked.indexOf(it.Id) >= 0 ? ' checked' : '';
      var input = '<input type="checkbox" name="' + esc(name) + '" value="' + esc(it.Id) + '"' + on;
      return box.getAttribute('data-qm-render') === 'tags'
        ? '<label class="tag-check">' + input + ' /><span><i class="bi bi-tag"></i> ' + esc(it.Label) + '</span></label>'
        : '<div class="form-check"><label class="form-check-label">' + input + ' class="form-check-input" /> ' + esc(it.Label) + '</label></div>';
    }).join('');
  }

  /* ---- Events */

  document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-qm-open]');
    if (!btn) return;
    var field = btn.closest('.qm-field');
    var scope = btn.getAttribute('data-qm-scope');
    state.list = btn.getAttribute('data-qm-open');
    state.origin = field ? field.querySelector('[data-qm]') : null;
    state.scope = scope && document.querySelector(scope) ? document.querySelector(scope).value : null;
    state.items = [];
    titleEl.textContent = 'مدیریت ' + btn.getAttribute('data-qm-title');
    addTitle.value = '';
    addParent.value = '';
    hintEl.textContent = '';
    show('');
    listEl.innerHTML = '<li class="qm-empty"><span class="spinner-border spinner-border-sm"></span> در حال بارگذاری…</li>';
    modal = modal || new bootstrap.Modal(modalEl);
    modal.show();
    fetch(urls.list + (urls.list.indexOf('?') < 0 ? '?' : '&') + 'list=' + encodeURIComponent(state.list), { credentials: 'same-origin' })
      .then(read).then(apply).catch(failed);
  });

  modalEl.addEventListener('shown.bs.modal', function () { addTitle.focus(); });
  modalEl.addEventListener('hidden.bs.modal', function () {
    if (state.origin && state.origin.focus) state.origin.focus();
  });

  addForm.addEventListener('submit', function (e) {
    e.preventDefault();
    var title = addTitle.value.trim();
    if (!title) return addTitle.focus();
    post(urls.save, { title: title, parentId: state.tree ? addParent.value : null }).then(function (res) {
      if (res && res.ok) { addTitle.value = ''; addTitle.focus(); }
    });
  });

  listEl.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-act]');
    if (!btn) return;
    var li = btn.closest('.qm-item');
    var item = li && find(li.getAttribute('data-id'));
    if (!item) return;
    if (btn.getAttribute('data-act') === 'edit') startEdit(li, item);
    else if (btn.getAttribute('data-act') === 'delete') remove(item);
    else if (btn.getAttribute('data-act') === 'cancel') render();
  });

  listEl.addEventListener('submit', function (e) {
    e.preventDefault();
    var form = e.target;
    var item = find(form.closest('.qm-item').getAttribute('data-id'));
    var title = form.querySelector('[name=title]').value.trim();
    var parent = form.querySelector('[name=parentId]');
    if (!item || !title) return;
    post(urls.save, { id: item.Id, title: title, parentId: parent ? parent.value : null });
  });

  listEl.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && e.target.closest('.qm-edit')) { e.stopPropagation(); render(); }
  });
})();
