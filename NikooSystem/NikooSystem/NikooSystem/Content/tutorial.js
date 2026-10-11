(function () {
  'use strict';

  var dataEl = document.getElementById('tutData');
  var launch = document.getElementById('tutLaunch');
  if (!dataEl || !launch) return;

  var tour;
  try { tour = JSON.parse(dataEl.textContent); } catch (e) { return; }
  if (!tour || !Array.isArray(tour.steps)) return;

  var seenKey = 'tut-seen-' + launch.dataset.page;
  var state = null;

  function faNum(n) {
    return String(n).replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; });
  }

  function el(tag, cls, html) {
    var node = document.createElement(tag);
    if (cls) node.className = cls;
    if (html != null) node.innerHTML = html;
    return node;
  }

  function text(tag, cls, value) {
    var node = el(tag, cls);
    node.textContent = value || '';
    return node;
  }

  function isVisible(node) {
    if (!node) return false;
    var r = node.getBoundingClientRect();
    return r.width > 0 && r.height > 0 && getComputedStyle(node).visibility !== 'hidden';
  }

  function gridColumn(node) {
    for (var n = node; n && n !== document.body; n = n.parentElement) {
      for (var i = 0; i < n.classList.length; i++) {
        if (/^col(-|$)/.test(n.classList[i])) return n;
      }
    }
    return node;
  }

  function firstVisible(selector) {
    var list;
    try { list = document.querySelectorAll(selector); } catch (e) { return null; }
    for (var i = 0; i < list.length; i++) if (isVisible(list[i])) return list[i];
    return null;
  }

  function findTarget(step) {
    if (step.field) {
      var input = firstVisible('[name="' + String(step.field).replace(/["\\]/g, '') + '"]');
      return input ? gridColumn(input) : null;
    }
    return step.target ? firstVisible(step.target) : null;
  }

  function isUntargeted(step) {
    return !step.field && !step.target;
  }

  function buildSteps(autoplay) {
    var steps = [];
    if (autoplay) steps.push({ intro: true, title: tour.title, text: tour.intro || '' });
    tour.steps.forEach(function (s) {
      if (isUntargeted(s) || findTarget(s)) steps.push(s);
    });
    if (autoplay) steps.push({ intro: true, title: 'پایان آموزش', text: tour.outro || 'هر وقت لازم شد، دکمه «آموزش این صفحه» را بزنید.' });
    return steps;
  }

  function buildUi(autoplay) {
    var root = el('div', 'tut-root' + (autoplay ? ' tut-auto' : ''));
    root.setAttribute('dir', 'rtl');
    var block = el('div', 'tut-block');
    var spot = el('div', 'tut-spot');
    var pop = el('div', 'tut-pop');
    pop.setAttribute('role', 'dialog');
    pop.setAttribute('aria-live', 'polite');

    var close = el('button', 'tut-close', '<i class="bi bi-x-lg"></i>');
    close.type = 'button';
    close.setAttribute('aria-label', 'بستن آموزش');
    var count = el('div', 'tut-count');
    var title = el('h4', 'tut-title');
    var body = el('p', 'tut-text');
    var bar = el('div', 'tut-bar', '<span></span>');
    var nav = el('div', 'tut-nav');
    var prev = el('button', 'tut-prev', '<i class="bi bi-arrow-right"></i> قبلی');
    var next = el('button', 'tut-next');
    prev.type = next.type = 'button';
    nav.appendChild(prev);
    nav.appendChild(next);

    pop.appendChild(close);
    pop.appendChild(count);
    pop.appendChild(title);
    pop.appendChild(body);
    pop.appendChild(bar);
    pop.appendChild(nav);
    root.appendChild(block);
    root.appendChild(spot);
    root.appendChild(pop);

    var cursor = null;
    if (autoplay) {
      cursor = el('div', 'tut-cursor', '<i class="bi bi-cursor-fill"></i>');
      cursor.style.transform = 'translate(' + (innerWidth / 2) + 'px,' + (innerHeight / 2) + 'px)';
      root.appendChild(cursor);
    }

    close.addEventListener('click', stop);
    prev.addEventListener('click', function () { go(state.index - 1); });
    next.addEventListener('click', function () { go(state.index + 1); });

    document.body.appendChild(root);
    return { root: root, spot: spot, pop: pop, count: count, title: title, body: body, bar: bar.firstChild, prev: prev, next: next, cursor: cursor };
  }

  function placeSpot(rect) {
    var ui = state.ui;
    if (!rect) {
      ui.spot.style.cssText = 'top:' + (innerHeight / 2) + 'px;left:' + (innerWidth / 2) + 'px;width:0;height:0;';
      return;
    }
    var pad = 6;
    var top = Math.max(rect.top - pad, 4);
    var left = Math.max(rect.left - pad, 4);
    var bottom = Math.min(rect.bottom + pad, innerHeight - 4);
    var right = Math.min(rect.right + pad, innerWidth - 4);
    ui.spot.style.cssText = 'top:' + top + 'px;left:' + left + 'px;width:' + Math.max(right - left, 0) + 'px;height:' + Math.max(bottom - top, 0) + 'px;';
  }

  function placePop(rect) {
    var pop = state.ui.pop;
    var margin = 14;
    var w = pop.offsetWidth;
    var h = pop.offsetHeight;
    var top, left;

    if (!rect) {
      top = (innerHeight - h) / 2;
      left = (innerWidth - w) / 2;
    } else if (rect.bottom + margin + h < innerHeight) {
      top = rect.bottom + margin;
    } else if (rect.top - margin - h > 0) {
      top = rect.top - margin - h;
    } else if (rect.left - margin - w > 0) {
      top = Math.min(Math.max(rect.top, margin), innerHeight - h - margin);
      left = rect.left - margin - w;
    } else {
      top = innerHeight - h - margin;
      left = margin;
    }
    if (left == null) left = rect.right - w;
    left = Math.min(Math.max(left, margin), innerWidth - w - margin);
    top = Math.min(Math.max(top, margin), innerHeight - h - margin);
    pop.style.top = top + 'px';
    pop.style.left = left + 'px';
  }

  function layout() {
    if (!state) return;
    var target = state.target;
    var rect = target && target.isConnected ? target.getBoundingClientRect() : null;
    placeSpot(rect);
    placePop(rect);
    if (state.ui.cursor && rect) {
      var x = rect.left + Math.min(rect.width / 2, 120);
      var y = rect.top + Math.min(rect.height / 2, 60);
      state.ui.cursor.style.transform = 'translate(' + x + 'px,' + y + 'px)';
    }
  }

  function stepDuration(step) {
    var length = ((step.title || '') + (step.text || '')).length;
    return Math.min(Math.max(2600 + length * 55, 3800), 10000);
  }

  function go(index) {
    if (!state) return;
    if (index < 0) return;
    if (index >= state.steps.length) { finish(); return; }

    clearTimeout(state.timer);
    state.index = index;
    var step = state.steps[index];
    var ui = state.ui;
    var target = step.intro ? null : findTarget(step);
    state.target = target;

    var numbered = state.steps.filter(function (s) { return !s.intro; });
    var position = numbered.indexOf(step) + 1;
    ui.count.textContent = position > 0 ? 'مرحله ' + faNum(position) + ' از ' + faNum(numbered.length) : '';
    ui.title.textContent = step.title || '';
    ui.body.textContent = step.text || '';
    ui.pop.classList.toggle('is-intro', !!step.intro);
    ui.prev.disabled = index === 0;
    ui.next.innerHTML = index === state.steps.length - 1 ? 'پایان <i class="bi bi-check2"></i>' : 'بعدی <i class="bi bi-arrow-left"></i>';

    ui.pop.classList.remove('is-shown');
    placeSpot(target ? target.getBoundingClientRect() : null);
    if (target) target.scrollIntoView({ block: 'center', behavior: 'smooth' });

    setTimeout(function () {
      if (!state || state.index !== index) return;
      layout();
      ui.pop.classList.add('is-shown');
      if (state.autoplay) {
        var duration = stepDuration(step);
        ui.bar.style.transition = 'none';
        ui.bar.style.width = '0';
        void ui.bar.offsetWidth;
        ui.bar.style.transition = 'width ' + duration + 'ms linear';
        ui.bar.style.width = '100%';
        state.timer = setTimeout(function () { go(index + 1); }, duration);
      } else {
        ui.next.focus({ preventScroll: true });
      }
    }, target ? 450 : 120);
  }

  function onKey(e) {
    if (!state || state.autoplay) return;
    if (e.key === 'Escape') stop();
    else if (e.key === 'ArrowLeft' || e.key === 'Enter') { e.preventDefault(); go(state.index + 1); }
    else if (e.key === 'ArrowRight') { e.preventDefault(); go(state.index - 1); }
  }

  function onViewport() {
    if (state && !state.frame) {
      state.frame = requestAnimationFrame(function () {
        if (!state) return;
        state.frame = 0;
        layout();
      });
    }
  }

  function start(options) {
    stop();
    var autoplay = !!(options && options.autoplay);
    var steps = buildSteps(autoplay);
    if (!steps.length) return false;

    window.__tutorialDone = false;
    document.body.classList.add('tut-active');
    if (autoplay) document.body.classList.add('tut-recording');
    state = { steps: steps, index: -1, autoplay: autoplay, ui: buildUi(autoplay), timer: 0, frame: 0, target: null };
    document.addEventListener('keydown', onKey);
    addEventListener('resize', onViewport);
    addEventListener('scroll', onViewport, true);
    go(0);
    return true;
  }

  function stop() {
    if (!state) return;
    clearTimeout(state.timer);
    cancelAnimationFrame(state.frame);
    state.ui.root.remove();
    state = null;
    document.removeEventListener('keydown', onKey);
    removeEventListener('resize', onViewport);
    removeEventListener('scroll', onViewport, true);
    document.body.classList.remove('tut-active', 'tut-recording');
  }

  function finish() {
    stop();
    window.__tutorialDone = true;
    document.dispatchEvent(new CustomEvent('tutorial:done'));
  }

  function openVideo(url) {
    var modal = el('div', 'tut-modal');
    modal.setAttribute('role', 'dialog');
    modal.setAttribute('aria-modal', 'true');
    var box = el('div', 'tut-modal-box');
    var head = el('div', 'tut-modal-head');
    head.appendChild(text('strong', '', 'آموزش: ' + (launch.dataset.title || '')));
    var closeBtn = el('button', 'btn btn-light btn-sm', '<i class="bi bi-x-lg"></i>');
    closeBtn.type = 'button';
    closeBtn.setAttribute('aria-label', 'بستن');
    head.appendChild(closeBtn);

    var video = el('video', 'tut-video');
    video.controls = true;
    video.autoplay = true;
    video.playsInline = true;
    video.preload = 'metadata';
    video.src = url;

    var foot = el('div', 'tut-modal-foot');
    var note = text('span', 'small text-muted', 'می‌خواهید همین مراحل را روی صفحه خودتان دنبال کنید؟');
    var guide = el('button', 'btn-store-sm', '<i class="bi bi-hand-index-thumb"></i> راهنمای قدم‌به‌قدم');
    guide.type = 'button';
    foot.appendChild(note);
    foot.appendChild(guide);

    box.appendChild(head);
    box.appendChild(video);
    box.appendChild(foot);
    modal.appendChild(box);
    document.body.appendChild(modal);

    function close() {
      video.pause();
      modal.remove();
      document.removeEventListener('keydown', onEsc);
    }
    function onEsc(e) { if (e.key === 'Escape') close(); }

    video.addEventListener('error', function () {
      note.textContent = 'مرورگر شما این ویدیو را پخش نمی‌کند؛ از راهنمای قدم‌به‌قدم استفاده کنید.';
      video.hidden = true;
    });
    closeBtn.addEventListener('click', close);
    modal.addEventListener('click', function (e) { if (e.target === modal) close(); });
    document.addEventListener('keydown', onEsc);
    guide.addEventListener('click', function () { close(); start(); });
  }

  launch.addEventListener('click', function () {
    try { localStorage.setItem(seenKey, '1'); } catch (e) { }
    launch.classList.remove('is-new');
    if (launch.dataset.video) openVideo(launch.dataset.video);
    else start();
  });

  try { if (!localStorage.getItem(seenKey)) launch.classList.add('is-new'); } catch (e) { }

  window.NikooTutorial = { start: start, stop: stop };
})();
