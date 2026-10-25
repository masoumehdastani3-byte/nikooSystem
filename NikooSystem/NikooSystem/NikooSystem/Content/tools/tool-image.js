(function () {
    'use strict';

    var U = window.NkTools;
    var J = window.NkJalali;
    var root = document.getElementById('tool-image');
    if (!root) return;

    var fileInput = U.qs(root, '[data-img-file]');
    var drop = U.qs(root, '[data-img-drop]');
    var preview = U.qs(root, '[data-img-preview]');
    var meta = U.qs(root, '[data-img-meta]');
    var err = U.qs(root, '[data-img-err]');
    var source = null;
    var sourceName = 'image';

    function showError(msg) {
        err.textContent = msg || '';
    }

    function loadFile(file) {
        showError('');
        if (!file || !file.type.match(/^image\//)) {
            showError('فقط فایل تصویری انتخاب کنید.');
            return;
        }
        sourceName = (file.name || 'image').replace(/\.[^.]+$/, '');
        var reader = new FileReader();
        reader.onload = function () {
            var img = new Image();
            img.onload = function () {
                source = img;
                preview.src = reader.result;
                preview.hidden = false;
                meta.textContent = 'حجم اولیه: ' + U.formatBytes(file.size) + ' · ' + J.fa(img.width) + '×' + J.fa(img.height);
                process(false);
            };
            img.src = reader.result;
        };
        reader.readAsDataURL(file);
    }

    fileInput.addEventListener('change', function () {
        if (fileInput.files[0]) loadFile(fileInput.files[0]);
    });

    drop.addEventListener('dragover', function (e) {
        e.preventDefault();
        drop.classList.add('is-drag');
    });
    drop.addEventListener('dragleave', function () { drop.classList.remove('is-drag'); });
    drop.addEventListener('drop', function (e) {
        e.preventDefault();
        drop.classList.remove('is-drag');
        if (e.dataTransfer.files[0]) loadFile(e.dataTransfer.files[0]);
    });

    var outCanvas = document.createElement('canvas');
    var outUrl = null;

    function process(download) {
        if (!source) return;
        showError('');
        var quality = parseFloat(U.qs(root, '[data-img-quality]').value) || 0.85;
        var maxW = parseInt(U.parseNum(U.qs(root, '[data-img-maxw]').value), 10) || 1920;
        var format = U.qs(root, '[data-img-format]').value;
        var w = source.width;
        var h = source.height;
        if (w > maxW) {
            h = Math.round(h * maxW / w);
            w = maxW;
        }
        outCanvas.width = w;
        outCanvas.height = h;
        var ctx = outCanvas.getContext('2d');
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, w, h);
        ctx.drawImage(source, 0, 0, w, h);
        var mime = format === 'png' ? 'image/png' : format === 'webp' ? 'image/webp' : 'image/jpeg';
        var q = format === 'png' ? undefined : quality;
        outCanvas.toBlob(function (blob) {
            if (!blob) {
                showError('مرورگر شما این فرمت را پشتیبانی نمی‌کند؛ JPG را امتحان کنید.');
                return;
            }
            if (outUrl) URL.revokeObjectURL(outUrl);
            outUrl = URL.createObjectURL(blob);
            preview.src = outUrl;
            U.qs(root, '[data-img-result]').textContent =
                'حجم جدید: ' + U.formatBytes(blob.size) + ' · فرمت ' + format.toUpperCase();
            if (download) {
                var a = document.createElement('a');
                a.href = outUrl;
                a.download = sourceName + '.' + (format === 'jpeg' ? 'jpg' : format);
                a.click();
            }
        }, mime, q);
    }

    U.qs(root, '[data-img-process]').addEventListener('click', function () { process(false); });
    U.qs(root, '[data-img-download]').addEventListener('click', function () { process(true); });
    U.qs(root, '[data-img-quality]').addEventListener('input', function () {
        U.qs(root, '[data-img-quality-label]').textContent = J.fa(Math.round(this.value * 100)) + '٪';
    });
})();
