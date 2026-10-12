using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>Posts and readers' comments of content sites, and the contact-form messages of every site.</summary>
    public static partial class TenantStore
    {
        // ---- Posts

        public static StorePost GetPost(string companyCode, int id) => Find(NikooTables.Posts, companyCode, id);

        /// <summary>A published post by its address, or null.</summary>
        public static StorePost GetLivePostBySlug(string companyCode, string slug)
        {
            var post = FindBy(NikooTables.Posts, companyCode, "Slug", slug);
            return post != null && post.IsLive() ? post : null;
        }

        /// <summary>Saves the post's own fields; its view counter and creation time are kept as stored.</summary>
        public static StorePost SavePost(string companyCode, StorePost post) =>
            Save(NikooTables.Posts, companyCode, post, (p, stored) =>
            {
                if (string.IsNullOrWhiteSpace(p.Slug)) p.Slug = "post-" + p.Id;
                if (stored == null)
                {
                    p.CreatedAt = DateTime.Now;
                    return;
                }
                p.CreatedAt = stored.CreatedAt;
                p.ViewCount = stored.ViewCount;
                p.UpdatedAt = DateTime.Now;
            });

        /// <summary>Deletes the post together with its comments.</summary>
        public static bool DeletePost(string companyCode, int id)
        {
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var key = NikooDb.Key(companyCode);
                key["PostId"] = id;
                NikooDb.Delete(NikooTables.PostComments, key, tx);
                var removed = NikooDb.Delete(NikooTables.Posts, NikooDb.Key(companyCode, id), tx);
                tx.Commit();
                return removed;
            }
        }

        public static void CountPostView(string companyCode, int postId)
        {
            NikooDb.Increment(NikooTables.Posts, NikooDb.Key(companyCode, postId), "ViewCount", 1);
        }

        private static SqlFilter LivePosts(string companyCode) =>
            SqlFilter.ForStore(companyCode).Eq("IsPublished", true).AtMost("PublishedAt", DateTime.Now);

        /// <summary>One page of the storefront post archive.</summary>
        public static PagedList<StorePost> PostsPage(string companyCode, PostQuery query)
        {
            var filter = LivePosts(companyCode).Search(query.Q, "Title", "Summary", "Kicker");
            if (query.CategoryId.HasValue) filter.In("CategoryId", CategoryTree(companyCode, query.CategoryId.Value));
            if (!string.IsNullOrWhiteSpace(query.Tag)) filter.Search(query.Tag, "Tags");
            return Page(NikooTables.Posts, companyCode, filter, "t.[PublishedAt] DESC", query.Page, PostQuery.PageSize);
        }

        /// <summary>
        /// The newest published posts; <paramref name="source"/> "popular" orders by views, "featured" and "breaking" keep only
        /// headline and breaking-news posts.
        /// </summary>
        public static List<StorePost> TopPosts(string companyCode, int take, string source = "newest", int? categoryId = null, int? exceptId = null)
        {
            var filter = LivePosts(companyCode);
            if (categoryId.HasValue) filter.In("CategoryId", CategoryTree(companyCode, categoryId.Value));
            if (exceptId.HasValue) filter.Where("t.[Id] <> {0}", exceptId.Value);
            if (source == "featured") filter.Eq("IsFeatured", true);
            if (source == "breaking") filter.Eq("IsBreaking", true);
            var orderBy = source == "popular" ? "t.[ViewCount] DESC" : "t.[PublishedAt] DESC";
            return Page(NikooTables.Posts, companyCode, filter, orderBy, 1, take).ToList();
        }

        private static readonly Dictionary<string, string> PostSorts = new Dictionary<string, string>
        {
            { "title", "t.[Title]" },
            { "views", "t.[ViewCount]" },
            { "published", "t.[PublishedAt]" }
        };

        public static PagedList<StorePost> PostGrid(string companyCode, PostGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Title", "Kicker", "Author", "Tags");
            if (query.CategoryId.HasValue) filter.In("CategoryId", CategoryTree(companyCode, query.CategoryId.Value));
            switch (query.State)
            {
                case "published": filter.Eq("IsPublished", true).AtMost("PublishedAt", DateTime.Now); break;
                case "scheduled": filter.Eq("IsPublished", true).Where("t.[PublishedAt] > {0}", DateTime.Now); break;
                case "draft": filter.Eq("IsPublished", false); break;
                case "featured": filter.Eq("IsFeatured", true); break;
                case "breaking": filter.Eq("IsBreaking", true); break;
            }
            return Grid(NikooTables.Posts, companyCode, query, filter, PostSorts, "published", true);
        }

        public static int CountPosts(string companyCode, bool? sample = null)
        {
            var filter = SqlFilter.ForStore(companyCode);
            if (sample.HasValue) filter.Eq("IsSample", sample.Value);
            return NikooDb.Count(NikooTables.Posts, filter);
        }

        public static int DeleteSamplePosts(string companyCode)
        {
            var key = NikooDb.Key(companyCode);
            var ids = NikooDb.Column<int>("SELECT [Id] FROM " + Table(NikooTables.Posts) + " WHERE [CompanyCode] = @CompanyCode AND [IsSample] = 1", key);
            return ids.Count(id => DeletePost(companyCode, id));
        }

        public static int TotalPostViews(string companyCode) =>
            NikooDb.Column<int>("SELECT ISNULL(SUM([ViewCount]), 0) FROM " + Table(NikooTables.Posts) + " WHERE [CompanyCode] = @CompanyCode",
                NikooDb.Key(companyCode)).FirstOrDefault();

        /// <summary>Post id → title, for the posts of one page of comments.</summary>
        public static Dictionary<int, StorePost> PostsByIds(string companyCode, IEnumerable<int> ids)
        {
            var list = ids.Distinct().ToList();
            if (!list.Any()) return new Dictionary<int, StorePost>();
            return Page(NikooTables.Posts, companyCode, SqlFilter.ForStore(companyCode).In("Id", list), null, 1, PagedList<StorePost>.MaxPageSize)
                .ToDictionary(p => p.Id);
        }

        // ---- Comments

        public static StorePostComment GetComment(string companyCode, int id) => Find(NikooTables.PostComments, companyCode, id);

        public static List<StorePostComment> ApprovedComments(string companyCode, int postId)
        {
            var filter = NikooDb.Key(companyCode);
            filter["PostId"] = postId;
            filter["IsApproved"] = true;
            return NikooDb.Query(NikooTables.PostComments, filter, "[CreatedAt], [Id]");
        }

        public static StorePostComment AddComment(string companyCode, StorePostComment comment)
        {
            comment.Id = 0;
            return Save(NikooTables.PostComments, companyCode, comment, (c, stored) => c.CreatedAt = DateTime.Now);
        }

        public static bool UpdateComment(string companyCode, StorePostComment comment) => Update(NikooTables.PostComments, companyCode, comment);

        public static bool DeleteComment(string companyCode, int id) => Remove(NikooTables.PostComments, companyCode, id);

        private static readonly Dictionary<string, string> CommentSorts = new Dictionary<string, string>
        {
            { "author", "t.[AuthorName]" },
            { "date", "t.[CreatedAt]" }
        };

        public static PagedList<StorePostComment> CommentGrid(string companyCode, CommentGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "AuthorName", "AuthorEmail", "Body");
            if (query.State == "pending") filter.Eq("IsApproved", false);
            else if (query.State == "approved") filter.Eq("IsApproved", true);
            return Grid(NikooTables.PostComments, companyCode, query, filter, CommentSorts, "date", true);
        }

        public static int CountPendingComments(string companyCode) =>
            NikooDb.Count(NikooTables.PostComments, SqlFilter.ForStore(companyCode).Eq("IsApproved", false));

        // ---- Contact messages

        public static StoreContactMessage GetMessage(string companyCode, int id) => Find(NikooTables.ContactMessages, companyCode, id);

        public static StoreContactMessage AddMessage(string companyCode, StoreContactMessage message)
        {
            message.Id = 0;
            message.IsRead = false;
            return Save(NikooTables.ContactMessages, companyCode, message, (m, stored) => m.CreatedAt = DateTime.Now);
        }

        public static bool UpdateMessage(string companyCode, StoreContactMessage message) => Update(NikooTables.ContactMessages, companyCode, message);

        public static bool DeleteMessage(string companyCode, int id) => Remove(NikooTables.ContactMessages, companyCode, id);

        private static readonly Dictionary<string, string> MessageSorts = new Dictionary<string, string>
        {
            { "name", "t.[FullName]" },
            { "subject", "t.[Subject]" },
            { "date", "t.[CreatedAt]" }
        };

        public static PagedList<StoreContactMessage> MessageGrid(string companyCode, MessageGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "FullName", "Email", "Mobile", "Subject", "Message");
            if (query.State == "unread") filter.Eq("IsRead", false);
            else if (query.State == "read") filter.Eq("IsRead", true);
            return Grid(NikooTables.ContactMessages, companyCode, query, filter, MessageSorts, "date", true);
        }

        public static int CountUnreadMessages(string companyCode) =>
            NikooDb.Count(NikooTables.ContactMessages, SqlFilter.ForStore(companyCode).Eq("IsRead", false));
    }
}
