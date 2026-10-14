using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NikooSystem.Models;

namespace NikooSystem.Services.Data
{
    /// <summary>Column name → value, used both for row keys and for row contents.</summary>
    public sealed class Row : Dictionary<string, object>
    {
        public Row() : base(StringComparer.OrdinalIgnoreCase) { }

        public Row(IDictionary<string, object> values) : base(values, StringComparer.OrdinalIgnoreCase) { }
    }

    /// <summary>An open connection with a transaction; disposing without <see cref="Commit"/> rolls back.</summary>
    public sealed class DbTx : IDisposable
    {
        internal DbTx(SqlConnection connection)
        {
            Connection = connection;
            Transaction = connection.BeginTransaction();
        }

        public SqlConnection Connection { get; }
        public SqlTransaction Transaction { get; }

        public void Commit() => Transaction.Commit();

        public void Dispose()
        {
            Transaction.Dispose();
            Connection.Dispose();
        }
    }

    /// <summary>
    /// Access to the "nikoo" SQL Server database. Each entity is one row of its table (see <see cref="NikooTables"/>)
    /// and each of its lists is a set of rows in a child table, read and written together with the entity.
    /// Table and column names come from the table maps, never from user input.
    /// </summary>
    public static class NikooDb
    {
        private const string ConnectionName = "Nikoo";
        private const char KeySeparator = '\u001f';
        private static readonly object InitLock = new object();
        private static volatile bool _initialized;
        [ThreadStatic] private static bool _initializing;

