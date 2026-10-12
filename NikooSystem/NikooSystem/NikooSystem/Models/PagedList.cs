using System;
using System.Collections;
using System.Collections.Generic;
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
    }
}
