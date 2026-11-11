using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models.Store;

namespace NikooSystem.Helpers
{
    /// <summary>Walks the parent/child tree of categories and menu items.</summary>
    public static class TreeHelper
    {
        /// <summary>The node and all of its descendants, at any depth.</summary>
        public static HashSet<int> SubtreeIds<T>(IEnumerable<T> all, int rootId) where T : ITreeNode
        {
            var children = all.Where(n => n.ParentId.HasValue).ToLookup(n => n.ParentId.Value, n => n.Id);
            var ids = new HashSet<int> { rootId };
            var pending = new Stack<int>(ids);
            while (pending.Count > 0)
                foreach (var child in children[pending.Pop()])
                    if (ids.Add(child)) pending.Push(child);
            return ids;
        }

        public static IEnumerable<T> Roots<T>(IEnumerable<T> all) where T : ITreeNode
        {
            return (all ?? Enumerable.Empty<T>()).Where(n => !n.ParentId.HasValue);
        }

        /// <summary>Title as shown in flat lists and dropdowns: children are marked with a dash.</summary>
        public static string TreeTitle(this ITreeNode node)
        {
            return (node.ParentId.HasValue ? "— " : "") + node.Title;
        }

        /// <summary>
        /// The tree depth-first, each node right after its parent and siblings in their given order; nodes whose parent is
        /// missing are treated as roots.
        /// </summary>
        public static List<TreeRow<T>> Flatten<T>(IEnumerable<T> all) where T : ITreeNode
        {
            var list = all.ToList();
            var ids = new HashSet<int>(list.Select(n => n.Id));
            var children = list.Where(n => n.ParentId.HasValue && ids.Contains(n.ParentId.Value)).ToLookup(n => n.ParentId.Value);
            var rows = new List<TreeRow<T>>();
            var seen = new HashSet<int>();

            void Visit(T node, int depth, T parent, bool isLast)
            {
                if (!seen.Add(node.Id)) return;
                var kids = children[node.Id].ToList();
                rows.Add(new TreeRow<T>(node, depth, parent, kids.Count > 0, isLast));
                for (var i = 0; i < kids.Count; i++)
                    Visit(kids[i], depth + 1, node, i == kids.Count - 1);
            }

            var roots = list.Where(n => !n.ParentId.HasValue || !ids.Contains(n.ParentId.Value)).ToList();
            for (var i = 0; i < roots.Count; i++)
                Visit(roots[i], 0, default(T), i == roots.Count - 1);
            return rows;
        }
    }

    public sealed class TreeRow<T> where T : ITreeNode
    {
        public TreeRow(T node, int depth, T parent, bool hasChildren = false, bool isLast = true)
        {
            Node = node;
            Depth = depth;
            Parent = parent;
            HasChildren = hasChildren;
            IsLast = isLast;
        }

        public T Node { get; }
        public int Depth { get; }
        public T Parent { get; }
        public bool HasChildren { get; }
        /// <summary>Last among its siblings; used to stop the vertical guide line.</summary>
        public bool IsLast { get; }
    }
}
