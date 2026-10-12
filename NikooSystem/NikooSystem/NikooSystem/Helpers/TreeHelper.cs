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

            void Visit(T node, int depth, T parent)
            {
                if (!seen.Add(node.Id)) return;
                rows.Add(new TreeRow<T>(node, depth, parent));
                foreach (var child in children[node.Id]) Visit(child, depth + 1, node);
            }

            foreach (var root in list.Where(n => !n.ParentId.HasValue || !ids.Contains(n.ParentId.Value)))
                Visit(root, 0, default(T));
            return rows;
        }
    }

    public sealed class TreeRow<T> where T : ITreeNode
    {
        public TreeRow(T node, int depth, T parent)
        {
            Node = node;
            Depth = depth;
            Parent = parent;
        }

        public T Node { get; }
        public int Depth { get; }
        public T Parent { get; }
    }
}
