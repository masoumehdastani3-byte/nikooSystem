using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace NikooSystem.Services.Data
{
    /// <summary>
    /// Creates the database and its tables from <see cref="NikooTables"/>. Runs on every start: missing tables,
    /// columns and indexes are added, existing ones are left alone.
    /// </summary>
    internal static class NikooSchema
    {
        private static readonly Regex DatabaseName = new Regex("^[A-Za-z0-9_]{1,100}$");

        /// <summary>Tables of the first database layout, which kept each record as JSON in a [Data] column.</summary>
        private static readonly string[] DataColumnTables =
        {
            "Tenants", "StoreSettings", "Categories", "AttributeDefs", "Products", "Collections", "MenuItems", "Pages",
            "GalleryItems", "Customers", "Orders", "Reviews", "SiteProducts", "SiteOrders", "PaymentClients", "HubPayments"
        };

        public static void Apply(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var database = builder.InitialCatalog;
            if (!DatabaseName.IsMatch(database ?? ""))
                throw new InvalidOperationException("The 'Nikoo' connection string needs an Initial Catalog made of letters, digits or '_'.");

            builder.InitialCatalog = "master";
            using (var master = new SqlConnection(builder.ConnectionString))
            using (var cmd = new SqlCommand(
                "IF DB_ID(@name) IS NULL BEGIN DECLARE @sql NVARCHAR(300) = N'CREATE DATABASE ' + QUOTENAME(@name); EXEC (@sql); END", master))
            {
                cmd.Parameters.AddWithValue("@name", database);
                master.Open();
                cmd.ExecuteNonQuery();
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                Exec(connection, null,
                    "IF OBJECT_ID(N'[dbo].[SchemaInfo]', N'U') IS NULL CREATE TABLE [dbo].[SchemaInfo] ("
                    + "[Name] NVARCHAR(100) NOT NULL CONSTRAINT [PK_SchemaInfo] PRIMARY KEY, "
                    + "[AppliedAt] DATETIME2 NOT NULL CONSTRAINT [DF_SchemaInfo_AppliedAt] DEFAULT SYSDATETIME())");

                MigrateDataColumnTables(connection, connectionString);

                foreach (var table in NikooTables.All)
                    EnsureTable(connection, null, table);
            }
        }

        /// <summary>Runs a one-time step (data import, default seed) and records it in SchemaInfo.</summary>
        public static void RunOnce(string name, Action step)
        {
            using (var connection = new SqlConnection(NikooDb.ConnectionString))
            {
                connection.Open();
                using (var check = new SqlCommand("SELECT COUNT(*) FROM [dbo].[SchemaInfo] WHERE [Name] = @name", connection))
                {
                    check.Parameters.AddWithValue("@name", name);
                    if ((int)check.ExecuteScalar() > 0) return;
                }

                step();

                using (var mark = new SqlCommand("INSERT INTO [dbo].[SchemaInfo] ([Name]) VALUES (@name)", connection))
                {
                    mark.Parameters.AddWithValue("@name", name);
                    mark.ExecuteNonQuery();
                }
            }
        }

        private static void EnsureTable(SqlConnection c, SqlTransaction t, TableMap table)
        {
            var existing = ExistingColumns(c, t, table.Name);
            if (existing.Count == 0)
            {
                Exec(c, t, CreateTableSql(table));
            }
            else
            {
                foreach (var column in table.Columns.Where(col => !existing.Contains(col.Name)))
                {
                    var sql = "ALTER TABLE " + NikooDb.Table(table.Name) + " ADD " + NikooDb.Name(column.Name) + " " + column.SqlType;
                    sql += column.AllowsNull
                        ? " NULL"
                        : " NOT NULL CONSTRAINT " + NikooDb.Name("DF_" + table.Name + "_" + column.Name) + " DEFAULT " + column.SqlDefault;
                    Exec(c, t, sql);
                }
            }

            foreach (var index in table.Indexes)
            {
                Exec(c, t,
                    "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'" + index.Name + "' AND [object_id] = OBJECT_ID(N'[dbo].[" + table.Name + "]')) "
                    + "CREATE " + (index.Unique ? "UNIQUE " : "") + "INDEX " + NikooDb.Name(index.Name) + " ON " + NikooDb.Table(table.Name)
                    + " (" + string.Join(", ", index.Columns.Select(NikooDb.Name)) + ")");
            }

            foreach (var child in table.Children)
                EnsureTable(c, t, child);
        }

        private static string CreateTableSql(TableMap table)
        {
            var parts = table.Columns
                .Select(col => NikooDb.Name(col.Name) + " " + col.SqlType + (col.AllowsNull ? " NULL" : " NOT NULL"))
                .ToList();
            parts.Add("CONSTRAINT " + NikooDb.Name("PK_" + table.Name) + " PRIMARY KEY (" + string.Join(", ", table.PrimaryKey.Select(NikooDb.Name)) + ")");

            var fk = table.ForeignKey;
            if (fk != null)
            {
                parts.Add("CONSTRAINT " + NikooDb.Name("FK_" + table.Name + "_" + fk.Parent.Name)
                    + " FOREIGN KEY (" + string.Join(", ", fk.Columns.Select(NikooDb.Name)) + ")"
                    + " REFERENCES " + NikooDb.Table(fk.Parent.Name) + " (" + string.Join(", ", fk.Parent.PrimaryKey.Select(NikooDb.Name)) + ")"
                    + " ON DELETE CASCADE");
            }
            return "CREATE TABLE " + NikooDb.Table(table.Name) + " (" + string.Join(", ", parts) + ")";
        }

        /// <summary>
        /// Moves data from the first layout (whole record as JSON in [Data]) into the column-per-field tables, in one
        /// transaction: the old rows are read, the old tables dropped and recreated, and the records inserted again.
        /// </summary>
        private static void MigrateDataColumnTables(SqlConnection connection, string connectionString)
        {
            var legacy = new HashSet<string>(
                DataColumnTables.Where(name => ExistingColumns(connection, null, name).Contains("Data")),
                StringComparer.OrdinalIgnoreCase);
            if (legacy.Count == 0) return;

            var moves = new List<Action<DbTx>>();
            Read(connection, legacy, NikooTables.Tenants, moves);
            Read(connection, legacy, NikooTables.Settings, moves, (settings, code, tx) =>
            {
                if (settings.PageLayout != null) NikooDb.Insert(NikooTables.PageLayouts, settings.PageLayout, settings.CompanyCode, tx);
            });
            Read(connection, legacy, NikooTables.Categories, moves);
            Read(connection, legacy, NikooTables.AttributeDefs, moves);
            Read(connection, legacy, NikooTables.Products, moves);
            Read(connection, legacy, NikooTables.Collections, moves);
            Read(connection, legacy, NikooTables.MenuItems, moves);
            Read(connection, legacy, NikooTables.Pages, moves);
            Read(connection, legacy, NikooTables.GalleryItems, moves);
            Read(connection, legacy, NikooTables.Customers, moves);
            Read(connection, legacy, NikooTables.Orders, moves);
            Read(connection, legacy, NikooTables.Reviews, moves);
            Read(connection, legacy, NikooTables.SiteProducts, moves);
            Read(connection, legacy, NikooTables.SiteOrders, moves);
            Read(connection, legacy, NikooTables.PaymentClients, moves);
            Read(connection, legacy, NikooTables.HubPayments, moves);

            var own = new SqlConnection(connectionString);
            own.Open();
            using (var tx = new DbTx(own))
            {
                foreach (var name in legacy)
                    Exec(own, tx.Transaction, "DROP TABLE " + NikooDb.Table(name));
                foreach (var table in NikooTables.All)
                    EnsureTable(own, tx.Transaction, table);
                foreach (var move in moves)
                    move(tx);
                tx.Commit();
            }
        }

        private static void Read<T>(SqlConnection connection, ICollection<string> legacy, EntityMap<T> map, List<Action<DbTx>> moves,
            Action<T, string, DbTx> after = null) where T : class, new()
        {
            if (!legacy.Contains(map.Name)) return;
            var hasCode = ExistingColumns(connection, null, map.Name).Contains("CompanyCode");
            var companyProperty = typeof(T).GetProperty("CompanyCode");

            using (var cmd = new SqlCommand("SELECT " + (hasCode ? "[CompanyCode]" : "NULL") + ", [Data] FROM " + NikooDb.Table(map.Name), connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var code = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var entity = JsonConvert.DeserializeObject<T>(reader.GetString(1));
                    if (entity == null) continue;
                    if (code != null && companyProperty != null && map.PrimaryKey.Contains("CompanyCode")
                        && string.IsNullOrEmpty(companyProperty.GetValue(entity) as string))
                        companyProperty.SetValue(entity, code);

                    moves.Add(tx =>
                    {
                        NikooDb.Insert(map, entity, code, tx);
                        after?.Invoke(entity, code, tx);
                    });
                }
            }
        }

        private static HashSet<string> ExistingColumns(SqlConnection c, SqlTransaction t, string table)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqlCommand("SELECT [name] FROM sys.columns WHERE [object_id] = OBJECT_ID(@table)", c, t))
            {
                cmd.Parameters.AddWithValue("@table", "[dbo].[" + table + "]");
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) columns.Add(reader.GetString(0));
            }
            return columns;
        }

        private static void Exec(SqlConnection c, SqlTransaction t, string sql)
        {
            using (var cmd = new SqlCommand(sql, c, t))
                cmd.ExecuteNonQuery();
        }
    }
}
