/* Upload space of the store panel: when the space is full, choosing a file opens the "buy more space" dialog instead of
   the file picker; a selection larger than the free space is cleared with the same dialog. The server checks again on save.
   Inputs marked data-no-quota (ticket attachments) are not limited. */
(function () {
    var body = document.body;
    if (!body || !body.hasAttribute('data-storage-free')) return;
    var free = parseInt(body.getAttribute('data-storage-free'), 10) || 0;
    var full = body.getAttribute('data-storage-full') === '1';
    var imageMax = 1024 * 1024;

    function fa(text) {
        return String(text).replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; }).replace('.', '٫');
    }

    function size(bytes) {
        var mb = 1024 * 1024, gb = mb * 1024;
        if (bytes >= gb) return fa((bytes / gb).toFixed(bytes >= 10 * gb ? 0 : 1).replace(/\.0$/, '')) + ' گیگابایت';
        if (bytes >= mb) return fa((bytes / mb).toFixed(bytes >= 10 * mb ? 0 : 1).replace(/\.0$/, '')) + ' مگابایت';
        return fa(Math.max(1, Math.ceil(bytes / 1024))) + ' کیلوبایت';
    }

    function guarded(input) {
        return input && input.type === 'file' && !input.hasAttribute('data-no-quota') && !input.closest('[data-no-quota]');
    }

    /** Photos are stored compressed, so the server counts at most 1 MB for each (see StorageQuota.ImageEstimate). */
    function isImageInput(input) {
        return /image|\.jpe?g|\.png|\.gif/i.test(input.getAttribute('accept') || '');
    }

    function warn(message) {
        var modal = document.getElementById('storageFullModal');
        if (!modal || !window.bootstrap) {
            if (window.confirm(message + '\n\nبرای خرید حجم بیشتر به صفحه فضای ذخیره‌سازی می‌روید؟'))
                location.href = body.getAttribute('data-storage-url');
            return;
        }
        modal.querySelector('[data-storage-message]').textContent = message;
        bootstrap.Modal.getOrCreateInstance(modal).show();
    }

    document.addEventListener('click', function (e) {
        var input = e.target.closest ? e.target.closest('input[type=file]') : null;
        if (!full || !guarded(input)) return;
        e.preventDefault();
        warn('فضای سایت شما پر شده است و دیگر نمی‌توانید تصویر یا فایل بارگذاری کنید. برای ادامه، حجم بیشتر بخرید.');
    }, true);

    document.addEventListener('change', function (e) {
        var input = e.target;
        if (!guarded(input) || !input.files || !input.files.length) return;
        var image = isImageInput(input), needed = 0;
        for (var i = 0; i < input.files.length; i++)
            needed += image ? Math.min(input.files[i].size, imageMax) : input.files[i].size;
        var pending = 0;
        document.querySelectorAll('input[type=file]').forEach(function (other) {
            if (other === input || !guarded(other) || !other.files) return;
            var otherImage = isImageInput(other);
            for (var j = 0; j < other.files.length; j++)
                pending += otherImage ? Math.min(other.files[j].size, imageMax) : other.files[j].size;
        });
        if (needed + pending <= free) return;
        input.value = '';
        warn(full || free <= pending
            ? 'فضای سایت شما پر شده است و دیگر نمی‌توانید تصویر یا فایل بارگذاری کنید. برای ادامه، حجم بیشتر بخرید.'
            : 'حجم فایل‌های انتخاب‌شده (' + size(needed) + ') از فضای خالی سایت شما (' + size(Math.max(0, free - pending)) + ') بیشتر است. حجم بیشتر بخرید یا فایل کوچک‌تری انتخاب کنید.');
    }, true);
})();
