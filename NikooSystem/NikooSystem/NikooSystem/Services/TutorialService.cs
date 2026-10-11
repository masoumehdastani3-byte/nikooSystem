using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using Newtonsoft.Json.Linq;

namespace NikooSystem.Services
{
    public class PageTutorial
    {
        public string Page { get; set; }
        public string Title { get; set; }
        public string TourJson { get; set; }
        public string Fingerprint { get; set; }

        /// <summary>Null when the page has no recording or the recording predates the current page.</summary>
        public string VideoUrl { get; set; }
    }

    /// <summary>
    /// Per-page admin tutorials. Each page has a tour script at Content/tutorials/{Page}.json; the recorder in
    /// tools/tutorials turns it into Content/tutorials/videos/{Page}.webm and stores the page fingerprint in
    /// videos/manifest.json. A video is only offered while that fingerprint still matches the page.
    /// </summary>
    public static class TutorialService
    {
        private const string ToursPath = "~/Content/tutorials/";
        private const string VideosPath = "~/Content/tutorials/videos/";
        private const string ViewsPath = "~/Views/";
        private static readonly Regex PageName = new Regex("^[A-Za-z0-9]{1,40}$");
        private static readonly Regex ViewName = new Regex(@"^[A-Za-z0-9_]+/[A-Za-z0-9_]+\.cshtml$");
        private static readonly Regex VideoName = new Regex(@"^[A-Za-z0-9_-]+\.webm$");
        private static readonly PageTutorial None = new PageTutorial();

        public static PageTutorial Get(string page)
        {
            if (string.IsNullOrEmpty(page) || !PageName.IsMatch(page)) return null;

            var cacheKey = "tutorial:" + page;
            var cached = HttpRuntime.Cache[cacheKey] as PageTutorial;
            if (cached != null) return cached == None ? null : cached;

            var watched = new List<string>();
            PageTutorial tutorial;
            try
            {
                tutorial = Load(page, watched) ?? None;
                var paths = watched.Select(Watchable).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                HttpRuntime.Cache.Insert(cacheKey, tutorial, new CacheDependency(paths));
            }
            catch (Exception)
            {
                tutorial = None;
                HttpRuntime.Cache.Insert(cacheKey, tutorial, null, DateTime.UtcNow.AddMinutes(1), Cache.NoSlidingExpiration);
            }
            return tutorial == None ? null : tutorial;
        }

        /// <summary>CacheDependency throws for files in missing folders, so watch the closest existing folder instead.</summary>
        private static string Watchable(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (Directory.Exists(dir)) return path;
            while (dir != null && !Directory.Exists(dir)) dir = Path.GetDirectoryName(dir);
            return dir ?? path;
        }

        private static PageTutorial Load(string page, List<string> watched)
        {
            var server = HttpContext.Current.Server;
            var tourFile = server.MapPath(ToursPath + page + ".json");
            var manifestFile = server.MapPath(VideosPath + "manifest.json");
            watched.Add(tourFile);
            watched.Add(manifestFile);
            if (!File.Exists(tourFile)) return null;

            JObject tour;
            try { tour = JObject.Parse(File.ReadAllText(tourFile, Encoding.UTF8)); }
            catch { return null; }

            var viewFiles = (tour["views"] as JArray ?? new JArray())
                .Select(v => (string)v)
                .Where(v => v != null && ViewName.IsMatch(v))
                .Select(v => server.MapPath(ViewsPath + v))
                .ToList();
            watched.AddRange(viewFiles);

            var fingerprint = Fingerprint(new[] { tourFile }.Concat(viewFiles));
            var tutorial = new PageTutorial
            {
                Page = page,
                Title = (string)tour["title"] ?? page,
                TourJson = tour.ToString(Newtonsoft.Json.Formatting.None).Replace("</", "<\\/"),
                Fingerprint = fingerprint
            };

            var entry = ReadManifest(manifestFile)?[page] as JObject;
            var video = (string)entry?["video"];
            if (video != null && VideoName.IsMatch(video) && (string)entry["fingerprint"] == fingerprint)
            {
                var videoFile = server.MapPath(VideosPath + video);
                watched.Add(videoFile);
                if (File.Exists(videoFile))
                    tutorial.VideoUrl = VirtualPathUtility.ToAbsolute(VideosPath + video) + "?v=" + fingerprint;
            }
            return tutorial;
        }

        private static JObject ReadManifest(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JObject.Parse(File.ReadAllText(path, Encoding.UTF8)); }
            catch { return null; }
        }

        /// <summary>
        /// SHA-256 of the files with BOMs and carriage returns stripped, so checkouts with different line endings
        /// agree. tools/tutorials/record.mjs computes the same value; keep the two in sync.
        /// </summary>
        public static string Fingerprint(IEnumerable<string> files)
        {
            using (var sha = SHA256.Create())
            using (var buffer = new MemoryStream())
            {
                foreach (var file in files)
                {
                    if (!File.Exists(file)) continue;
                    var bytes = File.ReadAllBytes(file);
                    var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
                    for (var i = start; i < bytes.Length; i++)
                        if (bytes[i] != 0x0D) buffer.WriteByte(bytes[i]);
                    buffer.WriteByte(0);
                }
                var hash = sha.ComputeHash(buffer.ToArray());
                return string.Concat(hash.Take(8).Select(b => b.ToString("x2")));
            }
        }
    }
}
