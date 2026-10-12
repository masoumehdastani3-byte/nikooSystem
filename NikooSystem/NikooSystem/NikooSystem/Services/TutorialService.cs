using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>The guided tour of one admin page (table Tutorials, with TutorialViews and TutorialSteps).</summary>
    public class TutorialDefinition
    {
        /// <summary>Admin action name, e.g. Products.</summary>
        public string Page { get; set; }
        public string Title { get; set; }
        public string Intro { get; set; }
        public string Outro { get; set; }
        /// <summary>Path under /ShopAdmin/{company}/ that the recorder opens; defaults to Page.</summary>
        public string Path { get; set; }
        /// <summary>Razor views (relative to Views/) whose changes make the recorded video stale.</summary>
        public List<string> Views { get; set; } = new List<string>();
        public List<TutorialStep> Steps { get; set; } = new List<TutorialStep>();
    }

    public class TutorialStep
    {
        /// <summary>CSS selector of the element to highlight.</summary>
        public string Target { get; set; }
        /// <summary>Name of a form field to highlight instead of <see cref="Target"/>.</summary>
        public string Field { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
    }

    /// <summary>A recorded video (file in Content/tutorials/videos) and the page fingerprint it was recorded from.</summary>
    public class TutorialVideo
    {
        public string Page { get; set; }
        public string VideoFile { get; set; }
        public string Fingerprint { get; set; }
        public DateTime RecordedAt { get; set; }
    }

    public class PageTutorial
    {
        public string Page { get; set; }
        public string Title { get; set; }
        public string TourJson { get; set; }
        public string Fingerprint { get; set; }

        /// <summary>Null when the page has no recording or the recording predates the current page.</summary>
        public string VideoUrl { get; set; }
    }

    /// <summary>What the recorder needs to decide which videos to re-record.</summary>
    public class TutorialStatus
    {
        public string Page { get; set; }
        public string Path { get; set; }
        public string Fingerprint { get; set; }
        public string VideoFingerprint { get; set; }
    }

    /// <summary>
    /// Per-page admin tutorials. Tours live in the database; tools/tutorials records them into
    /// Content/tutorials/videos/{Page}.webm and stores the page fingerprint in TutorialVideos. A video is only
    /// offered while that fingerprint still matches the tour and the page's views.
    /// </summary>
    public static class TutorialService
    {
        private const string VideosPath = "~/Content/tutorials/videos/";
        private const string ViewsPath = "~/Views/";
        private const string SeedPath = "~/App_Data/Seed/tutorials.sql";
        private static readonly Regex PageName = new Regex("^[A-Za-z0-9]{1,40}$");
        private static readonly Regex ViewName = new Regex(@"^[A-Za-z0-9_]+/[A-Za-z0-9_]+\.cshtml$");
        private static readonly Regex VideoName = new Regex(@"^[A-Za-z0-9_-]+\.webm$");
        private static readonly PageTutorial None = new PageTutorial();

        private static readonly JsonSerializerSettings TourJsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            StringEscapeHandling = StringEscapeHandling.EscapeHtml
        };

        public static PageTutorial Get(string page)
        {
            if (string.IsNullOrEmpty(page) || !PageName.IsMatch(page)) return null;

            var cacheKey = "tutorial:" + page;
            var cached = HttpRuntime.Cache[cacheKey] as PageTutorial;
            if (cached != null) return cached == None ? null : cached;

            PageTutorial tutorial;
            CacheDependency dependency = null;
            try
            {
                var definition = NikooDb.Get(NikooTables.Tutorials, new Row { ["Page"] = page });
                var video = definition == null ? null : NikooDb.Get(NikooTables.TutorialVideos, new Row { ["Page"] = page });
                var watched = new List<string>();
                tutorial = definition == null ? None : Build(definition, video, watched);
                if (watched.Count > 0)
                    dependency = new CacheDependency(watched.Select(Watchable).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            }
            catch (Exception)
            {
                tutorial = None;
            }
            // view and video files are watched; tour or video changes in the database show up within a minute
            HttpRuntime.Cache.Insert(cacheKey, tutorial, dependency, DateTime.UtcNow.AddMinutes(1), Cache.NoSlidingExpiration);
            return tutorial == None ? null : tutorial;
        }

        public static List<TutorialStatus> Statuses()
        {
            var videos = NikooDb.List(NikooTables.TutorialVideos).ToDictionary(v => v.Page, StringComparer.OrdinalIgnoreCase);
            return NikooDb.List(NikooTables.Tutorials)
                .Where(t => PageName.IsMatch(t.Page ?? ""))
                .Select(t => new TutorialStatus
                {
                    Page = t.Page,
                    Path = string.IsNullOrWhiteSpace(t.Path) ? t.Page : t.Path,
                    Fingerprint = Fingerprint(t, ViewFiles(t)),
                    VideoFingerprint = videos.TryGetValue(t.Page, out var v) ? v.Fingerprint : null
                })
                .ToList();
        }

        private static PageTutorial Build(TutorialDefinition definition, TutorialVideo video, List<string> watched)
        {
            var viewFiles = ViewFiles(definition);
            watched.AddRange(viewFiles);

            var fingerprint = Fingerprint(definition, viewFiles);
            var tutorial = new PageTutorial
            {
                Page = definition.Page,
                Title = definition.Title ?? definition.Page,
                TourJson = TourJson(definition),
                Fingerprint = fingerprint
            };

            if (video != null && VideoName.IsMatch(video.VideoFile ?? "") && video.Fingerprint == fingerprint)
            {
                var videoFile = HostingEnvironment.MapPath(VideosPath + video.VideoFile);
                watched.Add(videoFile);
                if (File.Exists(videoFile))
                    tutorial.VideoUrl = VirtualPathUtility.ToAbsolute(VideosPath + video.VideoFile) + "?v=" + fingerprint;
            }
            return tutorial;
        }

        /// <summary>The tour as read by Content/tutorial.js.</summary>
        private static string TourJson(TutorialDefinition t)
        {
            return JsonConvert.SerializeObject(new
            {
                title = t.Title,
                intro = t.Intro,
                outro = t.Outro,
                path = t.Path,
                views = t.Views,
                steps = (t.Steps ?? new List<TutorialStep>()).Select(s => new { target = s.Target, field = s.Field, title = s.Title, text = s.Text })
            }, TourJsonSettings);
        }

        private static List<string> ViewFiles(TutorialDefinition t)
        {
            return (t.Views ?? new List<string>())
                .Where(v => v != null && ViewName.IsMatch(v))
                .Select(v => HostingEnvironment.MapPath(ViewsPath + v))
                .ToList();
        }

        /// <summary>CacheDependency throws for files in missing folders, so watch the closest existing folder instead.</summary>
        private static string Watchable(string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (Directory.Exists(dir)) return path;
            while (dir != null && !Directory.Exists(dir)) dir = System.IO.Path.GetDirectoryName(dir);
            return dir ?? path;
        }

        /// <summary>
        /// SHA-256 of the tour and of its view files (BOMs and carriage returns stripped, so checkouts with different
        /// line endings agree); first 16 hex digits.
        /// </summary>
        private static string Fingerprint(TutorialDefinition definition, IEnumerable<string> viewFiles)
        {
            using (var buffer = new MemoryStream())
            {
                var tour = Encoding.UTF8.GetBytes(TourJson(definition));
                buffer.Write(tour, 0, tour.Length);
                buffer.WriteByte(0);
                foreach (var file in viewFiles) AppendNormalized(buffer, file);
                return Hash(buffer.ToArray());
            }
        }

        private static void AppendNormalized(Stream buffer, string file)
        {
            if (!File.Exists(file)) return;
            var bytes = File.ReadAllBytes(file);
            var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            for (var i = start; i < bytes.Length; i++)
                if (bytes[i] != 0x0D) buffer.WriteByte(bytes[i]);
            buffer.WriteByte(0);
        }

        private static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(data).Take(8).Select(b => b.ToString("x2")));
        }

        /// <summary>
        /// Loads App_Data/Seed/tutorials.sql (written by tools/tutorials/export-tutorials.ps1) when its content differs
        /// from the last one applied, so a deployment brings its tours and video records along.
        /// </summary>
        internal static void ApplySeed()
        {
            var path = HostingEnvironment.MapPath(SeedPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            var script = File.ReadAllText(path, Encoding.UTF8);
            var hash = Hash(Encoding.UTF8.GetBytes(script.Replace("\r", "")));
            NikooSchema.RunOnce("tutorials-seed-" + hash, () =>
            {
                using (var tx = NikooDb.Begin())
                {
                    NikooDb.Execute(script, null, tx);
                    tx.Commit();
                }
            });
        }

        /// <summary>
        /// One-time import of the tour files that used to live in Content/tutorials/*.json. A video whose old
        /// fingerprint still matched its files is kept, re-keyed to the new fingerprint.
        /// </summary>
        internal static void ImportLegacyFiles()
        {
            var toursDir = HostingEnvironment.MapPath("~/Content/tutorials");
            if (string.IsNullOrEmpty(toursDir) || !Directory.Exists(toursDir)) return;
            var manifest = ReadJson(System.IO.Path.Combine(toursDir, "videos", "manifest.json"));

            using (var tx = NikooDb.Begin())
            {
                foreach (var file in Directory.GetFiles(toursDir, "*.json"))
                {
                    var page = System.IO.Path.GetFileNameWithoutExtension(file);
                    var tour = ReadJson(file);
                    if (!PageName.IsMatch(page) || tour == null) continue;
                    if (NikooDb.Exists(NikooTables.Tutorials, new Row { ["Page"] = page }, tx)) continue;

                    var definition = new TutorialDefinition
                    {
                        Page = page,
                        Title = (string)tour["title"],
                        Intro = (string)tour["intro"],
                        Outro = (string)tour["outro"],
                        Path = (string)tour["path"],
                        Views = (tour["views"] as JArray ?? new JArray()).Select(v => (string)v).ToList(),
                        Steps = (tour["steps"] as JArray ?? new JArray()).OfType<JObject>().Select(s => new TutorialStep
                        {
                            Target = (string)s["target"],
                            Field = (string)s["field"],
                            Title = (string)s["title"],
                            Text = (string)s["text"]
                        }).ToList()
                    };
                    NikooDb.Insert(NikooTables.Tutorials, definition, tx: tx);

                    var entry = manifest?[page] as JObject;
                    var viewFiles = ViewFiles(definition);
                    if (entry == null || (string)entry["fingerprint"] != LegacyFingerprint(new[] { file }.Concat(viewFiles))) continue;
                    NikooDb.Upsert(NikooTables.TutorialVideos, new TutorialVideo
                    {
                        Page = page,
                        VideoFile = (string)entry["video"],
                        Fingerprint = Fingerprint(definition, viewFiles),
                        RecordedAt = (DateTime?)entry["recordedAt"] ?? DateTime.Now
                    }, tx: tx);
                }
                tx.Commit();
            }
        }

        /// <summary>The fingerprint the recorder used while tours were files: hash of the tour file and the views.</summary>
        private static string LegacyFingerprint(IEnumerable<string> files)
        {
            using (var buffer = new MemoryStream())
            {
                foreach (var file in files) AppendNormalized(buffer, file);
                return Hash(buffer.ToArray());
            }
        }

        private static JObject ReadJson(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JObject.Parse(File.ReadAllText(path, Encoding.UTF8)); }
            catch (JsonException) { return null; }
        }
    }
}
