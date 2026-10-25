(function () {
    'use strict';

    var U = window.NkTools;
    var root = document.getElementById('tool-qr');
    if (!root || typeof QRCode === 'undefined') return;

    var box = U.qs(root, '[data-qr-box]');
    var qrInstance = null;
    var lastCanvas = null;

    function clearBox() {
        box.innerHTML = '';
        qrInstance = null;
        lastCanvas = null;
    }

    function render() {
        var text = U.qs(root, '[data-qr-text]').value.trim();
        var size = parseInt(U.qs(root, '[data-qr-size]').value, 10) || 200;
        var err = U.qs(root, '[data-qr-err]');
        err.textContent = '';
        clearBox();
        if (!text) {
            err.textContent = 'متن یا لینک را وارد کنید.';
            return;
        }
        if (text.length > 2000) {
            err.textContent = 'متن خیلی طولانی است؛ QR کوتاه‌تر کنید.';
            return;
        }
        try {
            qrInstance = new QRCode(box, {
                text: text,
                width: size,
                height: size,
                colorDark: '#0f172a',
                colorLight: '#ffffff',
                correctLevel: QRCode.CorrectLevel.M
            });
            setTimeout(function () {
                lastCanvas = box.querySelector('canvas') || box.querySelector('img');
            }, 50);
        } catch (e) {
            err.textContent = 'ساخت QR ممکن نشد.';
        }
    }

    U.qs(root, '[data-qr-build]').addEventListener('click', render);
    U.qs(root, '[data-qr-size]').addEventListener('input', function () {
        U.qs(root, '[data-qr-size-label]').textContent = window.NkJalali.fa(this.value);
    });

    U.qs(root, '[data-qr-download]').addEventListener('click', function () {
        if (!lastCanvas) {
            render();
            return;
        }
        var canvas = box.querySelector('canvas');
        if (!canvas && box.querySelector('img')) {
            var img = box.querySelector('img');
            canvas = document.createElement('canvas');
            canvas.width = img.naturalWidth || img.width;
            canvas.height = img.naturalHeight || img.height;
            canvas.getContext('2d').drawImage(img, 0, 0);
        }
        if (!canvas) return;
        canvas.toBlob(function (blob) {
            var a = document.createElement('a');
            a.href = URL.createObjectURL(blob);
            a.download = 'qrcode.png';
            a.click();
            URL.revokeObjectURL(a.href);
        }, 'image/png');
    });

    U.qsa(root, '[data-qr-preset]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            U.qs(root, '[data-qr-text]').value = btn.getAttribute('data-qr-preset');
            render();
        });
    });
})();
