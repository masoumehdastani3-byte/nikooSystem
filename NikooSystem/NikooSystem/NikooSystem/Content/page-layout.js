(function () {
  'use strict';

  var form = document.getElementById('layoutForm');
  var list = document.getElementById('lsList');
  if (!form || !list) return;

  var dirtyNote = document.getElementById('lsDirty');
  var isDirty = false;
  var dragging = null;

  function markDirty() {
    isDirty = true;
    if (dirtyNote) dirtyNote.hidden = false;
  }

  function newId() {
    var hex = '';
    while (hex.length < 8) hex += Math.floor(Math.random() * 16).toString(16);
    return 's' + hex;
  }

  function field(li, name) {
    return li.querySelector('[data-field="' + name + '"]');
  }

  function selectedText(select) {
    return select && select.selectedIndex >= 0 && select.value ? select.options[select.selectedIndex].text.trim() : '';
  }

  function flash(li) {
    li.classList.remove('is-flash');
    void li.offsetWidth;
    li.classList.add('is-flash');
  }

  function refresh(li) {
    var visible = field(li, 'IsVisible');
    li.classList.toggle('is-hidden', !!visible && !visible.checked);

    var source = li.querySelector('[data-source]');
    if (source) {
      li.querySelectorAll('[data-when]').forEach(function (el) {
        el.hidden = el.getAttribute('data-when') !== source.value;
      });
    }

    var summary = li.querySelector('.ls-summary');
    if (!summary) return;
    var title = field(li, 'Title');
    var titleText = title ? title.value.trim() : '';
    var text = summary.getAttribute('data-hint');
    if (li.dataset.type === 'products' || li.dataset.type === 'posts') {
      var detail = selectedText(source);
      if (source && source.value === 'collection') detail += ': ' + (selectedText(field(li, 'CollectionId')) || 'انتخاب نشده');
      if (source && source.value === 'category') detail += ': ' + (selectedText(field(li, 'CategoryId')) || 'انتخاب نشده');
      text = (titleText ? '«' + titleText + '» — ' : '') + detail;
    } else if (li.dataset.type === 'banner') {
      text = titleText ? '«' + titleText + '»' : 'بدون عنوان — برای نمایش، عنوان بنویسید';
    } else if (titleText) {
      text = 'عنوان: «' + titleText + '»';
    }
    summary.textContent = text;
  }

  function toggleEdit(li, open) {
    var panel = li.querySelector('.ls-edit');
    if (!panel) return;
    panel.hidden = open === undefined ? !panel.hidden : !open;
    li.classList.toggle('is-open', !panel.hidden);
  }

  list.querySelectorAll('.ls-item').forEach(refresh);

  list.addEventListener('click', function (e) {
    var btn = e.target.closest('button');
    if (!btn || !list.contains(btn)) return;
    var li = btn.closest('.ls-item');

    if (btn.hasAttribute('data-move')) {
      if (btn.getAttribute('data-move') === '-1' && li.previousElementSibling) {
        list.insertBefore(li, li.previousElementSibling);
      } else if (btn.getAttribute('data-move') === '1' && li.nextElementSibling) {
        list.insertBefore(li.nextElementSibling, li);
      } else {
        return;
      }
      btn.focus();
      flash(li);
      markDirty();
    } else if (btn.hasAttribute('data-edit')) {
      toggleEdit(li);
    } else if (btn.hasAttribute('data-remove')) {
      if (!confirm('این بخش از صفحه اول حذف شود؟')) return;
      li.remove();
      markDirty();
    } else if (btn.hasAttribute('data-clear-image')) {
      field(li, 'ImageUrl').value = '';
      btn.closest('.ls-img').remove();
      markDirty();
    }
  });

  function onEdit(e) {
    var li = e.target.closest('.ls-item');
    if (!li) return;
    if (e.target.hasAttribute('data-link-pick')) {
      if (e.target.value) field(li, 'LinkUrl').value = e.target.value;
      e.target.value = '';
    }
    refresh(li);
  }
  list.addEventListener('change', onEdit);
  list.addEventListener('input', onEdit);

  // drag & drop, started only from the grip so text fields stay selectable
  list.addEventListener('mousedown', function (e) {
    var handle = e.target.closest('.ls-handle');
    if (handle) handle.closest('.ls-item').draggable = true;
  });
  list.addEventListener('dragstart', function (e) {
    dragging = e.target.closest('.ls-item');
    if (!dragging) return;
    dragging.classList.add('is-dragging');
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', dragging.dataset.id);
  });
  list.addEventListener('dragover', function (e) {
    if (!dragging) return;
    e.preventDefault();
    var over = e.target.closest('.ls-item');
    if (!over || over === dragging) return;
    var r = over.getBoundingClientRect();
    list.insertBefore(dragging, e.clientY > r.top + r.height / 2 ? over.nextElementSibling : over);
  });
  list.addEventListener('dragend', function () {
    if (!dragging) return;
    dragging.classList.remove('is-dragging');
    dragging.draggable = false;
    flash(dragging);
    dragging = null;
    markDirty();
  });
  document.addEventListener('mouseup', function () {
    list.querySelectorAll('.ls-item[draggable="true"]').forEach(function (li) {
      if (li !== dragging) li.draggable = false;
    });
  });

  document.querySelectorAll('[data-add]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var tpl = document.getElementById('tpl-' + btn.getAttribute('data-add'));
      if (!tpl) return;
      var holder = document.createElement('ul');
      holder.innerHTML = tpl.innerHTML.split('__ID__').join(newId());
      var li = holder.querySelector('.ls-item');
      var hero = list.querySelector('.ls-item[data-type="hero"]');
      list.insertBefore(li, hero ? hero.nextElementSibling : list.firstElementChild);
      refresh(li);
      toggleEdit(li, true);
      flash(li);
      markDirty();
      li.scrollIntoView({ behavior: 'smooth', block: 'center' });
      var first = li.querySelector('.ls-edit input:not([type=hidden]), .ls-edit select');
      if (first) setTimeout(function () { first.focus({ preventScroll: true }); }, 350);
    });
  });

  // product-page options
  form.querySelectorAll('[data-when-checked]').forEach(function (sub) {
    var box = document.getElementById(sub.getAttribute('data-when-checked'));
    function sync() { sub.hidden = !box.checked; }
    box.addEventListener('change', sync);
    sync();
  });
  form.addEventListener('change', markDirty);
  form.addEventListener('input', markDirty);

  form.addEventListener('submit', function (e) {
    var sections = [];
    var problem = null;
    list.querySelectorAll('.ls-item').forEach(function (li) {
      var s = { Id: li.dataset.id, Type: li.dataset.type };
      li.querySelectorAll('[data-field]').forEach(function (el) {
        var key = el.getAttribute('data-field');
        if (el.type === 'checkbox') s[key] = el.checked;
        else if (el.getAttribute('data-type') === 'int') { if (el.value !== '') s[key] = parseInt(el.value, 10); }
        else s[key] = el.value.trim() || null;
      });
      if (!problem && s.IsVisible) {
        if (s.Type === 'banner' && !s.Title) problem = { li: li, msg: 'برای بنر تبلیغاتی یک عنوان بنویسید (یا آن را مخفی کنید).' };
        else if (s.Type === 'products' && s.Source === 'collection' && !s.CollectionId) problem = { li: li, msg: 'برای ردیف محصولات، برچسب را انتخاب کنید.' };
        else if (s.Type === 'products' && s.Source === 'category' && !s.CategoryId) problem = { li: li, msg: 'برای ردیف محصولات، گروه کالا را انتخاب کنید.' };
        else if (s.Type === 'posts' && s.Source === 'category' && !s.CategoryId) problem = { li: li, msg: 'برای ردیف مطالب، دسته را انتخاب کنید.' };
      }
      sections.push(s);
    });

    if (problem) {
      e.preventDefault();
      toggleEdit(problem.li, true);
      flash(problem.li);
      problem.li.scrollIntoView({ behavior: 'smooth', block: 'center' });
      alert(problem.msg);
      return;
    }
    document.getElementById('sectionsJson').value = JSON.stringify(sections);
    isDirty = false;
  });

  window.addEventListener('beforeunload', function (e) {
    if (!isDirty) return;
    e.preventDefault();
    e.returnValue = '';
  });
})();
