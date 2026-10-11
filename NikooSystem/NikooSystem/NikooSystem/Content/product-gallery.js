(function () {
  'use strict';

  // The small image is shown first; the large one is fetched in the background and swapped in once ready.
  function upgrade(img) {
    if (!img || img.dataset.state) return;
    if (!img.getAttribute('src') && img.dataset.low) img.src = img.dataset.low;
    var large = img.dataset.large;
    if (!large || large === img.getAttribute('src')) {
      img.classList.remove('is-low');
      img.dataset.state = 'done';
      return;
    }
    img.dataset.state = 'loading';
    var pre = new Image();
    pre.onload = function () {
      img.src = large;
      img.classList.remove('is-low');
      img.dataset.state = 'done';
    };
    pre.onerror = function () {
      img.classList.remove('is-low');
      img.dataset.state = 'done';
    };
    pre.src = large;
  }

  function Carousel(root, onChange, lazy) {
    var self = this;
    var track = root.querySelector('.pg-track');
    var slides = Array.prototype.slice.call(track.querySelectorAll('.pg-slide'));
    var counter = root.querySelector('.pg-counter b');
    var index = 0;
    var drag = null;
    var scrollTimer = null;
    var jumpTimer = null;

    self.suppressClick = false;
    self.count = slides.length;
    self.index = function () { return index; };

    function isRtl() { return getComputedStyle(track).direction === 'rtl'; }

    function setActive(i) {
      if (i === index && track.dataset.ready) return;
      track.dataset.ready = '1';
      index = i;
      if (counter) counter.textContent = i + 1;
      [i, i + 1, i - 1].forEach(function (k) {
        if (slides[k]) upgrade(slides[k].querySelector('img'));
      });
      if (onChange) onChange(i);
    }

    self.goTo = function (i, instant) {
      i = Math.max(0, Math.min(slides.length - 1, i));
      var delta = slides[i].getBoundingClientRect().left - track.getBoundingClientRect().left;
      if (Math.abs(delta) > 1) {
        // scroll-snap-stop:always keeps swipes to one image, but would halt a multi-image jump halfway
        track.classList.add('is-jumping');
        clearTimeout(jumpTimer);
        jumpTimer = setTimeout(function () { track.classList.remove('is-jumping'); }, 700);
        track.scrollBy({ left: delta, behavior: instant ? 'auto' : 'smooth' });
      }
      setActive(i);
    };

    track.addEventListener('scroll', function () {
      if (drag) return;
      clearTimeout(scrollTimer);
      scrollTimer = setTimeout(function () {
        var i = Math.round(Math.abs(track.scrollLeft) / track.clientWidth);
        setActive(Math.max(0, Math.min(slides.length - 1, i)));
      }, 60);
    }, { passive: true });

    var prev = root.querySelector('.pg-prev');
    var next = root.querySelector('.pg-next');
    if (prev) prev.addEventListener('click', function () { self.goTo(index - 1); });
    if (next) next.addEventListener('click', function () { self.goTo(index + 1); });

    self.handleKey = function (e) {
      var forward = isRtl() ? 'ArrowLeft' : 'ArrowRight';
      var back = isRtl() ? 'ArrowRight' : 'ArrowLeft';
      if (e.key === forward) { e.preventDefault(); self.goTo(index + 1); }
      else if (e.key === back) { e.preventDefault(); self.goTo(index - 1); }
    };
    track.addEventListener('keydown', self.handleKey);

    // Touch devices swipe natively through scroll-snap; mouse users get drag-to-swipe.
    track.addEventListener('pointerdown', function (e) {
      if (e.pointerType !== 'mouse' || e.button !== 0 || slides.length < 2) return;
      drag = { x: e.clientX, left: track.scrollLeft, start: index, moved: false, id: e.pointerId };
    });
    track.addEventListener('pointermove', function (e) {
      if (!drag) return;
      var dx = e.clientX - drag.x;
      if (!drag.moved && Math.abs(dx) > 6) {
        drag.moved = true;
        track.classList.add('is-dragging');
        track.setPointerCapture(drag.id);
      }
      if (drag.moved) track.scrollLeft = drag.left - dx;
    });
    function endDrag(e) {
      if (!drag) return;
      var d = drag;
      drag = null;
      if (!d.moved) return;
      var dx = e.clientX - d.x;
      var target = d.start;
      if (Math.abs(dx) > track.clientWidth * 0.12) {
        var forward = isRtl() ? dx > 0 : dx < 0;
        target = forward ? d.start + 1 : d.start - 1;
      }
      self.suppressClick = true;
      setTimeout(function () { self.suppressClick = false; }, 80);
      self.goTo(target);
      setTimeout(function () { track.classList.remove('is-dragging'); }, 450);
    }
    track.addEventListener('pointerup', endDrag);
    track.addEventListener('pointercancel', endDrag);

    window.addEventListener('resize', function () {
      if (track.clientWidth > 0) self.goTo(index, true);
    });

    if (!lazy) setActive(0);
  }

  var gallery = document.getElementById('productGallery');
  if (!gallery) return;

  var thumbs = Array.prototype.slice.call(gallery.querySelectorAll('.pg-thumb'));
  var thumbStrip = gallery.querySelector('.pg-thumbs');

  var main = new Carousel(gallery, function (i) {
    thumbs.forEach(function (t, k) { t.classList.toggle('active', k === i); });
    var t = thumbs[i];
    if (t && thumbStrip) {
      var sr = thumbStrip.getBoundingClientRect();
      var tr = t.getBoundingClientRect();
      if (tr.left < sr.left || tr.right > sr.right)
        thumbStrip.scrollBy({ left: tr.left - sr.left - (sr.width - tr.width) / 2, behavior: 'smooth' });
    }
  });

  thumbs.forEach(function (t) {
    t.addEventListener('click', function () { main.goTo(parseInt(t.dataset.index, 10)); });
  });

  // ---------- full-screen viewer ----------
  var lb = document.getElementById('pgLightbox');
  var viewer = lb ? new Carousel(lb, null, true) : null;

  function openViewer(i) {
    if (!viewer) return;
    lb.hidden = false;
    document.body.classList.add('pg-lock');
    lb.querySelectorAll('img[data-low]').forEach(function (img) {
      if (!img.getAttribute('src')) img.src = img.dataset.low;
    });
    requestAnimationFrame(function () { viewer.goTo(i, true); });
    lb.querySelector('.pg-lb-close').focus();
  }

  function closeViewer() {
    if (!viewer || lb.hidden) return;
    lb.hidden = true;
    document.body.classList.remove('pg-lock');
    main.goTo(viewer.index(), true);
  }

  gallery.querySelector('.pg-track').addEventListener('click', function (e) {
    if (main.suppressClick || !e.target.classList.contains('pg-img')) return;
    openViewer(main.index());
  });
  var expand = gallery.querySelector('.pg-expand');
  if (expand) expand.addEventListener('click', function () { openViewer(main.index()); });

  if (lb) {
    lb.querySelector('.pg-lb-close').addEventListener('click', closeViewer);
    lb.addEventListener('click', function (e) {
      if (viewer.suppressClick) return;
      if (e.target.classList.contains('pg-slide') || e.target === lb) closeViewer();
    });
    document.addEventListener('keydown', function (e) {
      if (lb.hidden) return;
      if (e.key === 'Escape') closeViewer();
      else viewer.handleKey(e);
    });
  }

  // ---------- quantity stepper ----------
  document.querySelectorAll('.pd-qty').forEach(function (box) {
    var input = box.querySelector('input');
    var max = parseInt(box.dataset.max, 10) || 999;
    function clamp(v) { return Math.max(1, Math.min(max, isNaN(v) ? 1 : v)); }
    box.querySelectorAll('[data-step]').forEach(function (b) {
      b.addEventListener('click', function () {
        input.value = clamp(parseInt(input.value, 10) + parseInt(b.dataset.step, 10));
      });
    });
    input.addEventListener('change', function () { input.value = clamp(parseInt(input.value, 10)); });
  });

  // ---------- "n reviews" link opens the reviews tab ----------
  document.querySelectorAll('[data-open-tab]').forEach(function (a) {
    a.addEventListener('click', function (e) {
      var btn = document.querySelector('[data-bs-target="#' + a.dataset.openTab + '"]');
      if (!btn || !window.bootstrap) return;
      e.preventDefault();
      window.bootstrap.Tab.getOrCreateInstance(btn).show();
      document.getElementById('pdTabs').scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  });
})();