        internal static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings[ConnectionName];
                if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                    throw new ConfigurationErrorsException("Connection string '" + ConnectionName + "' is missing from Web.config.");
                return cs.ConnectionString;
            }
        }

        /// <summary>Creates or upgrades the database on first use, then runs the one-time imports and seeds.</summary>
        public static void EnsureInitialized()
        {
            if (_initialized || _initializing) return;
            lock (InitLock)
            {
                if (_initialized) return;
                _initializing = true;
                try
                {
                    NikooSchema.Apply(ConnectionString);
                    NikooSchema.RunOnce("import-app-data-json", LegacyJsonImport.Run);
                    NikooSchema.RunOnce("seed-defaults", () =>
                    {
                        TenantStore.SeedDefaults();
                        SiteStore.SeedDefaults();
                    });
                    NikooSchema.RunOnce("import-tutorial-json", LegacyJsonImport.RunTutorials);
                    NikooSchema.RunOnce("seed-site-kind-demos", TenantStore.SeedKindDemos);
                    NikooSchema.RunOnce("store-demo-images", SiteStore.ApplyStoreDemoImages);
                    NikooSchema.RunOnce("site-content-defaults", SiteContentStore.SeedDefaults);
                    NikooSchema.RunOnce("seed-booking-demo", TenantStore.SeedBookingDemo);
                    NikooSchema.RunOnce("site-booking-product", SiteStore.ApplyBookingProduct);
                    NikooSchema.RunOnce("seed-club-demo", TenantStore.SeedClubDemo);
                    NikooSchema.RunOnce("site-club-product", SiteStore.ApplyClubProduct);
                    NikooSchema.RunOnce("site-content-product", SiteStore.ApplyContentProduct);
                    NikooSchema.RunOnce("site-seo-defaults", SiteContentStore.ApplySeoDefaults);
                    NikooSchema.RunOnce("site-module-faqs", SiteContentStore.ApplyModuleFaqs);
                    NikooSchema.RunOnce("site-kinds-faq", SiteContentStore.ApplyKindsFaq);
                    NikooSchema.RunOnce("site-campus-kinds-faq", SiteContentStore.ApplyCampusKindsFaq);
                    NikooSchema.RunOnce("home-sections-demo-samples", HomeSamples.ApplyDemos);
                    NikooSchema.RunOnce("site-home-sections-faq", SiteContentStore.ApplyModuleFaqs);
                    NikooSchema.RunOnce("site-content-template-count", SiteStore.ApplyContentTemplateCount);
                    NikooSchema.RunOnce("site-news-templates", SiteStore.ApplyContentTemplateCount);
                    NikooSchema.RunOnce("site-support-faq", SiteContentStore.ApplyModuleFaqs);
                    NikooSchema.RunOnce("site-support-faq-images", SiteContentStore.ApplySupportFaqImages);
                    NikooSchema.RunOnce("site-visits-faq", SiteContentStore.ApplyModuleFaqs);
                    NikooSchema.RunOnce("site-storage-faq", SiteContentStore.ApplyModuleFaqs);
                    NikooSchema.RunOnce("subscription-periods", Subscriptions.MigratePermanentStores);
                    NikooSchema.RunOnce("site-subscription-faq", SiteContentStore.ApplySubscriptionFaqs);
                    NikooSchema.RunOnce("custom-domain-status", CustomDomains.MigrateExisting);
                    NikooSchema.RunOnce("site-domain-faq", SiteContentStore.ApplyDomainFaq);
                    NikooSchema.RunOnce("site-enamad-faq", SiteContentStore.ApplyModuleFaqs);
                    NikooSchema.RunOnce("site-campus-faq", SiteContentStore.ApplyModuleFaqs);
                    NikooSchema.RunOnce("site-accounting-faq", SiteContentStore.ApplyModuleFaqs);
                    NikooSchema.RunOnce("site-legal-texts", SiteContentStore.ApplyLegalTexts);
                    TutorialService.ApplySeed();
                    _initialized = true;
                }
                finally
                {
                    _initializing = false;
                }
            }
        }

        public static SqlConnection Open()
        {
            EnsureInitialized();
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        public static DbTx Begin() => new DbTx(Open());

        public static Row Key(string companyCode) => new Row { ["CompanyCode"] = companyCode };

        public static Row Key(string companyCode, int id) => new Row { ["CompanyCode"] = companyCode, ["Id"] = id };

        public static Row Key(int id) => new Row { ["Id"] = id };

        /// <summary>All rows of a table, optionally limited to one store, ordered by <paramref name="orderBy"/> (default: the key).</summary>
        public static List<T> List<T>(EntityMap<T> map, string companyCode = null, string orderBy = null, DbTx tx = null) where T : class, new()
        {
            return Query(map, companyCode == null ? new Row() : Key(companyCode), orderBy, tx);
        }

        /// <summary>The rows whose columns equal the values in <paramref name="filter"/>, with their lists.</summary>
        public static List<T> Query<T>(EntityMap<T> map, Row filter, string orderBy = null, DbTx tx = null) where T : class, new()
        {
            return Run(tx, (c, t) => Load(c, t, map, filter, orderBy, false));
        }

        /// <summary>
        /// One page of the rows matching <paramref name="filter"/> (the table has the alias <c>t</c>), with their lists.
        /// The primary key is appended to <paramref name="orderBy"/> so that pages never overlap.
        /// </summary>
        public static PagedList<T> Page<T>(EntityMap<T> map, SqlFilter filter, string orderBy, int page, int pageSize) where T : class, new()
        {
            pageSize = Math.Max(1, Math.Min(PagedList<T>.MaxPageSize, pageSize));
            return Run(null, (c, t) =>
            {
                int total;
                using (var cmd = Command(c, t, "SELECT COUNT(*) FROM " + Table(map.Name) + " t" + filter.Sql, filter.Parameters))
                    total = (int)cmd.ExecuteScalar();

                var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
                page = Math.Max(1, Math.Min(page, lastPage));

                var order = string.IsNullOrWhiteSpace(orderBy) ? new List<string>() : new List<string> { orderBy };
                var sortedBy = new HashSet<string>(
                    (orderBy ?? "").Split(',').Select(term => Regex.Replace(term.Trim(), @"\s+(ASC|DESC)$", "", RegexOptions.IgnoreCase)),
                    StringComparer.OrdinalIgnoreCase);
                order.AddRange(map.PrimaryKey.Select(k => "t." + Name(k)).Where(k => !sortedBy.Contains(k)));
                var parameters = new Row(filter.Parameters) { ["PageSkip"] = (page - 1) * pageSize, ["PageTake"] = pageSize };
                var sql = "SELECT t.* FROM " + Table(map.Name) + " t" + filter.Sql + " ORDER BY " + string.Join(", ", order)
                    + " OFFSET @PageSkip ROWS FETCH NEXT @PageTake ROWS ONLY";

                var entities = new List<T>();
                var keys = new List<object[]>();
                using (var cmd = Command(c, t, sql, parameters))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        entities.Add(map.Read(reader));
                        keys.Add(map.PrimaryKey.Select(k => reader[k]).ToArray());
                    }
                }
                LoadChildren(c, t, map, entities, keys);
                return new PagedList<T>(entities, page, pageSize, total);
            });
        }

        /// <summary>The values of the first column of a query written in code, with its values passed as parameters.</summary>
        internal static List<TValue> Column<TValue>(string sql, Row parameters = null)
        {
            return Run(null, (c, t) =>
            {
                var values = new List<TValue>();
                using (var cmd = Command(c, t, sql, parameters ?? new Row()))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        if (!reader.IsDBNull(0)) values.Add((TValue)Convert.ChangeType(reader.GetValue(0), typeof(TValue), CultureInfo.InvariantCulture));
                return values;
            });
        }

        /// <summary>Reads every row of a query written in code (e.g. a GROUP BY) with <paramref name="read"/>.</summary>
        internal static List<TResult> Select<TResult>(string sql, Row parameters, Func<IDataRecord, TResult> read)
        {
            return Run(null, (c, t) =>
            {
                var rows = new List<TResult>();
                using (var cmd = Command(c, t, sql, parameters ?? new Row()))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) rows.Add(read(reader));
                return rows;
            });
        }

        /// <summary>The number of rows matching <paramref name="filter"/>.</summary>
        public static int Count(TableMap map, SqlFilter filter)
        {
            return Run(null, (c, t) =>
            {
                using (var cmd = Command(c, t, "SELECT COUNT(*) FROM " + Table(map.Name) + " t" + filter.Sql, filter.Parameters))
                    return (int)cmd.ExecuteScalar();
            });
        }

        /// <summary>The first two columns of a query written in code as key → value (e.g. a GROUP BY with a count).</summary>
        internal static Dictionary<TKey, TValue> Pairs<TKey, TValue>(string sql, Row parameters = null)
        {
            return Run(null, (c, t) =>
            {
                var pairs = new Dictionary<TKey, TValue>();
                using (var cmd = Command(c, t, sql, parameters ?? new Row()))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                            pairs[(TKey)Convert.ChangeType(reader.GetValue(0), typeof(TKey), CultureInfo.InvariantCulture)] =
                                (TValue)Convert.ChangeType(reader.GetValue(1), typeof(TValue), CultureInfo.InvariantCulture);
                return pairs;
            });
        }

        /// <summary>Adds <paramref name="amount"/> to a numeric column in place, without rewriting the row.</summary>
        public static bool Increment(TableMap map, Row key, string column, int amount, DbTx tx = null)
        {
            var parameters = new Row(key) { ["Amount"] = amount };
            return Execute("UPDATE " + Table(map.Name) + " SET " + Name(column) + " = " + Name(column) + " + @Amount" + Where(key), parameters, tx) > 0;
        }

        /// <summary>The first row matching <paramref name="key"/> (any columns, not only the primary key), with its lists.</summary>
        public static T Get<T>(EntityMap<T> map, Row key, DbTx tx = null) where T : class, new()
        {
            return Run(tx, (c, t) => Load(c, t, map, key, null, true).FirstOrDefault());
        }

        public static bool Exists(TableMap map, Row key = null, DbTx tx = null)
        {
            key = key ?? new Row();
            return Run(tx, (c, t) =>
            {
                using (var cmd = Command(c, t, "SELECT CASE WHEN EXISTS (SELECT 1 FROM " + Table(map.Name) + Where(key) + ") THEN 1 ELSE 0 END", key))
                    return (int)cmd.ExecuteScalar() == 1;
            });
        }

        /// <param name="companyCode">Fills the CompanyCode key of per-store tables whose model has no CompanyCode property.</param>
        public static void Insert<T>(EntityMap<T> map, T entity, string companyCode = null, DbTx tx = null) where T : class, new()
        {
            Run(tx, (c, t) =>
            {
                InsertEntity(c, t, map, entity, companyCode);
                return 0;
            });
        }

        /// <summary>Rewrites an existing row and its lists; returns false when there is no such row.</summary>
        public static bool Update<T>(EntityMap<T> map, T entity, string companyCode = null, DbTx tx = null) where T : class, new()
        {
            return Run(tx, (c, t) => UpdateEntity(c, t, map, entity, companyCode));
        }

        public static void Upsert<T>(EntityMap<T> map, T entity, string companyCode = null, DbTx tx = null) where T : class, new()
        {
            Run(tx, (c, t) =>
            {
                if (!UpdateEntity(c, t, map, entity, companyCode)) InsertEntity(c, t, map, entity, companyCode);
                return 0;
            });
        }

        /// <summary>Deletes the matching rows; rows of child tables go with them (ON DELETE CASCADE).</summary>
        public static bool Delete(TableMap map, Row key, DbTx tx = null)
        {
            return Run(tx, (c, t) =>
            {
                using (var cmd = Command(c, t, "DELETE FROM " + Table(map.Name) + Where(key), key))
                    return cmd.ExecuteNonQuery() > 0;
            });
        }

        /// <summary>Next free Id within the store (or the whole table); locks the range until the transaction ends.</summary>
        public static int NextId(TableMap map, string companyCode, DbTx tx)
        {
            var scope = companyCode == null ? new Row() : Key(companyCode);
            using (var cmd = Command(tx.Connection, tx.Transaction,
                "SELECT ISNULL(MAX([Id]), 0) + 1 FROM " + Table(map.Name) + " WITH (UPDLOCK, HOLDLOCK)" + Where(scope), scope))
                return (int)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Makes the store's rows match <paramref name="items"/>: inserts new ones, rewrites changed ones and deletes
        /// rows that are no longer in the list. Unchanged rows are not touched.
        /// </summary>
        public static void SyncList<T>(EntityMap<T> map, string companyCode, IEnumerable<T> items, DbTx tx = null) where T : class, new()
        {
            var filter = companyCode == null ? new Row() : Key(companyCode);
            Run(tx, (c, t) =>
            {
                var existing = new Dictionary<string, KeyValuePair<Row, List<object>>>(StringComparer.OrdinalIgnoreCase);
                foreach (var entity in Load(c, t, map, filter, null, false))
                {
                    var key = map.KeyOf(map.ToRow(entity, companyCode));
                    existing[KeyText(key.Values)] = new KeyValuePair<Row, List<object>>(key, map.Snapshot(entity));
                }

                var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in items ?? Enumerable.Empty<T>())
                {
                    var keyText = KeyText(map.KeyOf(map.ToRow(item, companyCode)).Values);
                    if (!kept.Add(keyText)) continue;
                    if (!existing.TryGetValue(keyText, out var current))
                        InsertEntity(c, t, map, item, companyCode);
                    else if (!current.Value.SequenceEqual(map.Snapshot(item)))
                        UpdateEntity(c, t, map, item, companyCode);
                }

                foreach (var gone in existing.Where(e => !kept.Contains(e.Key)).Select(e => e.Value.Key).ToList())
                    using (var cmd = Command(c, t, "DELETE FROM " + Table(map.Name) + Where(gone), gone))
                        cmd.ExecuteNonQuery();
                return 0;
            });
        }

        /// <summary>Inserts the items whose key is not in the table yet; used when importing old data.</summary>
        public static void InsertMissing<T>(EntityMap<T> map, string companyCode, IEnumerable<T> items, DbTx tx) where T : class, new()
        {
            foreach (var item in items ?? Enumerable.Empty<T>())
            {
                var key = map.KeyOf(map.ToRow(item, companyCode));
                if (!Exists(map, key, tx)) Insert(map, item, companyCode, tx);
            }
        }

        public static void DeleteScope(IEnumerable<TableMap> maps, string companyCode, DbTx tx)
        {
            var key = Key(companyCode);
            foreach (var map in maps)
                using (var cmd = Command(tx.Connection, tx.Transaction, "DELETE FROM " + Table(map.Name) + Where(key), key))
                    cmd.ExecuteNonQuery();
        }

        /// <summary>Runs a statement written in code (never built from user input), with its values passed as parameters.</summary>
        internal static int Execute(string sql, Row parameters = null, DbTx tx = null)
        {
            return Run(tx, (c, t) =>
            {
                using (var cmd = Command(c, t, sql, parameters ?? new Row()))
                    return cmd.ExecuteNonQuery();
            });
        }

        public static bool IsDuplicateKey(SqlException ex) => ex.Number == 2627 || ex.Number == 2601;

        private static TResult Run<TResult>(DbTx tx, Func<SqlConnection, SqlTransaction, TResult> work)
        {
            if (tx != null) return work(tx.Connection, tx.Transaction);
            using (var connection = Open())
                return work(connection, null);
        }

        private static List<T> Load<T>(SqlConnection c, SqlTransaction t, EntityMap<T> map, Row filter, string orderBy, bool single) where T : class, new()
        {
            var entities = new List<T>();
            var keys = new List<string>();
            var firstKey = new Row();
            var sql = "SELECT " + (single ? "TOP 1 " : "") + "* FROM " + Table(map.Name) + Where(filter)
                + " ORDER BY " + (orderBy ?? string.Join(", ", map.PrimaryKey.Select(Name)));
            using (var cmd = Command(c, t, sql, filter))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (entities.Count == 0)
                        foreach (var k in map.PrimaryKey) firstKey[k] = reader[k];
                    entities.Add(map.Read(reader));
                    keys.Add(KeyText(map.PrimaryKey.Select(k => reader[k])));
                }
            }
            if (entities.Count == 0) return entities;

            foreach (var child in map.Children)
            {
                // a single row's lists are fetched by its full key, since the filter may be on another column
                var childFilter = ChildFilter(map, child, single ? firstKey : filter);
                var groups = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);
                var childSql = "SELECT * FROM " + Table(child.Name) + Where(childFilter)
                    + " ORDER BY " + string.Join(", ", child.PrimaryKey.Select(Name));
                using (var cmd = Command(c, t, childSql, childFilter))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var parent = KeyText(child.ParentColumns.Select(k => reader[k]));
                        if (!groups.TryGetValue(parent, out var list)) groups[parent] = list = new List<object>();
                        list.Add(child.ReadItem(reader));
                    }
                }
                for (var i = 0; i < entities.Count; i++)
                    child.SetItems(entities[i], groups.TryGetValue(keys[i], out var items) ? items : new List<object>());
            }
            return entities;
        }

        /// <summary>Fills the lists of the given rows, fetching only the child rows of those parents.</summary>
        private static void LoadChildren<T>(SqlConnection c, SqlTransaction t, EntityMap<T> map, List<T> entities, List<object[]> keys) where T : class, new()
        {
            if (entities.Count == 0 || map.Children.Count == 0) return;
            foreach (var child in map.Children)
            {
                // parents sharing the leading key columns (the store code) are fetched with one IN list
                var parameters = new Row();
                var groups = keys.GroupBy(k => KeyText(k.Take(k.Length - 1)));
                var clauses = new List<string>();
                foreach (var group in groups)
                {
                    var first = group.First();
                    var parts = new List<string>();
                    for (var i = 0; i < first.Length - 1; i++)
                    {
                        var p = "k" + parameters.Count;
                        parameters[p] = first[i];
                        parts.Add(Name(child.ParentColumns[i]) + " = @" + p);
                    }
                    var ids = new List<string>();
                    foreach (var key in group)
                    {
                        var p = "k" + parameters.Count;
                        parameters[p] = key[key.Length - 1];
                        ids.Add("@" + p);
                    }
                    parts.Add(Name(child.ParentColumns[first.Length - 1]) + " IN (" + string.Join(", ", ids) + ")");
                    clauses.Add("(" + string.Join(" AND ", parts) + ")");
                }

                var items = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);
                var sql = "SELECT * FROM " + Table(child.Name) + " WHERE " + string.Join(" OR ", clauses)
                    + " ORDER BY " + string.Join(", ", child.PrimaryKey.Select(Name));
                using (var cmd = Command(c, t, sql, parameters))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var parent = KeyText(child.ParentColumns.Select(k => reader[k]));
                        if (!items.TryGetValue(parent, out var list)) items[parent] = list = new List<object>();
                        list.Add(child.ReadItem(reader));
                    }
                }
                for (var i = 0; i < entities.Count; i++)
                    child.SetItems(entities[i], items.TryGetValue(KeyText(keys[i]), out var found) ? found : new List<object>());
            }
        }

        /// <summary>Translates a filter on the parent's key columns to the child's parent columns; other filters load all child rows.</summary>
        private static Row ChildFilter(TableMap parent, ChildMap child, Row parentFilter)
        {
            var filter = new Row();
            foreach (var kv in parentFilter)
            {
                var index = Array.FindIndex(parent.PrimaryKey, k => string.Equals(k, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (index < 0) return new Row();
                filter[child.ParentColumns[index]] = kv.Value;
            }
            return filter;
        }

        private static void InsertEntity<T>(SqlConnection c, SqlTransaction t, EntityMap<T> map, T entity, string companyCode) where T : class, new()
        {
            var row = map.ToRow(entity, companyCode);
            ExecInsert(c, t, map.Name, row);
            InsertChildren(c, t, map, entity, row);
        }

        private static bool UpdateEntity<T>(SqlConnection c, SqlTransaction t, EntityMap<T> map, T entity, string companyCode) where T : class, new()
        {
            var row = map.ToRow(entity, companyCode);
            var key = map.KeyOf(row);
            var values = new Row();
            foreach (var kv in row.Where(kv => !key.ContainsKey(kv.Key))) values[kv.Key] = kv.Value;

            bool found;
            if (values.Count == 0)
            {
                using (var cmd = Command(c, t, "SELECT COUNT(*) FROM " + Table(map.Name) + Where(key), key))
                    found = (int)cmd.ExecuteScalar() > 0;
            }
            else
            {
                var sql = "UPDATE " + Table(map.Name) + " SET " + string.Join(", ", values.Keys.Select(k => Name(k) + " = @" + k)) + Where(key);
                using (var cmd = Command(c, t, sql, row))
                    found = cmd.ExecuteNonQuery() > 0;
            }
            if (!found) return false;

            foreach (var child in map.Children)
            {
                var childKey = ChildFilter(map, child, key);
                using (var cmd = Command(c, t, "DELETE FROM " + Table(child.Name) + Where(childKey), childKey))
                    cmd.ExecuteNonQuery();
            }
            InsertChildren(c, t, map, entity, row);
            return true;
        }

        private static void InsertChildren<T>(SqlConnection c, SqlTransaction t, EntityMap<T> map, T entity, Row row) where T : class, new()
        {
            if (map.Children.Count == 0) return;
            var parentKey = map.PrimaryKey.Select(k => row[k]).ToArray();
            foreach (var child in map.Children)
            {
                var position = 0;
                foreach (var item in child.Items(entity))
                    ExecInsert(c, t, child.Name, child.ToRow(parentKey, position++, item));
            }
        }

        private static void ExecInsert(SqlConnection c, SqlTransaction t, string table, Row values)
        {
            var sql = "INSERT INTO " + Table(table) + " (" + string.Join(", ", values.Keys.Select(Name)) + ") VALUES ("
                + string.Join(", ", values.Keys.Select(k => "@" + k)) + ")";
            using (var cmd = Command(c, t, sql, values))
                cmd.ExecuteNonQuery();
        }

        private static string KeyText(IEnumerable<object> values)
        {
            return string.Join(KeySeparator.ToString(), values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)));
        }

        private static SqlCommand Command(SqlConnection c, SqlTransaction t, string sql, Row parameters)
        {
            var cmd = new SqlCommand(sql, c, t);
            foreach (var kv in parameters)
            {
                var p = cmd.Parameters.AddWithValue("@" + kv.Key, kv.Value ?? DBNull.Value);
                if (kv.Value is string text)
                {
                    p.SqlDbType = SqlDbType.NVarChar;
                    p.Size = text.Length > 4000 ? -1 : 4000;
                }
                else if (kv.Value is DateTime)
                {
                    p.SqlDbType = SqlDbType.DateTime2;
                }
            }
            return cmd;
        }

        private static string Where(Row key)
        {
            return key.Count == 0 ? "" : " WHERE " + string.Join(" AND ", key.Keys.Select(k => Name(k) + " = @" + k));
        }

        internal static string Table(string name) => "[dbo]." + Name(name);

        internal static string Name(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
                throw new ArgumentException("Invalid SQL identifier: " + name);
            return "[" + name + "]";
        }
    }
}
