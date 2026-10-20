using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NikooSystem.Models;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>The articles of the main site's blog (/blog).</summary>
    public static class SiteBlogStore
    {
        public const int PageSize = 9;
        private static readonly object SaveLock = new object();
        private static readonly TimeSpan LiveCacheTime = TimeSpan.FromMinutes(5);
        private static Tuple<DateTime, bool> _anyLive;

        public static SitePost Get(int id) => NikooDb.Get(NikooTables.SitePosts, NikooDb.Key(id));

        /// <summary>A published article by its address, or null.</summary>
        public static SitePost GetLive(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return null;
            var post = NikooDb.Get(NikooTables.SitePosts, new Row { ["Slug"] = slug.Trim() });
            return post != null && post.IsLive() ? post : null;
        }

        /// <summary>Every article, newest first. The admin list uses <see cref="Grid"/>.</summary>
        public static List<SitePost> All() => NikooDb.List(NikooTables.SitePosts, orderBy: "[PublishedAt] DESC, [Id] DESC");

        private static readonly Dictionary<string, string> AdminSorts = new Dictionary<string, string>
        {
            { "title", "t.[Title]" },
            { "published", "t.[PublishedAt]" },
            { "views", "t.[ViewCount]" },
            { "status", "t.[IsPublished], t.[PublishedAt]" }
        };

        /// <summary>The admin article list: search, status and sort happen in the database.</summary>
        public static PagedList<SitePost> Grid(GridQuery query, string state)
        {
            var filter = new SqlFilter().Search(query.Q, "Title", "Slug", "Summary", "Author", "Tags");
            var now = DateTime.Now;
            switch ((state ?? "").Trim().ToLowerInvariant())
            {
                case "published": filter.Eq("IsPublished", true).AtMost("PublishedAt", now); break;
                case "scheduled": filter.Eq("IsPublished", true).Where("t.[PublishedAt] > {0}", now); break;
                case "draft": filter.Eq("IsPublished", false); break;
            }
            return NikooDb.Page(NikooTables.SitePosts, filter, query.OrderBy(AdminSorts, "published", true), query.Page, query.PageSize);
        }

        private static SqlFilter Live() => new SqlFilter().Eq("IsPublished", true).AtMost("PublishedAt", DateTime.Now);

        /// <summary>One page of the blog, optionally only the articles with <paramref name="tag"/>.</summary>
        public static PagedList<SitePost> LivePage(int page, string tag = null)
        {
            var filter = Live();
            if (!string.IsNullOrWhiteSpace(tag)) filter.Search(tag, "Tags");
            return NikooDb.Page(NikooTables.SitePosts, filter, "t.[PublishedAt] DESC", Math.Max(1, page), PageSize);
        }

        /// <summary>The newest published articles, except <paramref name="exceptId"/>.</summary>
        public static List<SitePost> Latest(int take, int? exceptId = null)
        {
            var filter = Live();
            if (exceptId.HasValue) filter.Where("t.[Id] <> {0}", exceptId.Value);
            return NikooDb.Page(NikooTables.SitePosts, filter, "t.[PublishedAt] DESC", 1, take).ToList();
        }

        /// <summary>The newest published articles whose title, tags or summary contain one of <paramref name="keywords"/>.</summary>
        public static List<SitePost> Related(IEnumerable<string> keywords, int take)
        {
            var words = (keywords ?? Enumerable.Empty<string>()).Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            if (words.Count == 0 || !AnyLive) return new List<SitePost>();
            return All()
                .Where(p => p.IsLive() && words.Any(k => Mentions(p.Title, k) || Mentions(p.Tags, k) || Mentions(p.Summary, k)))
                .Take(take)
                .ToList();
        }

        private static bool Mentions(string text, string word) =>
            text != null && text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Whether the blog has something to show (for the menu links); checked again every few minutes.</summary>
        public static bool AnyLive
        {
            get
            {
                var cached = _anyLive;
                if (cached != null && DateTime.Now - cached.Item1 < LiveCacheTime) return cached.Item2;
                var any = NikooDb.Count(NikooTables.SitePosts, Live()) > 0;
                _anyLive = Tuple.Create(DateTime.Now, any);
                return any;
            }
        }

        /// <summary>Saves the article's own fields; a unique address is made from the title when it has none.</summary>
        public static SitePost Save(SitePost post)
        {
            lock (SaveLock)
            using (var tx = NikooDb.Begin())
            {
                var stored = post.Id > 0 ? NikooDb.Get(NikooTables.SitePosts, NikooDb.Key(post.Id), tx) : null;
                if (stored == null) post.Id = NikooDb.NextId(NikooTables.SitePosts, null, tx);
                post.Slug = UniqueSlug(Slugify(string.IsNullOrWhiteSpace(post.Slug) ? post.Title : post.Slug) ?? "post-" + post.Id, post.Id, tx);
                if (stored == null)
                {
                    post.CreatedAt = DateTime.Now;
                    NikooDb.Insert(NikooTables.SitePosts, post, tx: tx);
                }
                else
                {
                    post.CreatedAt = stored.CreatedAt;
                    post.ViewCount = stored.ViewCount;
                    post.UpdatedAt = DateTime.Now;
                    NikooDb.Update(NikooTables.SitePosts, post, tx: tx);
                }
                tx.Commit();
            }
            _anyLive = null;
            return post;
        }

        public static bool Delete(int id)
        {
            var removed = NikooDb.Delete(NikooTables.SitePosts, NikooDb.Key(id));
            _anyLive = null;
            return removed;
        }

        public static void CountView(int id) => NikooDb.Increment(NikooTables.SitePosts, NikooDb.Key(id), "ViewCount", 1);

        /// <summary>
        /// "راهنمای ساخت فروشگاه اینترنتی!" → "راهنمای-ساخت-فروشگاه-اینترنتی": Persian and Latin letters and digits joined with
        /// dashes, so the address itself carries the words people search for.
        /// </summary>
        public static string Slugify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var sb = new StringBuilder();
            foreach (var ch in text.Trim().Replace('ي', 'ی').Replace('ك', 'ک').ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (ch != '\u200c') sb.Append('-');
            }
            var slug = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
            if (slug.Length > 120) slug = slug.Substring(0, 120).TrimEnd('-');
            return slug.Length == 0 ? null : slug;
        }

        private static string UniqueSlug(string slug, int id, DbTx tx)
        {
            var candidate = slug;
            for (var n = 2; ; n++)
            {
                var taken = NikooDb.Get(NikooTables.SitePosts, new Row { ["Slug"] = candidate }, tx);
                if (taken == null || taken.Id == id) return candidate;
                candidate = slug + "-" + n;
            }
        }
    }
}
