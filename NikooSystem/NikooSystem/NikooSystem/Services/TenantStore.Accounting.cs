using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>
    /// Simple books for a shop or a sports club: chart of accounts, journal, purchase and sales invoices,
    /// stock moves and fixed assets. Online orders, class fees and court rent post themselves.
    /// </summary>
    public static partial class TenantStore
    {
        public static List<StoreLedgerAccount> GetAccounts(string companyCode)
        {
            EnsureAccounts(companyCode);
            return All(NikooTables.LedgerAccounts, companyCode, "[Code], [Id]");
        }

        public static StoreLedgerAccount GetAccount(string companyCode, int id) => Find(NikooTables.LedgerAccounts, companyCode, id);

        public static StoreLedgerAccount AccountByCode(string companyCode, string code) =>
            GetAccounts(companyCode).FirstOrDefault(a => a.Code == code);

        public static string SaveAccount(string companyCode, StoreLedgerAccount account)
        {
            account.Code = (account.Code ?? "").Trim();
            account.Title = (account.Title ?? "").Trim();
            if (account.Code.Length == 0 || account.Title.Length == 0) return "کد و عنوان سرفصل لازم است.";
            var all = GetAccounts(companyCode);
            if (all.Any(a => a.Id != account.Id && string.Equals(a.Code, account.Code, StringComparison.OrdinalIgnoreCase)))
                return "این کد سرفصل قبلاً استفاده شده است.";
            var existing = account.Id > 0 ? all.FirstOrDefault(a => a.Id == account.Id) : null;
            if (existing != null && existing.IsSystem)
            {
                account.Code = existing.Code;
                account.Kind = existing.Kind;
                account.IsSystem = true;
            }
            Save(NikooTables.LedgerAccounts, companyCode, account);
            return null;
        }

        public static string DeleteAccount(string companyCode, int id)
        {
            var account = GetAccount(companyCode, id);
            if (account == null) return null;
            if (account.IsSystem) return "سرفصل‌های پیش‌فرض حذف نمی‌شوند.";
            if (GetAccounts(companyCode).Any(a => a.ParentId == id)) return "اول سرفصل‌های زیرمجموعه را حذف کنید.";
            if (JournalLines(companyCode).Any(l => l.AccountId == id)) return "این سرفصل در سندها آمده و حذف نمی‌شود.";
            Remove(NikooTables.LedgerAccounts, companyCode, id);
            return null;
        }

        public static List<StoreJournalEntry> GetJournal(string companyCode) =>
            All(NikooTables.JournalEntries, companyCode, "[EntryDate] DESC, [Id] DESC");

        public static StoreJournalEntry GetEntry(string companyCode, int id) => Find(NikooTables.JournalEntries, companyCode, id);

        public static string SaveManualEntry(string companyCode, StoreJournalEntry entry)
        {
            entry.Description = (entry.Description ?? "").Trim();
            if (entry.Description.Length == 0) return "شرح سند را بنویسید.";
            var lines = CleanLines(entry.Lines);
            var error = Balanced(companyCode, lines);
            if (error != null) return error;
            entry.Lines = lines;
            entry.Source = JournalSource.Manual;
            entry.SourceId = 0;
            if (entry.Id > 0)
            {
                var stored = GetEntry(companyCode, entry.Id);
                if (stored == null) return "سند پیدا نشد.";
                if (stored.Source != JournalSource.Manual) return "سندهای خودکار از خود فاکتور یا سفارش اصلاح می‌شوند.";
                entry.Number = stored.Number;
            }
            else entry.Number = NextNumber(GetJournal(companyCode).Select(e => e.Number), "SN-");
            Save(NikooTables.JournalEntries, companyCode, entry);
            return null;
        }

        public static string DeleteManualEntry(string companyCode, int id)
        {
            var stored = GetEntry(companyCode, id);
            if (stored == null) return null;
            if (stored.Source != JournalSource.Manual) return "فقط سند دستی از اینجا حذف می‌شود.";
            Remove(NikooTables.JournalEntries, companyCode, id);
            return null;
        }

        public static List<StorePurchaseInvoice> GetPurchases(string companyCode) =>
            All(NikooTables.PurchaseInvoices, companyCode, "[InvoiceDate] DESC, [Id] DESC");

        public static StorePurchaseInvoice GetPurchase(string companyCode, int id) => Find(NikooTables.PurchaseInvoices, companyCode, id);

        public static string SavePurchase(string companyCode, StorePurchaseInvoice invoice)
        {
            var error = PreparePurchase(companyCode, invoice);
            if (error != null) return error;
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var stored = invoice.Id > 0 ? NikooDb.Get(NikooTables.PurchaseInvoices, NikooDb.Key(companyCode, invoice.Id), tx) : null;
                if (stored != null)
                {
                    error = ApplyPurchase(companyCode, stored, -1, tx);
                    if (error != null) return error;
                    DeleteEntry(companyCode, stored.EntryId, tx);
                    invoice.Number = stored.Number;
                }
                else
                {
                    invoice.Id = NikooDb.NextId(NikooTables.PurchaseInvoices, companyCode, tx);
                    invoice.Number = NextNumber(NikooDb.List(NikooTables.PurchaseInvoices, companyCode, tx: tx).Select(p => p.Number), "KH-");
                }
                error = ApplyPurchase(companyCode, invoice, +1, tx);
                if (error != null) return error;
                invoice.EntryId = WriteEntry(companyCode, new StoreJournalEntry
                {
                    Number = NextNumber(NikooDb.List(NikooTables.JournalEntries, companyCode, tx: tx).Select(e => e.Number), "SN-"),
                    EntryDate = invoice.InvoiceDate,
                    Description = "فاکتور خرید " + invoice.Number + " — " + invoice.PartyName,
                    Source = JournalSource.Purchase,
                    SourceId = invoice.Id,
                    Lines = PurchaseLines(companyCode, invoice)
                }, tx);
                invoice.Total = invoice.Lines.Sum(l => l.Amount);
                NikooDb.Upsert(NikooTables.PurchaseInvoices, invoice, companyCode, tx);
                tx.Commit();
                return null;
            }
        }

        public static string DeletePurchase(string companyCode, int id)
        {
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var stored = NikooDb.Get(NikooTables.PurchaseInvoices, NikooDb.Key(companyCode, id), tx);
                if (stored == null) return null;
                var error = ApplyPurchase(companyCode, stored, -1, tx);
                if (error != null) return error;
                DeleteEntry(companyCode, stored.EntryId, tx);
                NikooDb.Delete(NikooTables.PurchaseInvoices, NikooDb.Key(companyCode, id), tx);
                tx.Commit();
                return null;
            }
        }

        public static List<StoreSalesInvoice> GetSales(string companyCode) =>
            All(NikooTables.SalesInvoices, companyCode, "[InvoiceDate] DESC, [Id] DESC");

        public static StoreSalesInvoice GetSale(string companyCode, int id) => Find(NikooTables.SalesInvoices, companyCode, id);

        public static string SaveSale(string companyCode, StoreSalesInvoice invoice)
        {
            var error = PrepareSale(companyCode, invoice);
            if (error != null) return error;
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var stored = invoice.Id > 0 ? NikooDb.Get(NikooTables.SalesInvoices, NikooDb.Key(companyCode, invoice.Id), tx) : null;
                if (stored != null)
                {
                    error = ApplySale(companyCode, stored, -1, tx);
                    if (error != null) return error;
                    DeleteEntry(companyCode, stored.EntryId, tx);
                    invoice.Number = stored.Number;
                }
                else
                {
                    invoice.Id = NikooDb.NextId(NikooTables.SalesInvoices, companyCode, tx);
                    invoice.Number = NextNumber(NikooDb.List(NikooTables.SalesInvoices, companyCode, tx: tx).Select(p => p.Number), "FR-");
                }
                error = ApplySale(companyCode, invoice, +1, tx);
                if (error != null) return error;
                invoice.EntryId = WriteEntry(companyCode, new StoreJournalEntry
                {
                    Number = NextNumber(NikooDb.List(NikooTables.JournalEntries, companyCode, tx: tx).Select(e => e.Number), "SN-"),
                    EntryDate = invoice.InvoiceDate,
                    Description = "فاکتور فروش " + invoice.Number + " — " + invoice.PartyName,
                    Source = JournalSource.Sale,
                    SourceId = invoice.Id,
                    Lines = SaleLines(companyCode, invoice)
                }, tx);
                invoice.Total = invoice.Lines.Sum(l => l.Amount);
                NikooDb.Upsert(NikooTables.SalesInvoices, invoice, companyCode, tx);
                tx.Commit();
                return null;
            }
        }

        public static string DeleteSale(string companyCode, int id)
        {
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var stored = NikooDb.Get(NikooTables.SalesInvoices, NikooDb.Key(companyCode, id), tx);
                if (stored == null) return null;
                var error = ApplySale(companyCode, stored, -1, tx);
                if (error != null) return error;
                DeleteEntry(companyCode, stored.EntryId, tx);
                NikooDb.Delete(NikooTables.SalesInvoices, NikooDb.Key(companyCode, id), tx);
                tx.Commit();
                return null;
            }
        }

        public static List<StoreStockMove> GetStockMoves(string companyCode) =>
            All(NikooTables.StockMoves, companyCode, "[MoveDate] DESC, [Id] DESC");

        public static List<StoreGoodsItem> GetGoods(string companyCode) =>
            All(NikooTables.GoodsItems, companyCode, "[Title], [Id]");

        public static string SaveGoods(string companyCode, StoreGoodsItem item)
        {
            item.Title = (item.Title ?? "").Trim();
            item.Unit = string.IsNullOrWhiteSpace(item.Unit) ? "عدد" : item.Unit.Trim();
            if (item.Title.Length == 0) return "نام کالا را بنویسید.";
            if (GetGoods(companyCode).Any(g => g.Id != item.Id && string.Equals(g.Title, item.Title, StringComparison.OrdinalIgnoreCase)))
                return "کالایی با این نام در انبار هست.";
            var existing = item.Id > 0 ? Find(NikooTables.GoodsItems, companyCode, item.Id) : null;
            if (existing != null) item.Qty = existing.Qty;
            Save(NikooTables.GoodsItems, companyCode, item);
            return null;
        }

        public static string AdjustStock(string companyCode, int productId, int qty, string note)
        {
            if (qty == 0) return "مقدار تعدیل صفر است.";
            note = string.IsNullOrWhiteSpace(note) ? "تعدیل انبار" : note.Trim();
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var product = NikooDb.Get(NikooTables.Products, NikooDb.Key(companyCode, productId), tx);
                if (product == null || product.IsDigital()) return "این کالا در انبار ردیابی نمی‌شود.";
                if (product.Stock + qty < 0) return "موجودی «" + product.Title + "» کافی نیست.";
                var cost = AverageCost(companyCode, productId);
                NikooDb.Increment(NikooTables.Products, NikooDb.Key(companyCode, productId), "Stock", qty, tx);
                PostAdjust(companyCode, productId, 0, qty, cost, note, product.Title, tx);
                tx.Commit();
                return null;
            }
        }

        public static string AdjustGoods(string companyCode, int goodsId, int qty, string note)
        {
            if (qty == 0) return "مقدار تعدیل صفر است.";
            note = string.IsNullOrWhiteSpace(note) ? "تعدیل انبار" : note.Trim();
            EnsureAccounts(companyCode);
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var item = NikooDb.Get(NikooTables.GoodsItems, NikooDb.Key(companyCode, goodsId), tx);
                if (item == null) return "کالا پیدا نشد.";
                if (item.Qty + qty < 0) return "موجودی «" + item.Title + "» کافی نیست.";
                var cost = AverageGoodsCost(companyCode, goodsId);
                NikooDb.Increment(NikooTables.GoodsItems, NikooDb.Key(companyCode, goodsId), "Qty", qty, tx);
                PostAdjust(companyCode, 0, goodsId, qty, cost, note, item.Title, tx);
                tx.Commit();
                return null;
            }
        }

        public static List<StoreFixedAsset> GetAssets(string companyCode) =>
            All(NikooTables.FixedAssets, companyCode, "[Code], [Id]");

        public static StoreFixedAsset GetAsset(string companyCode, int id) => Find(NikooTables.FixedAssets, companyCode, id);

        public static string SaveAsset(string companyCode, StoreFixedAsset asset)
        {
            asset.Title = (asset.Title ?? "").Trim();
            asset.Code = (asset.Code ?? "").Trim();
            if (asset.Title.Length == 0) return "نام مال را بنویسید.";
            if (asset.Cost < 0) return "بهای تمام‌شده منفی نیست.";
            if (asset.Code.Length == 0) asset.Code = NextNumber(GetAssets(companyCode).Select(a => a.Code), "AM-");
            Save(NikooTables.FixedAssets, companyCode, asset);
            return null;
        }

        public static bool DeleteAsset(string companyCode, int id) => Remove(NikooTables.FixedAssets, companyCode, id);

        /// <summary>Posts a paid online order once. Stock itself was already moved with the order.</summary>
        public static void PostOnlineSale(string companyCode, int orderId)
        {
            var order = GetOrder(companyCode, orderId);
            if (order == null || !StoreOrder.Fulfilled.Contains(order.Status) || order.Amount <= 0) return;
            if (FindEntry(companyCode, JournalSource.Online, orderId) != null) return;
            EnsureAccounts(companyCode);
            var lines = new List<StoreJournalLine>();
            lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Bank), Debit = order.Amount, Memo = order.OrderNumber });
            lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.OnlineSales), Credit = order.Amount, Memo = order.OrderNumber });
            long cogs = 0;
            foreach (var item in order.Items ?? new List<StoreOrderItem>())
            {
                if (item.IsDigital || item.Quantity <= 0) continue;
                var cost = AverageCost(companyCode, item.ProductId) * item.Quantity;
                cogs += cost;
                AddMove(companyCode, item.ProductId, -item.Quantity, AverageCost(companyCode, item.ProductId), "online", orderId, order.OrderNumber);
            }
            if (cogs > 0)
            {
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Cogs), Debit = cogs, Memo = "بهای آنلاین" });
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Inventory), Credit = cogs, Memo = "بهای آنلاین" });
            }
            Save(NikooTables.JournalEntries, companyCode, new StoreJournalEntry
            {
                Number = NextNumber(GetJournal(companyCode).Select(e => e.Number), "SN-"),
                EntryDate = (order.PaidAt ?? order.CreatedAt).Date,
                Description = "فروش آنلاین " + order.OrderNumber,
                Source = JournalSource.Online,
                SourceId = orderId,
                Lines = lines
            });
        }

        public static void VoidOnlineSale(string companyCode, int orderId)
        {
            var entry = FindEntry(companyCode, JournalSource.Online, orderId);
            if (entry == null) return;
            entry.Reversed = true;
            Save(NikooTables.JournalEntries, companyCode, entry);
            Save(NikooTables.JournalEntries, companyCode, new StoreJournalEntry
            {
                Number = NextNumber(GetJournal(companyCode).Select(e => e.Number), "SN-"),
                EntryDate = DateTime.Today,
                Description = "برگشت " + entry.Description,
                Source = JournalSource.Online,
                SourceId = 0,
                Lines = entry.Lines.Select(l => new StoreJournalLine { AccountId = l.AccountId, Debit = l.Credit, Credit = l.Debit, Memo = l.Memo }).ToList()
            });
            foreach (var move in GetStockMoves(companyCode).Where(m => m.Kind == "online" && m.RefId == orderId))
                AddMove(companyCode, move.ProductId, -move.Qty, move.UnitCost, "online-void", orderId, "برگشت سفارش");
        }

        public static void PostIncome(string companyCode, JournalSource source, int sourceId, long amount, string description)
        {
            if (amount <= 0) return;
            if (FindEntry(companyCode, source, sourceId) != null) return;
            EnsureAccounts(companyCode);
            Save(NikooTables.JournalEntries, companyCode, new StoreJournalEntry
            {
                Number = NextNumber(GetJournal(companyCode).Select(e => e.Number), "SN-"),
                EntryDate = DateTime.Today,
                Description = description,
                Source = source,
                SourceId = sourceId,
                Lines = Pair(IdOf(companyCode, AccountCodes.Bank), IdOf(companyCode, AccountCodes.Tuition), amount, description).ToList()
            });
        }

        public static long AverageCost(string companyCode, int productId)
        {
            var inbound = GetStockMoves(companyCode).Where(m => m.ProductId == productId && m.Qty > 0 && m.UnitCost > 0).ToList();
            var qty = inbound.Sum(m => m.Qty);
            return qty <= 0 ? 0 : inbound.Sum(m => m.UnitCost * m.Qty) / qty;
        }

        private static void EnsureAccounts(string companyCode)
        {
            if (All(NikooTables.LedgerAccounts, companyCode).Count > 0) return;
            var rows = new[]
            {
                Row(AccountCodes.Cash, "صندوق", AccountKind.Asset),
                Row(AccountCodes.Bank, "بانک", AccountKind.Asset),
                Row(AccountCodes.Receivable, "حساب‌های دریافتنی", AccountKind.Asset),
                Row(AccountCodes.Inventory, "موجودی کالا", AccountKind.Asset),
                Row(AccountCodes.FixedAssets, "اموال و دارایی ثابت", AccountKind.Asset),
                Row(AccountCodes.Payable, "حساب‌های پرداختنی", AccountKind.Liability),
                Row(AccountCodes.Capital, "سرمایه", AccountKind.Equity),
                Row(AccountCodes.Sales, "فروش", AccountKind.Income),
                Row(AccountCodes.OnlineSales, "فروش آنلاین", AccountKind.Income),
                Row(AccountCodes.Tuition, "درآمد شهریه و رزرو", AccountKind.Income),
                Row(AccountCodes.Cogs, "بهای تمام‌شده کالا", AccountKind.Expense),
                Row(AccountCodes.Expense, "هزینه‌های عمومی", AccountKind.Expense),
                Row(AccountCodes.StockAdjust, "تعدیل موجودی", AccountKind.Expense)
            };
            foreach (var row in rows) Save(NikooTables.LedgerAccounts, companyCode, row);
        }

        private static StoreLedgerAccount Row(string code, string title, AccountKind kind) =>
            new StoreLedgerAccount { Code = code, Title = title, Kind = kind, IsSystem = true, IsActive = true };

        private static int IdOf(string companyCode, string code)
        {
            var account = AccountByCode(companyCode, code);
            if (account == null) throw new InvalidOperationException("سرفصل " + code + " پیدا نشد.");
            return account.Id;
        }

        private static string PayAccount(PayWith pay, bool incoming)
        {
            if (pay == PayWith.Bank) return AccountCodes.Bank;
            if (pay == PayWith.Credit) return incoming ? AccountCodes.Receivable : AccountCodes.Payable;
            return AccountCodes.Cash;
        }

        private static List<StoreJournalLine> PurchaseLines(string companyCode, StorePurchaseInvoice invoice)
        {
            var lines = new List<StoreJournalLine>();
            foreach (var line in invoice.Lines)
            {
                var code = line.Kind == PurchaseLineKind.Expense ? AccountCodes.Expense
                    : line.Kind == PurchaseLineKind.Asset ? AccountCodes.FixedAssets
                    : AccountCodes.Inventory;
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, code), Debit = line.Amount, Memo = line.Title });
            }
            lines.Add(new StoreJournalLine
            {
                AccountId = IdOf(companyCode, PayAccount(invoice.PayWith, false)),
                Credit = invoice.Lines.Sum(l => l.Amount),
                Memo = invoice.PartyName
            });
            return lines;
        }

        private static List<StoreJournalLine> SaleLines(string companyCode, StoreSalesInvoice invoice)
        {
            var total = invoice.Lines.Sum(l => l.Amount);
            var lines = new List<StoreJournalLine>
            {
                new StoreJournalLine { AccountId = IdOf(companyCode, PayAccount(invoice.PayWith, true)), Debit = total, Memo = invoice.PartyName },
                new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Sales), Credit = total, Memo = invoice.PartyName }
            };
            long cogs = 0;
            foreach (var line in invoice.Lines)
            {
                if (line.ProductId > 0)
                {
                    var product = GetProduct(companyCode, line.ProductId);
                    if (product == null || product.IsDigital()) continue;
                    cogs += AverageCost(companyCode, line.ProductId) * line.Qty;
                }
                else if (line.GoodsId > 0)
                {
                    cogs += AverageGoodsCost(companyCode, line.GoodsId) * line.Qty;
                }
            }
            if (cogs > 0)
            {
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Cogs), Debit = cogs, Memo = "بهای تمام‌شده" });
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Inventory), Credit = cogs, Memo = "بهای تمام‌شده" });
            }
            return lines;
        }

        private static string PreparePurchase(string companyCode, StorePurchaseInvoice invoice)
        {
            EnsureAccounts(companyCode);
            invoice.PartyName = (invoice.PartyName ?? "").Trim();
            if (invoice.PartyName.Length == 0) return "نام فروشنده را بنویسید.";
            invoice.Lines = (invoice.Lines ?? new List<StorePurchaseLine>()).Where(l => l.Qty > 0 && (l.ProductId > 0 || l.GoodsId > 0 || !string.IsNullOrWhiteSpace(l.Title))).ToList();
            if (invoice.Lines.Count == 0) return "حداقل یک ردیف خرید لازم است.";
            foreach (var line in invoice.Lines)
            {
                if (line.UnitCost < 0) return "بهای واحد منفی نیست.";
                if (line.ProductId > 0)
                {
                    var product = GetProduct(companyCode, line.ProductId);
                    if (product == null) return "کالای انتخاب‌شده پیدا نشد.";
                    if (product.IsDigital()) line.Kind = PurchaseLineKind.Expense;
                    if (string.IsNullOrWhiteSpace(line.Title)) line.Title = product.Title;
                    line.GoodsId = 0;
                }
                else if (line.Kind == PurchaseLineKind.Goods && line.GoodsId > 0)
                {
                    var goods = Find(NikooTables.GoodsItems, companyCode, line.GoodsId);
                    if (goods == null) return "کالای انبار پیدا نشد.";
                    if (string.IsNullOrWhiteSpace(line.Title)) line.Title = goods.Title;
                }
                else if (string.IsNullOrWhiteSpace(line.Title)) line.Title = "ردیف خرید";
            }
            return null;
        }

        private static string PrepareSale(string companyCode, StoreSalesInvoice invoice)
        {
            EnsureAccounts(companyCode);
            invoice.PartyName = (invoice.PartyName ?? "").Trim();
            if (invoice.PartyName.Length == 0) return "نام خریدار را بنویسید.";
            invoice.Lines = (invoice.Lines ?? new List<StoreSaleLine>()).Where(l => l.Qty > 0 && (l.ProductId > 0 || l.GoodsId > 0 || !string.IsNullOrWhiteSpace(l.Title))).ToList();
            if (invoice.Lines.Count == 0) return "حداقل یک ردیف فروش لازم است.";
            foreach (var line in invoice.Lines)
            {
                if (line.UnitPrice < 0) return "قیمت واحد منفی نیست.";
                if (line.ProductId > 0)
                {
                    var product = GetProduct(companyCode, line.ProductId);
                    if (product == null) return "کالای انتخاب‌شده پیدا نشد.";
                    if (string.IsNullOrWhiteSpace(line.Title)) line.Title = product.Title;
                    line.GoodsId = 0;
                }
                else if (line.GoodsId > 0)
                {
                    var goods = Find(NikooTables.GoodsItems, companyCode, line.GoodsId);
                    if (goods == null) return "کالای انبار پیدا نشد.";
                    if (string.IsNullOrWhiteSpace(line.Title)) line.Title = goods.Title;
                }
                else if (string.IsNullOrWhiteSpace(line.Title)) line.Title = "ردیف فروش";
            }
            return null;
        }

        /// <summary><paramref name="sign"/> +1 receives a purchase; -1 puts it back. Stock is read inside the same transaction.</summary>
        private static string ApplyPurchase(string companyCode, StorePurchaseInvoice invoice, int sign, DbTx tx)
        {
            foreach (var line in invoice.Lines.Where(l => l.Kind == PurchaseLineKind.Goods))
            {
                var name = StockName(companyCode, line.ProductId, line.GoodsId, line.Title, tx);
                var onHand = OnHand(companyCode, line.ProductId, line.GoodsId, tx);
                if (onHand == null) continue;
                if (onHand.Value + sign * line.Qty < 0) return "موجودی «" + name + "» برای برگشت این خرید کافی نیست.";
            }
            if (sign < 0) DeleteMoves(companyCode, "purchase", invoice.Id, tx);
            foreach (var line in invoice.Lines.Where(l => l.Kind == PurchaseLineKind.Goods))
            {
                var target = sign > 0 ? ReceiveTarget(companyCode, line, tx) : new StockTarget(line.ProductId, line.GoodsId, Tracked(companyCode, line.ProductId, line.GoodsId, tx));
                if (!target.Tracked) continue;
                if (target.ProductId > 0)
                    NikooDb.Increment(NikooTables.Products, NikooDb.Key(companyCode, target.ProductId), "Stock", sign * line.Qty, tx);
                else
                    NikooDb.Increment(NikooTables.GoodsItems, NikooDb.Key(companyCode, target.GoodsId), "Qty", sign * line.Qty, tx);
                line.GoodsId = target.GoodsId;
                if (sign > 0)
                {
                    NikooDb.Upsert(NikooTables.StockMoves, new StoreStockMove
                    {
                        Id = NikooDb.NextId(NikooTables.StockMoves, companyCode, tx),
                        ProductId = target.ProductId,
                        GoodsId = target.GoodsId,
                        MoveDate = invoice.InvoiceDate,
                        Qty = line.Qty,
                        UnitCost = line.UnitCost,
                        Kind = "purchase",
                        RefId = invoice.Id,
                        Note = invoice.Number
                    }, companyCode, tx);
                }
            }
            return null;
        }

        private static string ApplySale(string companyCode, StoreSalesInvoice invoice, int sign, DbTx tx)
        {
            foreach (var line in invoice.Lines)
            {
                var onHand = OnHand(companyCode, line.ProductId, line.GoodsId, tx);
                if (onHand == null) continue;
                if (onHand.Value - sign * line.Qty < 0)
                    return "موجودی «" + StockName(companyCode, line.ProductId, line.GoodsId, line.Title, tx) + "» کافی نیست.";
            }
            if (sign < 0) DeleteMoves(companyCode, "sale", invoice.Id, tx);
            foreach (var line in invoice.Lines)
            {
                var onHand = OnHand(companyCode, line.ProductId, line.GoodsId, tx);
                if (onHand == null) continue;
                var cost = line.ProductId > 0 ? AverageCost(companyCode, line.ProductId) : AverageGoodsCost(companyCode, line.GoodsId);
                if (line.ProductId > 0)
                    NikooDb.Increment(NikooTables.Products, NikooDb.Key(companyCode, line.ProductId), "Stock", -sign * line.Qty, tx);
                else
                    NikooDb.Increment(NikooTables.GoodsItems, NikooDb.Key(companyCode, line.GoodsId), "Qty", -sign * line.Qty, tx);
                if (sign > 0)
                {
                    NikooDb.Upsert(NikooTables.StockMoves, new StoreStockMove
                    {
                        Id = NikooDb.NextId(NikooTables.StockMoves, companyCode, tx),
                        ProductId = line.ProductId,
                        GoodsId = line.GoodsId,
                        MoveDate = invoice.InvoiceDate,
                        Qty = -line.Qty,
                        UnitCost = cost,
                        Kind = "sale",
                        RefId = invoice.Id,
                        Note = invoice.Number
                    }, companyCode, tx);
                }
            }
            return null;
        }

        private struct StockTarget
        {
            public StockTarget(int productId, int goodsId, bool tracked)
            {
                ProductId = productId;
                GoodsId = goodsId;
                Tracked = tracked;
            }

            public int ProductId;
            public int GoodsId;
            public bool Tracked;
        }

        private static StockTarget ReceiveTarget(string companyCode, StorePurchaseLine line, DbTx tx)
        {
            if (line.ProductId > 0)
            {
                var product = NikooDb.Get(NikooTables.Products, NikooDb.Key(companyCode, line.ProductId), tx);
                if (product == null || product.IsDigital()) return new StockTarget(0, 0, false);
                return new StockTarget(line.ProductId, 0, true);
            }
            var goods = line.GoodsId > 0 ? NikooDb.Get(NikooTables.GoodsItems, NikooDb.Key(companyCode, line.GoodsId), tx) : null;
            if (goods == null)
            {
                var title = (line.Title ?? "").Trim();
                goods = NikooDb.List(NikooTables.GoodsItems, companyCode, tx: tx)
                    .FirstOrDefault(g => string.Equals(g.Title, title, StringComparison.OrdinalIgnoreCase));
            }
            if (goods == null)
            {
                goods = new StoreGoodsItem
                {
                    Id = NikooDb.NextId(NikooTables.GoodsItems, companyCode, tx),
                    Title = (line.Title ?? "").Trim(),
                    Unit = "عدد",
                    Qty = 0,
                    IsActive = true
                };
                NikooDb.Upsert(NikooTables.GoodsItems, goods, companyCode, tx);
            }
            line.GoodsId = goods.Id;
            if (string.IsNullOrWhiteSpace(line.Title)) line.Title = goods.Title;
            return new StockTarget(0, goods.Id, true);
        }

        private static bool Tracked(string companyCode, int productId, int goodsId, DbTx tx) => OnHand(companyCode, productId, goodsId, tx).HasValue;

        /// <summary>Null when this line does not move a counted quantity.</summary>
        private static int? OnHand(string companyCode, int productId, int goodsId, DbTx tx)
        {
            if (productId > 0)
            {
                var product = NikooDb.Get(NikooTables.Products, NikooDb.Key(companyCode, productId), tx);
                if (product == null || product.IsDigital()) return null;
                return product.Stock;
            }
            if (goodsId <= 0) return null;
            var goods = NikooDb.Get(NikooTables.GoodsItems, NikooDb.Key(companyCode, goodsId), tx);
            return goods == null ? (int?)null : goods.Qty;
        }

        private static string StockName(string companyCode, int productId, int goodsId, string fallback, DbTx tx)
        {
            if (productId > 0)
            {
                var product = NikooDb.Get(NikooTables.Products, NikooDb.Key(companyCode, productId), tx);
                if (product != null) return product.Title;
            }
            if (goodsId > 0)
            {
                var goods = NikooDb.Get(NikooTables.GoodsItems, NikooDb.Key(companyCode, goodsId), tx);
                if (goods != null) return goods.Title;
            }
            return string.IsNullOrWhiteSpace(fallback) ? "کالا" : fallback;
        }

        private static void PostAdjust(string companyCode, int productId, int goodsId, int qty, long cost, string note, string title, DbTx tx)
        {
            var moveId = NikooDb.NextId(NikooTables.StockMoves, companyCode, tx);
            NikooDb.Upsert(NikooTables.StockMoves, new StoreStockMove
            {
                Id = moveId,
                ProductId = productId,
                GoodsId = goodsId,
                MoveDate = DateTime.Today,
                Qty = qty,
                UnitCost = cost,
                Kind = "adjust",
                Note = note
            }, companyCode, tx);
            var amount = Math.Abs(qty) * cost;
            if (amount <= 0) return;
            var inventory = IdOf(companyCode, AccountCodes.Inventory);
            var adjust = IdOf(companyCode, AccountCodes.StockAdjust);
            WriteEntry(companyCode, new StoreJournalEntry
            {
                Number = NextNumber(NikooDb.List(NikooTables.JournalEntries, companyCode, tx: tx).Select(e => e.Number), "SN-"),
                EntryDate = DateTime.Today,
                Description = note + " — " + title,
                Source = JournalSource.Adjust,
                SourceId = moveId,
                Lines = (qty > 0 ? Pair(inventory, adjust, amount, title) : Pair(adjust, inventory, amount, title)).ToList()
            }, tx);
        }

        public static long AverageGoodsCost(string companyCode, int goodsId)
        {
            var inbound = GetStockMoves(companyCode).Where(m => m.GoodsId == goodsId && m.Qty > 0 && m.UnitCost > 0).ToList();
            var qty = inbound.Sum(m => (long)m.Qty);
            return qty <= 0 ? 0 : inbound.Sum(m => m.UnitCost * m.Qty) / qty;
        }

        private static void DeleteMoves(string companyCode, string kind, int refId, DbTx tx)
        {
            foreach (var move in NikooDb.List(NikooTables.StockMoves, companyCode, tx: tx).Where(m => m.Kind == kind && m.RefId == refId))
                NikooDb.Delete(NikooTables.StockMoves, NikooDb.Key(companyCode, move.Id), tx);
        }

        private static void AddMove(string companyCode, int productId, int qty, long unitCost, string kind, int refId, string note)
        {
            Save(NikooTables.StockMoves, companyCode, new StoreStockMove
            {
                ProductId = productId,
                MoveDate = DateTime.Today,
                Qty = qty,
                UnitCost = unitCost,
                Kind = kind,
                RefId = refId,
                Note = note
            });
        }

        private static int WriteEntry(string companyCode, StoreJournalEntry entry, DbTx tx)
        {
            entry.Id = NikooDb.NextId(NikooTables.JournalEntries, companyCode, tx);
            NikooDb.Upsert(NikooTables.JournalEntries, entry, companyCode, tx);
            return entry.Id;
        }

        private static void DeleteEntry(string companyCode, int entryId, DbTx tx)
        {
            if (entryId <= 0) return;
            NikooDb.Delete(NikooTables.JournalEntries, NikooDb.Key(companyCode, entryId), tx);
        }

        private static StoreJournalEntry FindEntry(string companyCode, JournalSource source, int sourceId) =>
            GetJournal(companyCode).FirstOrDefault(e => e.Source == source && e.SourceId == sourceId && !e.Reversed);

        private static IEnumerable<StoreJournalLine> JournalLines(string companyCode) =>
            GetJournal(companyCode).SelectMany(e => e.Lines ?? new List<StoreJournalLine>());

        private static List<StoreJournalLine> CleanLines(IEnumerable<StoreJournalLine> lines) =>
            (lines ?? Enumerable.Empty<StoreJournalLine>()).Where(l => l.AccountId > 0 && (l.Debit > 0 || l.Credit > 0)).ToList();

        private static string Balanced(string companyCode, List<StoreJournalLine> lines)
        {
            if (lines.Count < 2) return "سند حداقل دو ردیف دارد.";
            var accounts = GetAccounts(companyCode);
            long debit = 0, credit = 0;
            foreach (var line in lines)
            {
                if (line.Debit > 0 && line.Credit > 0) return "یک ردیف هم‌زمان بدهکار و بستانکار نیست.";
                if (line.Debit < 0 || line.Credit < 0) return "مبلغ منفی نیست.";
                if (accounts.All(a => a.Id != line.AccountId)) return "سرفصل یک ردیف پیدا نشد.";
                debit += line.Debit;
                credit += line.Credit;
            }
            if (debit != credit || debit == 0) return "جمع بدهکار و بستانکار باید برابر و بیشتر از صفر باشد.";
            return null;
        }

        private static IEnumerable<StoreJournalLine> Pair(int debitAccount, int creditAccount, long amount, string memo)
        {
            yield return new StoreJournalLine { AccountId = debitAccount, Debit = amount, Memo = memo };
            yield return new StoreJournalLine { AccountId = creditAccount, Credit = amount, Memo = memo };
        }

        private static string NextNumber(IEnumerable<string> numbers, string prefix)
        {
            var max = 0;
            foreach (var number in numbers)
            {
                if (number == null || !number.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                int n;
                if (int.TryParse(number.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > max)
                    max = n;
            }
            return prefix + (max + 1).ToString("0000", CultureInfo.InvariantCulture);
        }
    }
}
