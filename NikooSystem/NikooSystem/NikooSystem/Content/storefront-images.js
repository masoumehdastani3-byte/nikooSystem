/* "Smart" image fit of the storefront (see storefront-images.css): a picture fills its frame unless its shape is so far
   from the frame's that filling would cut most of it (a tall poster in a wide card, a panorama in a square); that one is
   shown whole instead. Pictures added later (infinite scroll, sliders) are handled when they load. */
(function () {
    var body = document.body;
    var fit = body && body.getAttribute('data-img-fit');
    var FRAMES = '.wm-product .thumb img, .product-card .thumb img, .gallery-item img, .cn-card-img img, .library-cover img, ' +
        '.bk-service-img, .cn-topic-lead img, .cn-list-item img, .cn-side-story img, .dl-lead img, .pg-thumb img, ' +
        '.cart-item-thumb img, .rh-about-media img';
    var LIMIT = 1.6;

    function inFrame(img) {
        return img && img.tagName === 'IMG' && img.matches && img.matches(FRAMES);
    }

    function check(img) {
        if (!img.naturalWidth || !img.naturalHeight) return;
        var w = img.clientWidth, h = img.clientHeight;
        if (!w || !h) return;
        var picture = img.naturalWidth / img.naturalHeight, frame = w / h;
        var off = picture > frame ? picture / frame : frame / picture;
        img.classList.toggle('fit-contain', off > LIMIT);
    }

    function broken(img) {
        if (inFrame(img)) img.classList.add('img-broken');
    }

    document.addEventListener('error', function (e) { broken(e.target); }, true);

    if (fit !== 'auto') return;

    document.addEventListener('load', function (e) {
        if (inFrame(e.target)) check(e.target);
    }, true);

    function all() {
        document.querySelectorAll(FRAMES).forEach(function (img) {
            if (img.complete) check(img);
        });
    }

    all();
    var timer;
    window.addEventListener('resize', function () {
        clearTimeout(timer);
        timer = setTimeout(all, 200);
    });
})();
