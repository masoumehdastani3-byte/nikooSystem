/* Support tickets: image attachments of the reply forms (Views/Shared/_SupportUpload.cshtml) and the image viewer of the conversation */
(function () {
    'use strict';

    var MAX_SIDE = 1920;
    var KEEP_BYTES = 1.5 * 1024 * 1024;
    var PNG_MAX_BYTES = 4 * 1024 * 1024;
    var TYPES = ['image/jpeg', 'image/png', 'image/gif'];

    var canSetFiles = (function () {
        try { return !!new DataTransfer(); } catch (e) { return false; }
    })();

    function fa(value) {
        return String(value).replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; });
    }

    function formatSize(bytes) {
        return bytes >= 1024 * 1024 ? fa((bytes / 1024 / 1024).toFixed(1)) + ' مگابایت' : fa(Math.max(1, Math.round(bytes / 1024))) + ' کیلوبایت';
    }

    function loadImage(file) {
        return new Promise(function (resolve, reject) {
            var url = URL.createObjectURL(file);
            var img = new Image();
            img.onload = function () { resolve({ img: img, url: url }); };
            img.onerror = function () { URL.revokeObjectURL(url); reject(new Error('decode')); };
            img.src = url;
        });
    }

    function encode(canvas, type) {
        return new Promise(function (resolve) { canvas.toBlob(resolve, type, 0.86); });
    }

    // Phone photos are scaled down here so a reply uploads in a moment; screenshots stay PNG for sharp text, GIFs are left alone.
    function shrink(file) {
        if (file.type === 'image/gif') return Promise.resolve(file);
        var png = file.type === 'image/png';
        return loadImage(file).then(function (loaded) {
            var img = loaded.img;
            var scale = Math.min(1, MAX_SIDE / Math.max(img.naturalWidth, img.naturalHeight));
            if (scale === 1 && (png || file.size <= KEEP_BYTES)) {
                URL.revokeObjectURL(loaded.url);
                return file;
            }
            var canvas = document.createElement('canvas');
            canvas.width = Math.round(img.naturalWidth * scale);
            canvas.height = Math.round(img.naturalHeight * scale);
            var ctx = canvas.getContext('2d');
            if (!png) {
                ctx.fillStyle = '#fff';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
            }
            ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
            URL.revokeObjectURL(loaded.url);
            return encode(canvas, png ? 'image/png' : 'image/jpeg').then(function (blob) {
                if (blob && png && blob.size > PNG_MAX_BYTES) return encode(canvas, 'image/jpeg');
                return blob;
            }).then(function (blob) {
                if (!blob || blob.size >= file.size) return file;
                var name = (file.name || 'image').replace(/\.[^.]+$/, '') + (blob.type === 'image/png' ? '.png' : '.jpg');
                return new File([blob], name, { type: blob.type });
            });
        }).catch(function () { return file; });
    }

    function named(file) {
        if (file.name) return file;
        return new File([file], 'screenshot.' + (file.type === 'image/jpeg' ? 'jpg' : file.type === 'image/gif' ? 'gif' : 'png'), { type: file.type });
    }

    function setup(box) {
        var input = box.querySelector('input[type=file]');
        var list = box.querySelector('.sp-upload-list');
        var max = parseInt(box.getAttribute('data-max'), 10) || 4;
        var form = box.closest('form');
        var textarea = form && form.querySelector('textarea');
        var files = [];
        var busy = 0;
        var noteTimer;

        // without DataTransfer the plain file input still works, only without previews
        if (!input || !canSetFiles) return;
        box.classList.add('is-enhanced');

        var noteEl = document.createElement('div');
        noteEl.className = 'sp-upload-note';
        box.appendChild(noteEl);

        function note(text) {
            noteEl.textContent = text;
            clearTimeout(noteTimer);
            noteTimer = setTimeout(function () { noteEl.textContent = ''; }, 5000);
        }

        function render() {
            list.innerHTML = '';
            files.forEach(function (file, i) {
                var item = document.createElement('div');
                item.className = 'sp-upload-item';
                var img = document.createElement('img');
                img.alt = '';
                img.src = URL.createObjectURL(file);
                img.onload = function () { URL.revokeObjectURL(img.src); };
                var size = document.createElement('span');
                size.className = 'sp-upload-size';
                size.textContent = formatSize(file.size);
                var remove = document.createElement('button');
                remove.type = 'button';
                remove.className = 'sp-upload-remove';
                remove.title = 'حذف تصویر';
                remove.innerHTML = '<i class="bi bi-x"></i>';
                remove.addEventListener('click', function () {
                    files.splice(i, 1);
                    sync();
                });
                item.appendChild(img);
                item.appendChild(size);
                item.appendChild(remove);
                list.appendChild(item);
            });
            for (var i = 0; i < busy; i++) {
                var wait = document.createElement('div');
                wait.className = 'sp-upload-item is-busy';
                list.appendChild(wait);
            }
            box.classList.toggle('is-full', files.length + busy >= max);
        }

        function sync() {
            var dt = new DataTransfer();
            files.forEach(function (f) { dt.items.add(f); });
            input.files = dt.files;
            render();
        }

        function add(incoming) {
            var all = Array.prototype.slice.call(incoming);
            var picked = all.filter(function (f) { return TYPES.indexOf(f.type) >= 0; });
            if (picked.length < all.length) note('فقط تصویر JPG، PNG یا GIF پذیرفته می‌شود.');
            var room = Math.max(0, max - files.length - busy);
            if (picked.length > room) {
                note('هر پیام حداکثر ' + fa(max) + ' تصویر می‌تواند داشته باشد.');
                picked = picked.slice(0, room);
            }
            busy += picked.length;
            sync();
            picked.forEach(function (file) {
                shrink(named(file)).then(function (small) {
                    files.push(small);
                    busy--;
                    sync();
                });
            });
        }

        input.addEventListener('change', function () {
            add(input.files);
        });

        if (textarea) {
            textarea.addEventListener('paste', function (e) {
                var pasted = e.clipboardData ? Array.prototype.slice.call(e.clipboardData.files || []) : [];
                var images = pasted.filter(function (f) { return f.type.indexOf('image/') === 0; });
                if (!images.length) return;
                e.preventDefault();
                add(images);
            });
        }

        if (form) {
            form.addEventListener('dragover', function (e) {
                if (!e.dataTransfer || Array.prototype.indexOf.call(e.dataTransfer.types, 'Files') < 0) return;
                e.preventDefault();
                box.classList.add('is-drop');
            });
            form.addEventListener('dragleave', function (e) {
                if (!form.contains(e.relatedTarget)) box.classList.remove('is-drop');
            });
            form.addEventListener('drop', function (e) {
                if (!e.dataTransfer || !e.dataTransfer.files.length) return;
                e.preventDefault();
                box.classList.remove('is-drop');
                add(e.dataTransfer.files);
            });
            form.addEventListener('submit', function (e) {
                if (busy) {
                    e.preventDefault();
                    note('چند لحظه صبر کنید تا تصویرها آماده شوند.');
                }
            });
        }
    }

    Array.prototype.forEach.call(document.querySelectorAll('[data-sp-upload]'), setup);

    // images of the conversation open over the page; Ctrl/middle click still opens them in a new tab
    function closeViewer() {
        var open = document.querySelector('.sp-lightbox');
        if (open) open.parentNode.removeChild(open);
    }

    document.addEventListener('click', function (e) {
        var link = e.target.closest && e.target.closest('.sp-image');
        if (!link || e.ctrlKey || e.metaKey || e.shiftKey || e.button) return;
        e.preventDefault();
        var viewer = document.createElement('div');
        viewer.className = 'sp-lightbox';
        viewer.setAttribute('role', 'dialog');
        viewer.innerHTML = '<button type="button" class="sp-lightbox-close" aria-label="بستن"><i class="bi bi-x"></i></button><img alt="تصویر پیوست" />';
        viewer.querySelector('img').src = link.href;
        viewer.addEventListener('click', closeViewer);
        document.body.appendChild(viewer);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') closeViewer();
    });
})();
