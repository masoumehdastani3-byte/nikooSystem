using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;

namespace NikooSystem.Services
{
    public class ImageSaveResult
    {
        public string ThumbUrl { get; set; }
        public string LargeUrl { get; set; }
    }

    public static class ImageService
    {
        /// <summary>اندازه نسخه کوچک (فهرست محصولات و بارگذاری اولیه گالری)</summary>
        public const int ThumbSize = 480;
        /// <summary>اندازه نسخه بزرگ (گالری و نمایش تمام‌صفحه)</summary>
        public const int LargeSize = 1400;
        private const int MaxUploadBytes = 10 * 1024 * 1024;
        private const int ExifOrientationId = 0x0112;

        /// <summary>هر تصویر در دو اندازه ذخیره می‌شود: کوچک در thumbs و بزرگ در large</summary>
        public static ImageSaveResult SaveProductImage(string companyCode, HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
                return null;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
                throw new InvalidOperationException("فقط تصویر JPG، PNG یا GIF قابل بارگذاری است.");
            if (file.ContentLength > MaxUploadBytes)
                throw new InvalidOperationException("حجم هر تصویر باید کمتر از ۱۰ مگابایت باشد.");

            var root = HttpContext.Current.Server.MapPath("~/Uploads/" + companyCode);
            var thumbDir = Path.Combine(root, "thumbs");
            var largeDir = Path.Combine(root, "large");
            Directory.CreateDirectory(thumbDir);
            Directory.CreateDirectory(largeDir);

            var name = Guid.NewGuid().ToString("N") + ".jpg";
            var thumbPath = Path.Combine(thumbDir, name);
            var largePath = Path.Combine(largeDir, name);

            if (file.InputStream.CanSeek)
                file.InputStream.Position = 0;

            Image img;
            try { img = Image.FromStream(file.InputStream); }
            catch (ArgumentException) { throw new InvalidOperationException("فایل انتخاب‌شده تصویر معتبر نیست."); }

            using (img)
            {
                ApplyExifOrientation(img);
                SaveResized(img, largePath, LargeSize, LargeSize, 82L);
                SaveResized(img, thumbPath, ThumbSize, ThumbSize, 78L);
            }

            return new ImageSaveResult
            {
                ThumbUrl = "/Uploads/" + companyCode + "/thumbs/" + name,
                LargeUrl = "/Uploads/" + companyCode + "/large/" + name
            };
        }

        /// <summary>ذخیره لوگو یا تصویر بنر فروشگاه؛ PNG/GIF با پس‌زمینه شفاف PNG می‌ماند</summary>
        public static string SaveBrandImage(string companyCode, HttpPostedFileBase file, string kind, int maxW, int maxH)
        {
            if (file == null || file.ContentLength == 0)
                return null;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
                throw new InvalidOperationException("فقط تصویر JPG یا PNG قابل بارگذاری است.");
            if (file.ContentLength > 5 * 1024 * 1024)
                throw new InvalidOperationException("حجم تصویر باید کمتر از ۵ مگابایت باشد.");

            var dir = HttpContext.Current.Server.MapPath("~/Uploads/" + companyCode + "/brand");
            Directory.CreateDirectory(dir);

            var keepAlpha = ext == ".png" || ext == ".gif";
            var name = kind + "-" + Guid.NewGuid().ToString("N").Substring(0, 10) + (keepAlpha ? ".png" : ".jpg");
            var path = Path.Combine(dir, name);

            if (file.InputStream.CanSeek)
                file.InputStream.Position = 0;

            Image img;
            try { img = Image.FromStream(file.InputStream); }
            catch (ArgumentException) { throw new InvalidOperationException("فایل انتخاب‌شده تصویر معتبر نیست."); }

            using (img)
            {
                if (keepAlpha) SaveResizedPng(img, path, maxW, maxH);
                else SaveResized(img, path, maxW, maxH, 85L);
            }

            return "/Uploads/" + companyCode + "/brand/" + name;
        }

        private static void SaveResizedPng(Image source, string path, int maxW, int maxH)
        {
            var ratio = Math.Min((double)maxW / source.Width, (double)maxH / source.Height);
            if (ratio > 1) ratio = 1;
            var w = Math.Max(1, (int)(source.Width * ratio));
            var h = Math.Max(1, (int)(source.Height * ratio));

            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(source, 0, 0, w, h);
                bmp.Save(path, ImageFormat.Png);
            }
        }

        /// <summary>
        /// ذخیره چند تصویر؛ اگر errors داده شود، فایل‌های نامعتبر رد و خطایشان در آن ثبت می‌شود
        /// تا بقیه تصاویر و فرم از دست نروند.
        /// </summary>
        public static List<ImageSaveResult> SaveProductImages(string companyCode, IEnumerable<HttpPostedFileBase> files, List<string> errors = null)
        {
            var results = new List<ImageSaveResult>();
            if (files == null) return results;
            foreach (var file in files)
            {
                if (file == null || file.ContentLength == 0) continue;
                try
                {
                    var saved = SaveProductImage(companyCode, file);
                    if (saved != null) results.Add(saved);
                }
                catch (InvalidOperationException ex)
                {
                    if (errors == null) throw;
                    errors.Add(Path.GetFileName(file.FileName) + ": " + ex.Message);
                }
            }
            return results;
        }

        /// <summary>عکس‌های موبایل معمولاً چرخش را در EXIF نگه می‌دارند؛ بدون این، تصویر کج ذخیره می‌شود.</summary>
        private static void ApplyExifOrientation(Image img)
        {
            if (!img.PropertyIdList.Contains(ExifOrientationId)) return;
            var value = img.GetPropertyItem(ExifOrientationId).Value;
            if (value == null || value.Length == 0) return;

            RotateFlipType flip;
            switch (value[0])
            {
                case 2: flip = RotateFlipType.RotateNoneFlipX; break;
                case 3: flip = RotateFlipType.Rotate180FlipNone; break;
                case 4: flip = RotateFlipType.Rotate180FlipX; break;
                case 5: flip = RotateFlipType.Rotate90FlipX; break;
                case 6: flip = RotateFlipType.Rotate90FlipNone; break;
                case 7: flip = RotateFlipType.Rotate270FlipX; break;
                case 8: flip = RotateFlipType.Rotate270FlipNone; break;
                default: return;
            }
            img.RotateFlip(flip);
            img.RemovePropertyItem(ExifOrientationId);
        }

        private static void SaveResized(Image source, string path, int maxW, int maxH, long quality)
        {
            var ratio = Math.Min((double)maxW / source.Width, (double)maxH / source.Height);
            if (ratio > 1) ratio = 1;
            var w = Math.Max(1, (int)(source.Width * ratio));
            var h = Math.Max(1, (int)(source.Height * ratio));

            using (var bmp = new Bitmap(w, h))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(source, 0, 0, w, h);

                var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                var enc = new EncoderParameters(1);
                enc.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                bmp.Save(path, jpeg, enc);
            }
        }
    }
}
