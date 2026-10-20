using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NikooSystem.Models
{
    /// <summary>What the pager needs to know about a page, whatever the item type.</summary>
    public interface IPagedList
    {
        int Page { get; }
        int PageSize { get; }
        int TotalCount { get; }
        int TotalPages { get; }
        bool HasNext { get; }
    }

    /// <summary>One page of a longer list, cut on the server.</summary>
    public sealed class PagedList<T> : IReadOnlyList<T>, IPagedList
    {
        public const int MaxPageSize = 100;

        private readonly IReadOnlyList<T> _items;

        public PagedList(IEnumerable<T> items, int page, int pageSize, int totalCount)
        {
            _items = items.ToList();
            Page = page;
            PageSize = pageSize;
            TotalCount = totalCount;
        }

        /// <summary>Cuts a page out of a list that is already in memory (tree lists, which are ordered by their structure).</summary>
        public static PagedList<T> From(IEnumerable<T> all, int page, int pageSize)
        {
            var list = all.ToList();
            pageSize = Math.Max(1, Math.Min(MaxPageSize, pageSize));
            var lastPage = Math.Max(1, (int)Math.Ceiling(list.Count / (double)pageSize));
            page = Math.Max(1, Math.Min(page, lastPage));
            return new PagedList<T>(list.Skip((page - 1) * pageSize).Take(pageSize), page, pageSize, list.Count);
        }

        public int Page { get; }
        public int PageSize { get; }
        public int TotalCount { get; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public bool HasNext => Page < TotalPages;

        public int Count => _items.Count;
        public T this[int index] => _items[index];
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Page, page size, search text and sort order of an admin table, read from the query string.</summary>
    public class GridQuery
    {
        public static readonly int[] PageSizes = { 10, 20, 50, 100 };

        public int Page { get; set; } = 1;
        public int Size { get; set; } = 20;
        public string Q { get; set; }
        public string Sort { get; set; }
        public string Dir { get; set; }

        public int PageSize => PageSizes.Contains(Size) ? Size : 20;

        public bool Descending => string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The ORDER BY expression for the requested sort key, chosen from <paramref name="sorts"/> (key → SQL written in code);
        /// an unknown key falls back to <paramref name="defaultSort"/>.
        /// </summary>
        public string OrderBy(IDictionary<string, string> sorts, string defaultSort, bool defaultDescending = false)
        {
            string column;
            bool desc;
            if (!string.IsNullOrEmpty(Sort) && sorts.TryGetValue(Sort, out column))
            {
                desc = Descending;
            }
            else
            {
                column = sorts[defaultSort];
                desc = defaultDescending;
            }
            return string.Join(", ", column.Split(',').Select(c => c.Trim() + (desc ? " DESC" : " ASC")));
        }

        /// <summary>
        /// Search, sort and page a list already in memory. An unknown sort key, or no <paramref name="defaultSort"/>,
        /// keeps the order of <paramref name="source"/> (tree lists stay in their structure until a column is chosen).
        /// </summary>
        public PagedList<T> Slice<T>(IEnumerable<T> source, Func<T, string> search, IDictionary<string, Func<T, object>> sorts,
            string defaultSort = null, bool defaultDescending = false)
        {
            IEnumerable<T> rows = source ?? Enumerable.Empty<T>();
            var text = (Q ?? "").Trim();
            if (text.Length > 0 && search != null)
                rows = rows.Where(item => (search(item) ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);

            Func<T, object> key = null;
            var desc = defaultDescending;
            if (sorts != null && !string.IsNullOrEmpty(Sort) && sorts.TryGetValue(Sort, out key))
                desc = Descending;
            else if (sorts != null && !string.IsNullOrEmpty(defaultSort))
                sorts.TryGetValue(defaultSort, out key);

            if (key != null)
                rows = desc
                    ? rows.OrderByDescending(item => key(item), GridValueComparer.Instance)
                    : rows.OrderBy(item => key(item), GridValueComparer.Instance);
            return PagedList<T>.From(rows, Page, PageSize);
        }

        private sealed class GridValueComparer : IComparer<object>
        {
            public static readonly GridValueComparer Instance = new GridValueComparer();

            public int Compare(object x, object y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                if (x is string || y is string)
                    return string.Compare(Convert.ToString(x, CultureInfo.CurrentCulture), Convert.ToString(y, CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
                var comparable = x as IComparable;
                if (comparable != null && x.GetType() == y.GetType()) return comparable.CompareTo(y);
                return string.Compare(Convert.ToString(x, CultureInfo.InvariantCulture), Convert.ToString(y, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
