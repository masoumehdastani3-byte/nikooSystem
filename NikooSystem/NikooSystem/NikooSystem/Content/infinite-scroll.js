/*
 * Infinite scroll for server-paged lists. A container marked [data-infinite] holds the items of the first page followed
 * by a .infinite-next[data-next] marker; when the marker scrolls into view the next page is fetched (the server answers
 * AJAX requests with the items and the following marker only) and appended in its place.
 */
(function () {
  function load(marker) {
    if (marker.dataset.loading) return;
    marker.dataset.loading = '1';
    var link = marker.querySelector('a');
    var spinner = marker.querySelector('.infinite-loading');
    if (link) link.hidden = true;
    if (spinner) spinner.hidden = false;

    fetch(marker.getAttribute('data-next'), { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' })
      .then(function (res) {
        if (!res.ok) throw new Error(res.status);
        return res.text();
      })
      .then(function (html) {
        var tpl = document.createElement('template');
        tpl.innerHTML = html;
        var container = marker.parentNode;
        container.insertBefore(tpl.content, marker);
        marker.remove();
        watch(container);
      })
      .catch(function () {
        delete marker.dataset.loading;
        if (spinner) spinner.hidden = true;
        if (link) link.hidden = false;
      });
  }

  var observer = 'IntersectionObserver' in window
    ? new IntersectionObserver(function (entries) {
        entries.forEach(function (e) {
          if (e.isIntersecting) {
            observer.unobserve(e.target);
            load(e.target);
          }
        });
      }, { rootMargin: '600px 0px' })
    : null;

  function watch(container) {
    container.querySelectorAll('.infinite-next[data-next]').forEach(function (marker) {
      if (observer) observer.observe(marker);
    });
  }

  document.addEventListener('click', function (e) {
    var link = e.target.closest('.infinite-next a');
    if (!link) return;
    e.preventDefault();
    load(link.closest('.infinite-next'));
  });

  document.querySelectorAll('[data-infinite]').forEach(watch);
})();
