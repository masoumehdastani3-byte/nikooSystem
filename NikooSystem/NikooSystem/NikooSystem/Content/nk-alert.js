/*
  Messages of the whole project (styles in nk-alert.css):
  - nk.alert(text, options) and nk.confirm(text, options) open a dialog and return a Promise (confirm resolves to true/false);
    options: title, ok, cancel, tone ("danger", "warning", "success", "info"), icon (a Bootstrap Icons class).
  - nk.toast(text, tone) shows a message in the corner that closes by itself.
  - A form or button with data-confirm="question" asks in the dialog before submitting or following the link;
    data-confirm-tone, data-confirm-ok and data-confirm-title change the dialog.
  - The .nk-flash messages of FlashMessages become toasts.
*/
(function () {
  if (window.nk && window.nk.confirm) return;

  var icons = {
    danger: 'bi-exclamation-octagon-fill',
    warning: 'bi-exclamation-triangle-fill',
    success: 'bi-check-circle-fill',
    info: 'bi-info-circle-fill',
    question: 'bi-question-circle-fill'
  };
  var queue = Promise.resolve();

  function el(tag, cls, text) {
    var node = document.createElement(tag);
    if (cls) node.className = cls;
    if (text != null) node.textContent = text;
    return node;
  }

  function dialog(text, options, withCancel) {
    options = options || {};
    var run = function () {
      return new Promise(function (resolve) {
        var tone = options.tone || (withCancel ? 'danger' : 'info');
        var backdrop = el('div', 'nk-dialog-backdrop');
        var box = el('div', 'nk-dialog is-' + tone);
        box.setAttribute('role', withCancel ? 'alertdialog' : 'dialog');
        box.setAttribute('aria-modal', 'true');

        var icon = el('div', 'nk-dialog-icon');
        icon.appendChild(el('i', 'bi ' + (options.icon || (withCancel && tone === 'danger' ? 'bi-trash' : icons[tone] || icons.info))));
        box.appendChild(icon);
        var title = options.title || (withCancel ? 'مطمئن هستید؟' : null);
        if (title) {
          var heading = el('h5', 'nk-dialog-title', title);
          heading.id = 'nkDialogTitle';
          box.setAttribute('aria-labelledby', heading.id);
          box.appendChild(heading);
        }
        var body = el('p', 'nk-dialog-text', text || '');
        body.id = 'nkDialogText';
        box.setAttribute('aria-describedby', body.id);
        box.appendChild(body);

        var actions = el('div', 'nk-dialog-actions');
        var ok = el('button', 'nk-dialog-ok', options.ok || (withCancel ? 'بله' : 'باشه'));
        ok.type = 'button';
        actions.appendChild(ok);
        var cancel = null;
        if (withCancel) {
          cancel = el('button', 'nk-dialog-cancel', options.cancel || 'انصراف');
          cancel.type = 'button';
          actions.appendChild(cancel);
        }
        box.appendChild(actions);
        backdrop.appendChild(box);

        var before = document.activeElement;
        var done = false;
        function close(answer) {
          if (done) return;
          done = true;
          document.removeEventListener('keydown', onKey, true);
          backdrop.classList.add('is-leaving');
          setTimeout(function () {
            backdrop.remove();
            if (!document.querySelector('.nk-dialog-backdrop')) document.body.classList.remove('nk-dialog-open');
            if (before && before.focus) try { before.focus({ preventScroll: true }); } catch (e) { }
            resolve(answer);
          }, 150);
        }
        function onKey(e) {
          if (e.key === 'Escape') { e.preventDefault(); close(!withCancel); }
          else if (e.key === 'Tab') {
            var items = cancel ? [ok, cancel] : [ok];
            var i = items.indexOf(document.activeElement);
            e.preventDefault();
            items[(i + (e.shiftKey ? items.length - 1 : 1)) % items.length].focus();
          }
        }

        ok.addEventListener('click', function () { close(true); });
        if (cancel) cancel.addEventListener('click', function () { close(false); });
        backdrop.addEventListener('mousedown', function (e) { if (e.target === backdrop) close(!withCancel); });
        document.addEventListener('keydown', onKey, true);

        // Inside an open Bootstrap modal, whose focus trap would not let the buttons take focus.
        (document.querySelector('.modal.show') || document.body).appendChild(backdrop);
        document.body.classList.add('nk-dialog-open');
        (tone === 'danger' && cancel ? cancel : ok).focus();
      });
    };
    var shown = queue.then(run);
    queue = shown.catch(function () { });
    return shown;
  }

  function toastBox() {
    var box = document.querySelector('.nk-toasts');
    if (!box) {
      box = el('div', 'nk-toasts');
      box.setAttribute('aria-live', 'polite');
      document.body.appendChild(box);
    }
    return box;
  }

  function showToast(alert, seconds) {
    alert.classList.remove('nk-flash');
    var close = el('button', 'nk-toast-close');
    close.type = 'button';
    close.setAttribute('aria-label', 'بستن');
    close.appendChild(el('i', 'bi bi-x-lg'));
    alert.appendChild(close);
    var bar = el('span', 'nk-toast-bar');
    bar.style.animationDuration = seconds + 's';
    alert.appendChild(bar);
    toastBox().appendChild(alert);

    var gone = false;
    function hide() {
      if (gone) return;
      gone = true;
      alert.classList.add('is-leaving');
      setTimeout(function () { alert.remove(); }, 220);
    }
    close.addEventListener('click', hide);
    bar.addEventListener('animationend', hide);
    return alert;
  }

  function toast(text, tone) {
    var alert = el('div', 'alert alert-' + (tone || 'success'), text);
    alert.setAttribute('role', tone === 'danger' ? 'alert' : 'status');
    return showToast(alert, tone === 'danger' ? 10 : 6);
  }

  function flashToToasts() {
    Array.prototype.forEach.call(document.querySelectorAll('.alert.nk-flash'), function (alert) {
      var wrapper = alert.parentElement;
      showToast(alert, alert.classList.contains('alert-danger') ? 12 : 7);
      if (wrapper && wrapper !== document.body && !wrapper.children.length && !wrapper.textContent.trim()) wrapper.remove();
    });
  }

  function confirmOptions(node) {
    return {
      tone: node.getAttribute('data-confirm-tone') || undefined,
      ok: node.getAttribute('data-confirm-ok') || undefined,
      title: node.getAttribute('data-confirm-title') || undefined
    };
  }

  function submitWith(form, submitter) {
    form.setAttribute('data-nk-confirmed', '1');
    if (form.requestSubmit) {
      form.requestSubmit(submitter && submitter.form === form ? submitter : undefined);
    } else {
      if (submitter && submitter.name) {
        var input = el('input');
        input.type = 'hidden';
        input.name = submitter.name;
        input.value = submitter.value;
        form.appendChild(input);
      }
      form.submit();
    }
    setTimeout(function () { form.removeAttribute('data-nk-confirmed'); }, 0);
  }

  // Buttons and links with their own question (the form may have none).
  document.addEventListener('click', function (e) {
    var node = e.target.closest && e.target.closest('[data-confirm]:not(form)');
    if (!node || node.getAttribute('data-nk-confirmed')) return;
    e.preventDefault();
    e.stopPropagation();
    dialog(node.getAttribute('data-confirm'), confirmOptions(node), true).then(function (yes) {
      if (!yes) return;
      if (node.form && node.type === 'submit') {
        submitWith(node.form, node);
      } else {
        node.setAttribute('data-nk-confirmed', '1');
        node.click();
        node.removeAttribute('data-nk-confirmed');
      }
    });
  }, true);

  document.addEventListener('submit', function (e) {
    var form = e.target;
    if (!form.matches || !form.matches('form[data-confirm]')) return;
    if (form.getAttribute('data-nk-confirmed')) return;
    e.preventDefault();
    e.stopPropagation();
    var submitter = e.submitter;
    dialog(form.getAttribute('data-confirm'), confirmOptions(form), true).then(function (yes) {
      if (yes) submitWith(form, submitter);
    });
  }, true);

  window.nk = {
    alert: function (text, options) { return dialog(text, options, false); },
    confirm: function (text, options) { return dialog(text, options, true); },
    toast: toast
  };

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', flashToToasts);
  else flashToToasts();
})();
