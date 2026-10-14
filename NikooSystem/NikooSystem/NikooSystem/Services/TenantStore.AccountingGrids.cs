using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>Suppliers and customers of the books, and the paged, searchable tables of the accounting pages.</summary>
    public static partial class TenantStore
    {
        // ---- Suppliers and customers

        public static List<StoreParty> GetParties(string companyCode) => All(NikooTables.Parties, companyCode, "[Name], [Id]");

        public static StoreParty GetParty(string companyCode, int id) => id > 0 ? Find(NikooTables.Parties, companyCode, id) : null;

        public static string SaveParty(string companyCode, StoreParty party)
        {
            party.Name = (party.Name ?? "").Trim();
            party.Code = string.IsNullOrWhiteSpace(party.Code) ? null : party.Code.Trim();
            party.Mobile = string.IsNullOrWhiteSpace(party.Mobile) ? null : party.Mobile.Trim();
            party.Phone = string.IsNullOrWhiteSpace(party.Phone) ? null : party.Phone.Trim();
            if (party.Name.Length == 0) return "نام تامین‌کننده یا مشتری را بنویسید.";
            var all = GetParties(companyCode);
            if (all.Any(p => p.Id != party.Id && string.Equals(p.Name, party.Name, StringComparison.OrdinalIgnoreCase)))
                return "طرف حسابی با این نام در فهرست هست.";
            if (party.Code == null) party.Code = NextNumber(all.Select(p => p.Code), "TH-");
            if (all.Any(p => p.Id != party.Id && string.Equals(p.Code, party.Code, StringComparison.OrdinalIgnoreCase)))
                return "این کد قبلاً استفاده شده است.";
            var existing = party.Id > 0 ? all.FirstOrDefault(p => p.Id == party.Id) : null;
            if (party.Id > 0 && existing == null) return "طرف حساب پیدا نشد.";
            if (existing != null) party.CreatedAt = existing.CreatedAt;
            Save(NikooTables.Parties, companyCode, party);
            return null;
        }

        public static string DeleteParty(string companyCode, int id)
        {
            var party = GetParty(companyCode, id);
            if (party == null) return null;
            var used = NikooDb.Count(NikooTables.PurchaseInvoices, SqlFilter.ForStore(companyCode).Eq("PartyId", id)) > 0
                || NikooDb.Count(NikooTables.SalesInvoices, SqlFilter.ForStore(companyCode).Eq("PartyId", id)) > 0
                || NikooDb.Count(NikooTables.CashTransactions, SqlFilter.ForStore(companyCode).Eq("PartyId", id)) > 0;
            if (used) return "«" + party.Name + "» در فاکتور یا دریافت و پرداخت آمده و حذف نمی‌شود؛ غیرفعالش کنید.";
            Remove(NikooTables.Parties, companyCode, id);
            return null;
        }

        /// <summary>
        /// The party named <paramref name="name"/>, added to the list as <paramref name="kind"/> when it is new, so an invoice
        /// can name a supplier or customer that was not entered beforehand.
        /// </summary>
        public static StoreParty PartyByName(string companyCode, string name, PartyKind kind)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return null;
            var party = GetParties(companyCode).FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (party != null)
            {
                if (party.Kind != kind && party.Kind != PartyKind.Both)
                {
                    party.Kind = PartyKind.Both;
                    Save(NikooTables.Parties, companyCode, party);
                }
                return party;
            }
            party = new StoreParty { Name = name, Kind = kind, IsActive = true };
            return SaveParty(companyCode, party) == null ? party : null;
        }

        /// <summary>Site customers that have no party with the same mobile yet; picking one in a sales invoice adds it to the list.</summary>
        public static List<StoreCustomer> CustomersWithoutParty(string companyCode)
        {
            var mobiles = new HashSet<string>(GetParties(companyCode).Where(p => p.Mobile != null).Select(p => p.Mobile));
            return All(NikooTables.Customers, companyCode, "[FullName], [Id]")
                .Where(c => !string.IsNullOrWhiteSpace(c.FullName) && (c.Mobile == null || !mobiles.Contains(c.Mobile)))
                .ToList();
        }

        /// <summary>The party of a site customer, created as a customer the first time it is picked.</summary>
        public static StoreParty PartyForCustomer(string companyCode, int customerId)
        {
            var customer = GetCustomer(companyCode, customerId);
            if (customer == null) return null;
            var all = GetParties(companyCode);
            var party = all.FirstOrDefault(p => customer.Mobile != null && p.Mobile == customer.Mobile);
            if (party != null)
            {
                if (party.Buys) return party;
                party.Kind = PartyKind.Both;
                Save(NikooTables.Parties, companyCode, party);
                return party;
            }
            var name = (customer.FullName ?? "").Trim();
            if (all.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                name = name + " (" + customer.Mobile + ")";
            party = new StoreParty
            {
                Name = name,
                Kind = PartyKind.Customer,
                Mobile = customer.Mobile,
                Address = customer.Address,
                Note = "مشتری فروشگاه آنلاین",
                IsActive = true
            };
            return SaveParty(companyCode, party) == null ? party : null;
        }

        private static readonly Dictionary<string, string> PartySorts = new Dictionary<string, string>
        {
            { "name", "t.[Name]" },
            { "code", "t.[Code]" },
            { "kind", "t.[Kind]" },
            { "mobile", "t.[Mobile]" },
            { "created", "t.[CreatedAt]" }
        };

        public static PagedList<StoreParty> PartyGrid(string companyCode, PartyGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Name", "Code", "Mobile", "Phone", "Address");
            if (query.Kind.HasValue)
                filter.In("Kind", new[] { query.Kind.Value.ToString(), PartyKind.Both.ToString() }.Distinct());
            return Grid(NikooTables.Parties, companyCode, query, filter, PartySorts, "name", false);
        }

        // ---- Accounting tables

        private static readonly Dictionary<string, string> AccountSorts = new Dictionary<string, string>
        {
            { "code", "t.[Code]" },
            { "title", "t.[Title]" },
            { "kind", "t.[Kind]" }
        };

        public static PagedList<StoreLedgerAccount> AccountGrid(string companyCode, AccountGridQuery query)
        {
            EnsureAccounts(companyCode);
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Code", "Title");
            if (query.Kind.HasValue) filter.Eq("Kind", query.Kind.Value.ToString());
            return Grid(NikooTables.LedgerAccounts, companyCode, query, filter, AccountSorts, "code", false);
        }

        private static readonly Dictionary<string, string> JournalSorts = new Dictionary<string, string>
        {
            { "number", "t.[Number]" },
            { "date", "t.[EntryDate], t.[Id]" },
            { "source", "t.[Source]" }
        };

        public static PagedList<StoreJournalEntry> JournalGrid(string companyCode, JournalGridQuery query)
        {
            EnsureDocumentTypes(companyCode);
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Number", "Description");
            if (query.Type.HasValue) filter.Eq("DocumentTypeId", query.Type.Value);
            return Grid(NikooTables.JournalEntries, companyCode, query, filter, JournalSorts, "date", true);
        }

        private static readonly Dictionary<string, string> InvoiceSorts = new Dictionary<string, string>
        {
            { "number", "t.[Number]" },
            { "date", "t.[InvoiceDate], t.[Id]" },
            { "party", "t.[PartyName]" },
            { "pay", "t.[PayWith]" },
            { "total", "t.[Total]" }
        };

        public static PagedList<StorePurchaseInvoice> PurchaseGrid(string companyCode, InvoiceGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Number", "PartyName", "Note");
            if (query.Pay.HasValue) filter.Eq("PayWith", query.Pay.Value.ToString());
            return Grid(NikooTables.PurchaseInvoices, companyCode, query, filter, InvoiceSorts, "date", true);
        }

        public static PagedList<StoreSalesInvoice> SaleGrid(string companyCode, InvoiceGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Number", "PartyName", "Note");
            if (query.Pay.HasValue) filter.Eq("PayWith", query.Pay.Value.ToString());
            return Grid(NikooTables.SalesInvoices, companyCode, query, filter, InvoiceSorts, "date", true);
        }

        private static readonly Dictionary<string, string> CashSorts = new Dictionary<string, string>
        {
            { "number", "t.[Number]" },
            { "date", "t.[TxDate], t.[Id]" },
            { "party", "t.[PartyName]" },
            { "amount", "t.[Amount]" }
        };

        public static PagedList<StoreCashTransaction> CashGrid(string companyCode, CashGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Number", "PartyName", "Note");
            if (query.Flow.HasValue) filter.Eq("Direction", query.Flow.Value.ToString());
            return Grid(NikooTables.CashTransactions, companyCode, query, filter, CashSorts, "date", true);
        }

        private static readonly Dictionary<string, string> DocumentTypeSorts = new Dictionary<string, string>
        {
            { "code", "t.[Code]" },
            { "title", "t.[Title]" }
        };

        public static PagedList<StoreDocumentType> DocumentTypeGrid(string companyCode, GridQuery query)
        {
            EnsureDocumentTypes(companyCode);
            return Grid(NikooTables.DocumentTypes, companyCode, query,
                SqlFilter.ForStore(companyCode).Search(query.Q, "Code", "Title"),
                DocumentTypeSorts, "code", false);
        }

        private static readonly Dictionary<string, string> AssetSorts = new Dictionary<string, string>
        {
            { "code", "t.[Code]" },
            { "title", "t.[Title]" },
            { "date", "t.[BoughtOn]" },
            { "cost", "t.[Cost]" },
            { "location", "t.[Location]" }
        };

        public static PagedList<StoreFixedAsset> AssetGrid(string companyCode, GridQuery query) =>
            Grid(NikooTables.FixedAssets, companyCode, query,
                SqlFilter.ForStore(companyCode).Search(query.Q, "Code", "Title", "Location", "Note"),
                AssetSorts, "code", false);

        private static readonly Dictionary<string, string> GoodsSorts = new Dictionary<string, string>
        {
            { "title", "t.[Title]" },
            { "code", "t.[Code]" },
            { "qty", "t.[Qty]" }
        };

        public static PagedList<StoreGoodsItem> GoodsGrid(string companyCode, GridQuery query) =>
            Grid(NikooTables.GoodsItems, companyCode, query,
                SqlFilter.ForStore(companyCode).Search(query.Q, "Title", "Code"),
                GoodsSorts, "title", false);

        private static readonly Dictionary<string, string> StockSorts = new Dictionary<string, string>
        {
            { "title", "t.[Title]" },
            { "code", "t.[Sku]" },
            { "qty", "t.[Stock]" }
        };

        /// <summary>Physical products and their stock on hand.</summary>
        public static PagedList<StoreProduct> StockGrid(string companyCode, GridQuery query) =>
            Grid(NikooTables.Products, companyCode, query,
                SqlFilter.ForStore(companyCode).Eq("ProductType", StoreProductType.Physical.ToString()).Search(query.Q, "Title", "Sku"),
                StockSorts, "title", false);

        private static readonly Dictionary<string, string> MoveSorts = new Dictionary<string, string>
        {
            { "date", "t.[MoveDate], t.[Id]" },
            { "kind", "t.[Kind]" },
            { "qty", "t.[Qty]" },
            { "cost", "t.[UnitCost]" }
        };

        public static PagedList<StoreStockMove> MoveGrid(string companyCode, GridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode);
            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var text = query.Q.Trim();
                var products = Grid(NikooTables.Products, companyCode, new GridQuery { Size = PagedList<StoreProduct>.MaxPageSize },
                    SqlFilter.ForStore(companyCode).Search(text, "Title", "Sku"), StockSorts, "title", false).Select(p => p.Id).ToList();
                var goods = Grid(NikooTables.GoodsItems, companyCode, new GridQuery { Size = PagedList<StoreGoodsItem>.MaxPageSize },
                    SqlFilter.ForStore(companyCode).Search(text, "Title", "Code"), GoodsSorts, "title", false).Select(g => g.Id).ToList();
                var pattern = "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%";
                var clauses = new List<string> { "t.[Note] LIKE {0} ESCAPE '\\'" };
                var values = new List<object> { pattern };
                foreach (var id in products)
                {
                    clauses.Add("t.[ProductId] = {" + values.Count + "}");
                    values.Add(id);
                }
                foreach (var id in goods)
                {
                    clauses.Add("t.[GoodsId] = {" + values.Count + "}");
                    values.Add(id);
                }
                filter.Where(string.Join(" OR ", clauses), values.ToArray());
            }
            return Grid(NikooTables.StockMoves, companyCode, query, filter, MoveSorts, "date", true);
        }

        /// <summary>Product and warehouse item titles for the moves on one page.</summary>
        public static Dictionary<string, string> MoveTitles(string companyCode, IEnumerable<StoreStockMove> moves)
        {
            var list = moves.ToList();
            var titles = new Dictionary<string, string>();
            foreach (var product in GetProductsByIds(companyCode, list.Where(m => m.ProductId > 0).Select(m => m.ProductId).Distinct()))
                titles["p-" + product.Id] = product.Title;
            foreach (var goodsId in list.Where(m => m.ProductId <= 0 && m.GoodsId > 0).Select(m => m.GoodsId).Distinct())
            {
                var item = Find(NikooTables.GoodsItems, companyCode, goodsId);
                if (item != null) titles["g-" + goodsId] = item.Title;
            }
            return titles;
        }
    }
}
