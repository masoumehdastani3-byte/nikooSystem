using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace NikooSystem.Services.Data
{
    /// <summary>One column of a mapped table.</summary>
    public sealed class ColumnMap
    {
        internal ColumnMap(string name, Type type, PropertyInfo property)
        {
            Name = name;
            var underlying = Nullable.GetUnderlyingType(type);
            ClrType = underlying ?? type;
            AllowsNull = underlying != null || !type.IsValueType;
            Property = property;
        }

        public string Name { get; }
        internal Type ClrType { get; }
        internal bool AllowsNull { get; set; }

        /// <summary>Maximum length of a text column; null means NVARCHAR(MAX).</summary>
        internal int? Length { get; set; }

        /// <summary>The model property behind the column; null for key columns filled from the parent row or the store code.</summary>
        internal PropertyInfo Property { get; }

        internal string SqlType
        {
            get
            {
                if (ClrType == typeof(string)) return Length.HasValue ? "NVARCHAR(" + Length.Value + ")" : "NVARCHAR(MAX)";
                if (ClrType.IsEnum) return "NVARCHAR(30)";
                if (ClrType == typeof(int)) return "INT";
                if (ClrType == typeof(long)) return "BIGINT";
                if (ClrType == typeof(bool)) return "BIT";
                if (ClrType == typeof(DateTime)) return "DATETIME2";
                if (ClrType == typeof(decimal)) return "DECIMAL(18, 2)";
                if (ClrType == typeof(double)) return "FLOAT";
                throw new NotSupportedException("No SQL type for " + ClrType.Name);
            }
        }

        /// <summary>Value given to existing rows when a NOT NULL column is added to a table that already has data.</summary>
        internal string SqlDefault
        {
            get
            {
                if (ClrType == typeof(string) || ClrType.IsEnum) return "N''";
                if (ClrType == typeof(DateTime)) return "SYSDATETIME()";
                return "0";
            }
        }

        internal static bool IsScalar(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            return t == typeof(string) || t.IsEnum || t == typeof(int) || t == typeof(long) || t == typeof(bool)
                || t == typeof(DateTime) || t == typeof(decimal) || t == typeof(double);
        }

        internal object ToDb(object value)
        {
            if (value == null) return null;
            if (ClrType.IsEnum) return value.ToString();
            var text = value as string;
            if (text != null && Length.HasValue && text.Length > Length.Value) return text.Substring(0, Length.Value);
            return value;
        }

        internal object FromDb(object value)
        {
            if (value == null || value is DBNull) return null;
            if (ClrType.IsEnum)
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                try { return Enum.Parse(ClrType, text, true); }
                catch (ArgumentException) { return Activator.CreateInstance(ClrType); }
            }
            return value.GetType() == ClrType ? value : Convert.ChangeType(value, ClrType, CultureInfo.InvariantCulture);
        }
    }

    internal sealed class IndexMap
    {
        public string Name;
        public string[] Columns;
        public bool Unique;
    }

    internal sealed class ForeignKeyMap
    {
        public string[] Columns;
        public TableMap Parent;
    }

    /// <summary>A table: its columns, primary key, indexes and the child tables that hold the entity's lists.</summary>
    public abstract class TableMap
    {
        private readonly List<ColumnMap> _columns = new List<ColumnMap>();

        internal TableMap(string name)
        {
            Name = name;
        }

        public string Name { get; }
        internal IReadOnlyList<ColumnMap> Columns => _columns;
        internal string[] PrimaryKey { get; set; }
        internal List<IndexMap> Indexes { get; } = new List<IndexMap>();
        internal ForeignKeyMap ForeignKey { get; set; }
        internal List<ChildMap> Children { get; } = new List<ChildMap>();

        internal ColumnMap Find(string name)
        {
            return _columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        internal void AddColumn(ColumnMap column)
        {
            if (Find(column.Name) != null) throw new InvalidOperationException(Name + " already has a column " + column.Name);
            _columns.Add(column);
        }

        /// <summary>Checks the mapping and builds the child tables; called once before the schema is created.</summary>
        internal virtual void Validate()
        {
            foreach (var key in PrimaryKey)
            {
                var column = Find(key) ?? throw new InvalidOperationException(Name + ": key column " + key + " is not mapped");
                column.AllowsNull = false;
                if (column.ClrType == typeof(string) && !column.Length.HasValue) column.Length = 100;
            }
            foreach (var child in Children) child.Validate();
        }

        internal static object CreateFrom(Type type, IDataRecord record, IEnumerable<ColumnMap> columns)
        {
            var entity = Activator.CreateInstance(type);
            foreach (var column in columns.Where(c => c.Property != null))
            {
                var value = column.FromDb(record[column.Name]);
                if (value != null || column.AllowsNull) column.Property.SetValue(entity, value);
            }
            return entity;
        }

        internal static IEnumerable<PropertyInfo> MappableProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);
        }
    }

    /// <summary>
    /// Maps a model class to a table: every scalar property becomes a column with the same name, and list properties
    /// are stored in child tables (see <see cref="Child"/> and <see cref="Values"/>). Key columns that are not
    /// properties of the model (the store's CompanyCode) are filled from the store code passed to each operation.
    /// </summary>
    public sealed class EntityMap<T> : TableMap where T : class, new()
    {
        private readonly HashSet<string> _unmapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public EntityMap(string table, params string[] key) : base(table)
        {
            PrimaryKey = key;
            foreach (var name in key.Where(k => MappableProperties(typeof(T)).All(p => p.Name != k)))
                AddColumn(new ColumnMap(name, typeof(string), null) { AllowsNull = false, Length = 40 });

            foreach (var property in MappableProperties(typeof(T)))
            {
                if (ColumnMap.IsScalar(property.PropertyType)) AddColumn(new ColumnMap(property.Name, property.PropertyType, property));
                else _unmapped.Add(property.Name);
            }
        }

        public EntityMap<T> Text(string column, int length)
        {
            var c = Find(column) ?? throw new ArgumentException(Name + " has no column " + column);
            c.Length = length;
            return this;
        }

        public EntityMap<T> Ignore(string property)
        {
            _unmapped.Remove(property);
            return this;
        }

        public EntityMap<T> Index(string name, params string[] columns)
        {
            Indexes.Add(new IndexMap { Name = name, Columns = columns });
            return this;
        }

        public EntityMap<T> UniqueIndex(string name, params string[] columns)
        {
            Indexes.Add(new IndexMap { Name = name, Columns = columns, Unique = true });
            return this;
        }

        /// <summary>Rows are deleted together with the row of <paramref name="parent"/> that has the same key.</summary>
        public EntityMap<T> References(TableMap parent)
        {
            ForeignKey = new ForeignKeyMap { Columns = PrimaryKey, Parent = parent };
            return this;
        }

        /// <summary>Stores the objects of a list property in <paramref name="table"/>, one row per item.</summary>
        public EntityMap<T> Child(string property, string table, string parentColumn)
        {
            return AddChild(property, table, parentColumn, null, null);
        }

        /// <summary>Stores a list of numbers or strings in <paramref name="table"/>, one row per value.</summary>
        public EntityMap<T> Values(string property, string table, string parentColumn, string valueColumn, int? length = null)
        {
            return AddChild(property, table, parentColumn, valueColumn, length);
        }

        private EntityMap<T> AddChild(string property, string table, string parentColumn, string valueColumn, int? length)
        {
            var info = typeof(T).GetProperty(property) ?? throw new ArgumentException(typeof(T).Name + " has no property " + property);
            _unmapped.Remove(property);
            Children.Add(new ChildMap(this, table, info, parentColumn, valueColumn, length));
            return this;
        }

        internal override void Validate()
        {
            if (_unmapped.Count > 0)
                throw new InvalidOperationException(typeof(T).Name + " has properties with no column or child table: " + string.Join(", ", _unmapped));
            base.Validate();
        }

        internal T Read(IDataRecord record) => (T)CreateFrom(typeof(T), record, Columns);

        /// <summary>Column values for the row, with key columns that are not model properties set to <paramref name="companyCode"/>.</summary>
        internal Row ToRow(T entity, string companyCode)
        {
            var row = new Row();
            foreach (var column in Columns)
            {
                if (column.Property != null)
                {
                    row[column.Name] = column.ToDb(column.Property.GetValue(entity));
                }
                else
                {
                    if (string.IsNullOrEmpty(companyCode)) throw new ArgumentException(Name + " rows need a company code");
                    row[column.Name] = companyCode;
                }
            }
            return row;
        }

        internal Row KeyOf(Row row)
        {
            var key = new Row();
            foreach (var k in PrimaryKey) key[k] = row[k];
            return key;
        }

        /// <summary>Everything that is stored for the entity, in a fixed order; equal snapshots mean nothing to save.</summary>
        internal List<object> Snapshot(T entity)
        {
            var values = Columns.Where(c => c.Property != null).Select(c => c.ToDb(c.Property.GetValue(entity))).ToList();
            foreach (var child in Children)
            {
                var items = child.Items(entity).ToList();
                values.Add(items.Count);
                foreach (var item in items) values.AddRange(child.Snapshot(item));
            }
            return values;
        }
    }

    /// <summary>A table holding the items of one list property: parent key columns, Position, then the item's columns.</summary>
    internal sealed class ChildMap : TableMap
    {
        private readonly TableMap _parent;
        private readonly string _parentColumn;
        private readonly string _valueColumn;
        private readonly int? _valueLength;
        private bool _built;

        internal ChildMap(TableMap parent, string table, PropertyInfo property, string parentColumn, string valueColumn, int? valueLength)
            : base(table)
        {
            _parent = parent;
            _parentColumn = parentColumn;
            _valueColumn = valueColumn;
            _valueLength = valueLength;
            Property = property;
            ItemType = ElementType(property.PropertyType)
                ?? throw new ArgumentException(property.Name + " is not a list");
            if (ColumnMap.IsScalar(ItemType) != (valueColumn != null))
                throw new ArgumentException(property.Name + ": use Values() for lists of numbers or text and Child() for lists of objects");
        }

        internal PropertyInfo Property { get; }
        internal Type ItemType { get; }
        internal string[] ParentColumns { get; private set; }

        internal override void Validate()
        {
            if (_built) return;
            _built = true;

            var parentKey = _parent.PrimaryKey.Select(_parent.Find).ToList();
            ParentColumns = new string[parentKey.Count];
            for (var i = 0; i < parentKey.Count; i++)
            {
                ParentColumns[i] = i == parentKey.Count - 1 ? _parentColumn : parentKey[i].Name;
                AddColumn(new ColumnMap(ParentColumns[i], parentKey[i].ClrType, null) { AllowsNull = false, Length = parentKey[i].Length });
            }
            AddColumn(new ColumnMap("Position", typeof(int), null));

            if (_valueColumn != null)
            {
                AddColumn(new ColumnMap(_valueColumn, ItemType, null) { Length = _valueLength });
            }
            else
            {
                foreach (var p in MappableProperties(ItemType))
                {
                    if (!ColumnMap.IsScalar(p.PropertyType))
                        throw new InvalidOperationException(ItemType.Name + "." + p.Name + " cannot be stored in " + Name);
                    AddColumn(new ColumnMap(p.Name, p.PropertyType, p));
                }
            }

            PrimaryKey = ParentColumns.Concat(new[] { "Position" }).ToArray();
            ForeignKey = new ForeignKeyMap { Columns = ParentColumns, Parent = _parent };
            base.Validate();
        }

        internal IEnumerable<object> Items(object entity)
        {
            return (Property.GetValue(entity) as IEnumerable)?.Cast<object>() ?? Enumerable.Empty<object>();
        }

        internal void SetItems(object entity, List<object> items)
        {
            if (Property.PropertyType.IsArray)
            {
                var array = Array.CreateInstance(ItemType, items.Count);
                for (var i = 0; i < items.Count; i++) array.SetValue(items[i], i);
                Property.SetValue(entity, array);
                return;
            }
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ItemType));
            foreach (var item in items) list.Add(item);
            Property.SetValue(entity, list);
        }

        internal Row ToRow(object[] parentKey, int position, object item)
        {
            var row = new Row();
            for (var i = 0; i < ParentColumns.Length; i++) row[ParentColumns[i]] = parentKey[i];
            row["Position"] = position;
            if (_valueColumn != null)
                row[_valueColumn] = Find(_valueColumn).ToDb(item);
            else
                foreach (var column in Columns.Where(c => c.Property != null))
                    row[column.Name] = column.ToDb(column.Property.GetValue(item));
            return row;
        }

        internal object ReadItem(IDataRecord record)
        {
            if (_valueColumn != null)
            {
                var value = Find(_valueColumn).FromDb(record[_valueColumn]);
                return value ?? (ItemType.IsValueType ? Activator.CreateInstance(ItemType) : null);
            }
            return CreateFrom(ItemType, record, Columns);
        }

        internal IEnumerable<object> Snapshot(object item)
        {
            if (_valueColumn != null) return new[] { Find(_valueColumn).ToDb(item) };
            return Columns.Where(c => c.Property != null).Select(c => c.ToDb(c.Property.GetValue(item)));
        }

        private static Type ElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type)) return type.GetGenericArguments()[0];
            return null;
        }
    }
}
