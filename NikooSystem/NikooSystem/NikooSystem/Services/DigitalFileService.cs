using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using NikooSystem.Models.Store;

namespace NikooSystem.Services
{
    /// <summary>
    /// Files of digital products (books, lesson videos, downloads). They are kept under App_Data, which IIS never serves,
    /// and reach the buyer only through the access-checked download and stream actions.
    /// </summary>
    public static class DigitalFileService
    {
        public const string Root = "~/App_Data/Files/";
        public const long MaxBytes = 1024L * 1024 * 1024;

        private static readonly string[] AllowedExtensions =
        {
            ".pdf", ".epub", ".mobi", ".mp3", ".m4a", ".wav", ".mp4", ".webm", ".m4v", ".zip", ".rar", ".7z",
            ".docx", ".xlsx", ".pptx", ".txt", ".psd", ".ai", ".fig"
        };

        private static readonly string[] VideoExtensions = { ".mp4", ".webm", ".m4v" };
        private static readonly string[] AudioExtensions = { ".mp3", ".m4a", ".wav" };

        public static string AllowedList => string.Join(" ", AllowedExtensions.Select(e => e.TrimStart('.').ToUpperInvariant()));

        /// <summary>Value for the accept attribute of the upload input.</summary>
        public static string AcceptAttribute => string.Join(",", AllowedExtensions);

        public static StoreProductFile Save(string companyCode, HttpPostedFileBase upload, string title)
        {
            if (upload == null || upload.ContentLength == 0) return null;
            var extension = (Path.GetExtension(upload.FileName) ?? "").ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("این نوع فایل مجاز نیست. فرمت‌های مجاز: " + AllowedList);
            if (upload.ContentLength > MaxBytes)
                throw new InvalidOperationException("حجم فایل باید کمتر از ۱ گیگابایت باشد.");
            StorageQuota.EnsureRoom(companyCode, upload.ContentLength);

            var file = new StoreProductFile
            {
                FileName = Path.GetFileName(upload.FileName),
                ContentType = MimeMapping.GetMimeMapping(upload.FileName),
                Size = upload.ContentLength
            };
            file.StoredName = file.Id + extension;
            file.Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title.Trim();

            var dir = Folder(companyCode);
            Directory.CreateDirectory(dir);
            upload.SaveAs(Path.Combine(dir, file.StoredName));
            StorageQuota.Added(companyCode, 0, upload.ContentLength);
            return file;
        }

        public static void Delete(string companyCode, StoreProductFile file)
        {
            var path = PathOf(companyCode, file);
            if (path == null || !File.Exists(path)) return;
            var length = new FileInfo(path).Length;
            File.Delete(path);
            StorageQuota.Added(companyCode, 0, -length);
        }

        /// <summary>The file on disk, or null when it is missing or its stored name is not a plain file name.</summary>
        public static string PathOf(string companyCode, StoreProductFile file)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.StoredName) || file.StoredName != Path.GetFileName(file.StoredName)) return null;
            var path = Path.Combine(Folder(companyCode), file.StoredName);
            return File.Exists(path) ? path : null;
        }

        public static bool IsVideo(StoreProductFile file) => VideoExtensions.Contains(Extension(file));

        public static bool IsAudio(StoreProductFile file) => AudioExtensions.Contains(Extension(file));

        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024 * 1024)).ToString("0.#", CultureInfo.InvariantCulture) + " GB";
            if (bytes >= 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
            return Math.Max(1, bytes / 1024) + " KB";
        }

        /// <summary>
        /// Writes the file to the response. Range requests (a video player seeking) get 206 with the asked part, so long lessons
        /// play without downloading the whole file first.
        /// </summary>
        public static void Send(HttpContextBase context, string path, StoreProductFile file, bool asAttachment)
        {
            var request = context.Request;
            var response = context.Response;
            var length = new FileInfo(path).Length;
            long start = 0, end = length - 1;

            response.Clear();
            response.BufferOutput = false;
            response.ContentType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType;
            response.AddHeader("Accept-Ranges", "bytes");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            response.Cache.SetCacheability(HttpCacheability.Private);
            response.AddHeader("Content-Disposition", (asAttachment ? "attachment" : "inline") + "; filename*=UTF-8''" + Uri.EscapeDataString(file.FileName ?? file.StoredName));

            var range = request.Headers["Range"];
            if (!string.IsNullOrEmpty(range) && range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            {
                var parts = range.Substring(6).Split(',')[0].Split('-');
                long from, to;
                if (parts[0].Length == 0 && long.TryParse(parts[1], out to))
                {
                    start = Math.Max(0, length - to);
                }
                else if (long.TryParse(parts[0], out from))
                {
                    start = from;
                    if (parts.Length > 1 && long.TryParse(parts[1], out to)) end = Math.Min(to, length - 1);
                }
                if (start > end || start >= length)
                {
                    response.StatusCode = 416;
                    response.AddHeader("Content-Range", "bytes */" + length);
                    return;
                }
                response.StatusCode = 206;
                response.AddHeader("Content-Range", "bytes " + start + "-" + end + "/" + length);
            }

            response.AddHeader("Content-Length", (end - start + 1).ToString(CultureInfo.InvariantCulture));
            response.TransmitFile(path, start, end - start + 1);
        }

        private static string Folder(string companyCode) => HostingEnvironment.MapPath(Root + companyCode.ToUpperInvariant());

        private static string Extension(StoreProductFile file) => (Path.GetExtension(file?.StoredName) ?? "").ToLowerInvariant();
    }
}
