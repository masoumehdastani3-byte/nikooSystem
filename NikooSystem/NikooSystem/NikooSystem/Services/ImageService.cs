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

    /// <summary>Store images that replace each other (logo, hero, banners): file name prefix and largest size.</summary>
    public sealed class BrandImageKind
    {
        public static readonly BrandImageKind Logo = new BrandImageKind("logo", 480, 240);
        public static readonly BrandImageKind Hero = new BrandImageKind("hero", 1600, 1000);
        public static readonly BrandImageKind Banner = new BrandImageKind("banner", 1600, 800);

        private BrandImageKind(string name, int maxWidth, int maxHeight)
        {
            Name = name;
            MaxWidth = maxWidth;
            MaxHeight = maxHeight;
        }

        public string Name { get; }
        public int MaxWidth { get; }
        public int MaxHeight { get; }
    }

    public static class ImageService
    {
        /// <summary>اندازه نسخه کوچک (فهرست محصولات و بارگذاری اولیه گالری)</summary>
        public const int ThumbSize = 480;
        /// <summary>اندازه نسخه بزرگ (گالری و نمایش تمام‌صفحه)</summary>
        public const int LargeSize = 1400;
        private const int MaxUploadBytes = 10 * 1024 * 1024;
        private const int MaxBrandBytes = 5 * 1024 * 1024;
        private const int ExifOrientationId = 0x0112;

        private static readonly string[] PhotoExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
        private static readonly string[] BrandExtensions = { ".jpg", ".jpeg", ".png", ".gif" };

        /// <summary>هر تصویر در دو اندازه ذخیره می‌شود: کوچک در thumbs و بزرگ در large</summary>
        public static ImageSaveResult SaveProductImage(string companyCode, HttpPostedFileBase file)
        {
            if (!HasFile(file)) return null;
            Validate(file, PhotoExtensions, MaxUploadBytes, "فقط تصویر JPG، PNG یا GIF قابل بارگذاری است.", "حجم هر تصویر باید کمتر از ۱۰ مگابایت باشد.");
            StorageQuota.EnsureRoom(companyCode, StorageQuota.ImageEstimate(file.ContentLength));

            var root = HttpContext.Current.Server.MapPath("~/Uploads/" + companyCode);
            var thumbDir = Path.Combine(root, "thumbs");
            var largeDir = Path.Combine(root, "large");
            Directory.CreateDirectory(thumbDir);
            Directory.CreateDirectory(largeDir);

            var name = Guid.NewGuid().ToString("N") + ".jpg";
            using (var img = Decode(file))
            {
                ApplyExifOrientation(img);
                SaveResized(img, Path.Combine(largeDir, name), LargeSize, LargeSize, 82L);
                SaveResized(img, Path.Combine(thumbDir, name), ThumbSize, ThumbSize, 78L);
            }
            StorageQuota.Added(companyCode, new FileInfo(Path.Combine(largeDir, name)).Length + new FileInfo(Path.Combine(thumbDir, name)).Length);

            return new ImageSaveResult
            {
                ThumbUrl = "/Uploads/" + companyCode + "/thumbs/" + name,
                LargeUrl = "/Uploads/" + companyCode + "/large/" + name
            };
        }

        /// <summary>ذخیره لوگو یا تصویر بنر فروشگاه؛ PNG/GIF با پس‌زمینه شفاف PNG می‌ماند</summary>
        public static string SaveBrandImage(string companyCode, HttpPostedFileBase file, BrandImageKind kind)
        {
            return SaveSingle(file, "/Uploads/" + companyCode + "/brand", kind.Name + "-", kind.MaxWidth, kind.MaxHeight, companyCode);
        }

        /// <summary>تصویر محصولات سایت اصلی (پنل مدیر سایت)</summary>
        public static string SaveSiteImage(HttpPostedFileBase file)
        {
            return SaveSingle(file, "/Content/Uploads", "", LargeSize, LargeSize, null);
        }

        /// <param name="companyCode">The store whose upload space the image takes; null for Nikoo's own site.</param>
        private static string SaveSingle(HttpPostedFileBase file, string folderUrl, string prefix, int maxW, int maxH, string companyCode)
        {
            if (!HasFile(file)) return null;
            Validate(file, BrandExtensions, MaxBrandBytes, "فقط تصویر JPG یا PNG قابل بارگذاری است.", "حجم تصویر باید کمتر از ۵ مگابایت باشد.");
            StorageQuota.EnsureRoom(companyCode, StorageQuota.ImageEstimate(file.ContentLength));

            var dir = HttpContext.Current.Server.MapPath("~" + folderUrl);
            Directory.CreateDirectory(dir);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var keepAlpha = ext == ".png" || ext == ".gif";
            var name = prefix + Guid.NewGuid().ToString("N").Substring(0, 10) + (keepAlpha ? ".png" : ".jpg");
            var path = Path.Combine(dir, name);

            using (var img = Decode(file))
            {
                ApplyExifOrientation(img);
                if (keepAlpha) SaveResizedPng(img, path, maxW, maxH);
                else SaveResized(img, path, maxW, maxH, 85L);
            }
            StorageQuota.Added(companyCode, new FileInfo(path).Length);
            return folderUrl + "/" + name;
        }

        /// <summary>
        /// Saves an image in <paramref name="dir"/>, a folder outside the web root (e.g. a ticket's attachments), at most
        /// <paramref name="maxSide"/> pixels on its longer side. PNG and GIF stay PNG so screenshots keep sharp text.
        /// Returns the file name.
        /// </summary>
        public static string SavePrivate(Stream input, string fileName, string dir, int maxSide, int maxBytes)
        {
            var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
            if (!PhotoExtensions.Contains(ext))
                throw new InvalidOperationException("فقط تصویر JPG، PNG یا GIF قابل بارگذاری است.");
            if (input.CanSeek && input.Length > maxBytes)
                throw new InvalidOperationException("حجم هر تصویر باید کمتر از " + (maxBytes / (1024 * 1024)) + " مگابایت باشد.");

            Directory.CreateDirectory(dir);
            var keepAlpha = ext == ".png" || ext == ".gif";
            var name = Guid.NewGuid().ToString("N") + (keepAlpha ? ".png" : ".jpg");
            var path = Path.Combine(dir, name);
            using (var img = Decode(input))
            {
                ApplyExifOrientation(img);
                if (keepAlpha) SaveResizedPng(img, path, maxSide, maxSide);
                else SaveResized(img, path, maxSide, maxSide, 85L);
            }
            return name;
        }

        private static bool HasFile(HttpPostedFileBase file) => file != null && file.ContentLength > 0;

        private static void Validate(HttpPostedFileBase file, string[] extensions, int maxBytes, string typeError, string sizeError)
        {
            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !extensions.Contains(ext))
                throw new InvalidOperationException(typeError);
            if (file.ContentLength > maxBytes)
                throw new InvalidOperationException(sizeError);
        }

        /// <summary>Decoding (rather than trusting the extension) rejects files that are not images.</summary>
        private static Image Decode(HttpPostedFileBase file) => Decode(file.InputStream);

        private static Image Decode(Stream input)
        {
            if (input.CanSeek)
                input.Position = 0;
            try { return Image.FromStream(input); }
            catch (ArgumentException) { throw new InvalidOperationException("فایل انتخاب‌شده تصویر معتبر نیست."); }
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
                catch (StorageFullException ex)
                {
                    if (errors == null) throw;
                    errors.Add(ex.Message);
                    break;
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
