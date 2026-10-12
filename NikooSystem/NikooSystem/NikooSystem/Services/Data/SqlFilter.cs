using System.Collections.Generic;
using System.Linq;

namespace NikooSystem.Services.Data
{
    /// <summary>
    /// The WHERE clause of a paged query on a table with the alias <c>t</c>. Column names and SQL fragments come from
    /// code; every value (including search text) is passed as a parameter.
    /// </summary>
    public sealed class SqlFilter
    {
        private readonly List<string> _clauses = new List<string>();

        internal Row Parameters { get; } = new Row();

        internal string Sql => _clauses.Count == 0 ? "" : " WHERE " + string.Join(" AND ", _clauses);

        public static SqlFilter ForStore(string companyCode) => new SqlFilter().Eq("CompanyCode", companyCode);

        public SqlFilter Eq(string column, object value)
        {
            _clauses.Add(Column(column) + " = " + Param(value));
            return this;
        }

        public SqlFilter AtLeast(string column, object value)
        {
            _clauses.Add(Column(column) + " >= " + Param(value));
            return this;
        }

        public SqlFilter AtMost(string column, object value)
        {
            _clauses.Add(Column(column) + " <= " + Param(value));
            return this;
        }

        /// <summary>The column is one of <paramref name="values"/>; an empty list matches nothing.</summary>
        public SqlFilter In<TValue>(string column, IEnumerable<TValue> values)
        {
            var list = values.Distinct().ToList();
            _clauses.Add(list.Count == 0 ? "1 = 0" : Column(column) + " IN (" + string.Join(", ", list.Select(v => Param(v))) + ")");
            return this;
        }

        /// <summary>Any of the columns contains <paramref name="text"/>; blank text adds nothing.</summary>
        public SqlFilter Search(string text, params string[] columns)
        {
            if (string.IsNullOrWhiteSpace(text)) return this;
            var pattern = Param("%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%");
            _clauses.Add("(" + string.Join(" OR ", columns.Select(c => Column(c) + " LIKE " + pattern + " ESCAPE '\\'")) + ")");
            return this;
        }

        /// <summary>A condition written in code; <c>{0}</c>, <c>{1}</c>… stand for parameters holding <paramref name="values"/>.</summary>
        public SqlFilter Where(string condition, params object[] values)
        {
            _clauses.Add("(" + string.Format(condition, values.Select(v => (object)Param(v)).ToArray()) + ")");
            return this;
        }

        private static string Column(string column) => "t." + NikooDb.Name(column);

        private string Param(object value)
        {
            var name = "f" + Parameters.Count;
            Parameters[name] = value;
            return "@" + name;
        }
    }
}
