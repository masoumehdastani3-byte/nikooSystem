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
        public static ImageSaveResult SaveProductImage(string companyCode, HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
                return null;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
                throw new InvalidOperationException("فرمت تصویر مجاز نیست.");

            var root = HttpContext.Current.Server.MapPath("~/Uploads/" + companyCode);
            var thumbDir = Path.Combine(root, "thumbs");
            var largeDir = Path.Combine(root, "large");
            Directory.CreateDirectory(thumbDir);
            Directory.CreateDirectory(largeDir);

            var name = Guid.NewGuid().ToString("N") + ".jpg";
            var thumbPath = Path.Combine(thumbDir, name);
            var largePath = Path.Combine(largeDir, name);

            // Reset stream if needed for multiple reads
            if (file.InputStream.CanSeek)
                file.InputStream.Position = 0;

            using (var img = Image.FromStream(file.InputStream))
            {
                SaveResized(img, largePath, 1200, 1200, 85L);
                SaveResized(img, thumbPath, 400, 400, 75L);
            }

            return new ImageSaveResult
            {
                ThumbUrl = "/Uploads/" + companyCode + "/thumbs/" + name,
                LargeUrl = "/Uploads/" + companyCode + "/large/" + name
            };
        }

        public static List<ImageSaveResult> SaveProductImages(string companyCode, IEnumerable<HttpPostedFileBase> files)
        {
            var results = new List<ImageSaveResult>();
            if (files == null) return results;
            foreach (var file in files)
            {
                if (file == null || file.ContentLength == 0) continue;
                var saved = SaveProductImage(companyCode, file);
                if (saved != null) results.Add(saved);
            }
            return results;
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
