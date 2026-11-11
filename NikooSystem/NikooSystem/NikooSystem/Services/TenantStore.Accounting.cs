using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NikooSystem.Helpers;
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

        public static List<StoreJournalEntry> GetJournal(string companyCode) =>
            All(NikooTables.JournalEntries, companyCode, "[EntryDate] DESC, [Id] DESC");

        public static StoreJournalEntry GetEntry(string companyCode, int id) => Find(NikooTables.JournalEntries, companyCode, id);

        public static string SaveManualEntry(string companyCode, StoreJournalEntry entry)
        {
            entry.Description = (entry.Description ?? "").Trim();
            if (entry.Description.Length == 0) return "شرح سند را بنویسید.";
            var fiscalError = CheckFiscalDate(companyCode, entry.EntryDate);
            if (fiscalError != null) return fiscalError;
            EnsureAccounts(companyCode);
            var lines = CleanLines(entry.Lines);
            var error = Balanced(companyCode, lines);
            if (error != null) return error;
            entry.Lines = lines;
            entry.Source = JournalSource.Manual;
            entry.SourceId = 0;
            var types = GetDocumentTypes(companyCode);
            var chosen = types.FirstOrDefault(t => t.Id == entry.DocumentTypeId);
            if (chosen == null) chosen = types.First(t => t.Code == DocumentTypeCodes.Manual);
            if (!chosen.IsActive) return "این نوع سند غیرفعال است.";
            entry.DocumentTypeId = chosen.Id;
            if (entry.Id > 0)
            {
                var stored = GetEntry(companyCode, entry.Id);
                if (stored == null) return "سند پیدا نشد.";
                if (stored.Source != JournalSource.Manual) return "سندهای خودکار از خود فاکتور یا سفارش اصلاح می‌شوند.";
                entry.Number = stored.Number;
            }
            else entry.Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-");
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
            var fiscalError = CheckFiscalDate(companyCode, invoice.InvoiceDate);
            if (fiscalError != null) return fiscalError;
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
                    invoice.Number = NextSerial(NikooTables.PurchaseInvoices, companyCode, "Number", "KH-", tx);
                }
                error = ApplyPurchase(companyCode, invoice, +1, tx);
                if (error != null) return error;
                ApplyInvoiceTax(invoice);
                invoice.EntryId = WriteEntry(companyCode, new StoreJournalEntry
                {
                    Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-", tx),
                    EntryDate = invoice.InvoiceDate,
                    Description = "فاکتور خرید " + invoice.Number + " — " + invoice.PartyName,
                    Source = JournalSource.Purchase,
                    SourceId = invoice.Id,
                    DocumentTypeId = TypeId(companyCode, DocumentTypeCodes.Purchase),
                    Lines = PurchaseLines(companyCode, invoice)
                }, tx);
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
            var fiscalError = CheckFiscalDate(companyCode, invoice.InvoiceDate);
            if (fiscalError != null) return fiscalError;
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
                    invoice.Number = NextSerial(NikooTables.SalesInvoices, companyCode, "Number", "FR-", tx);
                }
                error = ApplySale(companyCode, invoice, +1, tx);
                if (error != null) return error;
                ApplyInvoiceTax(invoice);
                invoice.EntryId = WriteEntry(companyCode, new StoreJournalEntry
                {
                    Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-", tx),
                    EntryDate = invoice.InvoiceDate,
                    Description = "فاکتور فروش " + invoice.Number + " — " + invoice.PartyName,
                    Source = JournalSource.Sale,
                    SourceId = invoice.Id,
                    DocumentTypeId = TypeId(companyCode, DocumentTypeCodes.Sale),
                    Lines = SaleLines(companyCode, invoice, tx)
                }, tx);
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
            item.Code = string.IsNullOrWhiteSpace(item.Code) ? null : item.Code.Trim();
            if (item.Title.Length == 0) return "نام کالا را بنویسید.";
            if (GetGoods(companyCode).Any(g => g.Id != item.Id && string.Equals(g.Title, item.Title, StringComparison.OrdinalIgnoreCase)))
                return "کالایی با این نام در انبار هست.";
            if (item.Code != null && GetGoods(companyCode).Any(g => g.Id != item.Id && string.Equals(g.Code, item.Code, StringComparison.OrdinalIgnoreCase)))
                return "این کد کالا قبلاً استفاده شده است.";
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
                var cost = AverageCost(companyCode, productId, tx);
                NikooDb.Increment(NikooTables.Products, NikooDb.Key(companyCode, productId), "Stock", qty, tx);
                PostAdjust(companyCode, productId, 0, qty, cost, note, product.Title, tx);
                tx.Commit();
                return null;
            }
        }

        /// <summary>
        /// Opening quantity already written on a new product row; only the stock move (and journal when cost is known)
        /// is posted so انبار stays in sync.
        /// </summary>
        internal static void RecordProductOpening(string companyCode, int productId, int qty, string title)
        {
            if (qty <= 0 || productId <= 0) return;
            EnsureAccounts(companyCode);
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var product = NikooDb.Get(NikooTables.Products, NikooDb.Key(companyCode, productId), tx);
                if (product == null || product.IsDigital()) return;
                PostAdjust(companyCode, productId, 0, qty, 0, "موجودی اولیه", string.IsNullOrWhiteSpace(title) ? product.Title : title, tx);
                tx.Commit();
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
                var cost = AverageGoodsCost(companyCode, goodsId, tx);
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
            PostOrderIncome(companyCode, orderId, PayWith.Bank, 0, true, "فروش آنلاین ");
        }

        /// <summary>Posts a manual (cash/bank) digital sale for an already-completed order.</summary>
        public static void PostManualDigitalSale(string companyCode, int orderId, PayWith payWith, int bankId)
        {
            PostOrderIncome(companyCode, orderId, payWith, bankId, false, "فروش دستی ");
        }

        private static void PostOrderIncome(string companyCode, int orderId, PayWith payWith, int bankId, bool gatewayBank, string descriptionPrefix)
        {
            var order = GetOrder(companyCode, orderId);
            if (order == null || !StoreOrder.Fulfilled.Contains(order.Status) || order.Amount <= 0) return;
            if (FindEntry(companyCode, JournalSource.Online, orderId) != null) return;
            EnsureAccounts(companyCode);
            var debitAccount = gatewayBank
                ? OnlineBankAccountId(companyCode)
                : MoneyAccountId(companyCode, payWith == PayWith.Credit ? PayWith.Cash : payWith, bankId, true);
            var shipping = Math.Max(0, Math.Min(order.ShippingCost, order.Amount));
            var goods = order.Amount - shipping;
            var lines = new List<StoreJournalLine>();
            lines.Add(new StoreJournalLine { AccountId = debitAccount, Debit = order.Amount, Memo = order.OrderNumber });
            if (goods > 0)
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.OnlineSales), Credit = goods, Memo = order.OrderNumber });
            if (shipping > 0)
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.ShippingIncome), Credit = shipping, Memo = "ارسال " + order.OrderNumber });
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
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Cogs), Debit = cogs, Memo = "بهای فروش" });
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Inventory), Credit = cogs, Memo = "بهای فروش" });
            }
            Save(NikooTables.JournalEntries, companyCode, new StoreJournalEntry
            {
                Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-"),
                EntryDate = (order.PaidAt ?? order.CreatedAt).Date,
                Description = descriptionPrefix + order.OrderNumber,
                Source = JournalSource.Online,
                SourceId = orderId,
                DocumentTypeId = TypeId(companyCode, DocumentTypeCodes.Online),
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
                Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-"),
                EntryDate = DateTime.Today,
                Description = "برگشت " + entry.Description,
                Source = JournalSource.Online,
                SourceId = 0,
                DocumentTypeId = entry.DocumentTypeId,
                Lines = entry.Lines.Select(l => new StoreJournalLine { AccountId = l.AccountId, Debit = l.Credit, Credit = l.Debit, Memo = l.Memo }).ToList()
            });
            foreach (var move in MovesFor(companyCode, "online", orderId))
                AddMove(companyCode, move.ProductId, -move.Qty, move.UnitCost, "online-void", orderId, "برگشت سفارش");
        }

        public static void PostIncome(string companyCode, JournalSource source, int sourceId, long amount, string description)
        {
            if (amount <= 0) return;
            if (FindEntry(companyCode, source, sourceId) != null) return;
            EnsureAccounts(companyCode);
            EnsureDocumentTypes(companyCode);
            Save(NikooTables.JournalEntries, companyCode, new StoreJournalEntry
            {
                Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-"),
                EntryDate = DateTime.Today,
                Description = description,
                Source = source,
                SourceId = sourceId,
                DocumentTypeId = TypeId(companyCode, IncomeDocumentType(source)),
                Lines = Pair(OnlineBankAccountId(companyCode), IdOf(companyCode, IncomeAccount(source)), amount, description).ToList()
            });
        }

        /// <summary>Visit fees are their own income; class tuition and hall bookings stay on the tuition account.</summary>
        private static string IncomeAccount(JournalSource source) =>
            source == JournalSource.Appointment ? AccountCodes.Visit : AccountCodes.Tuition;

        private static string IncomeDocumentType(JournalSource source)
        {
            if (source == JournalSource.Facility) return DocumentTypeCodes.Facility;
            if (source == JournalSource.Appointment) return DocumentTypeCodes.Appointment;
            return DocumentTypeCodes.Tuition;
        }

        /// <summary>
        /// Records cash/bank payment for a booking appointment: updates PaidAmount and writes a cash receipt
        /// against visit income, so a clinic visit is not posted as tuition.
        /// </summary>
        public static string RecordAppointmentPayment(string companyCode, int appointmentId, long amount, PayWith payWith, int bankId)
        {
            if (amount <= 0) return "مبلغ دریافت باید بیشتر از صفر باشد.";
            StoreAppointment appointment;
            lock (LockFor(companyCode))
            {
                appointment = GetAppointment(companyCode, appointmentId);
                if (appointment == null) return "نوبت پیدا نشد.";
                if (appointment.Status == AppointmentStatus.Cancelled) return "نوبت لغوشده قابل تسویه نیست.";
                var due = Math.Max(0, appointment.Price - appointment.PaidAmount);
                if (due <= 0) return "مبلغ این نوبت کامل دریافت شده است.";
                if (amount > due) amount = due;
            }

            EnsureAccounts(companyCode);
            var visit = AccountByCode(companyCode, AccountCodes.Visit);
            if (visit == null) return "سرفصل درآمد ویزیت پیدا نشد.";
            var party = PartyByName(companyCode, string.IsNullOrWhiteSpace(appointment.CustomerName) ? "مراجع نوبت" : appointment.CustomerName, PartyKind.Customer);
            var cash = new StoreCashTransaction
            {
                Direction = CashDirection.In,
                Amount = amount,
                PayWith = payWith == PayWith.Credit ? PayWith.Cash : payWith,
                BankId = bankId,
                AccountId = visit.Id,
                PartyId = party == null ? 0 : party.Id,
                PartyName = appointment.CustomerName,
                Note = "نوبت " + appointment.Code + " — " + appointment.ServiceTitle,
                TxDate = DateTime.Today
            };
            var error = SaveCash(companyCode, cash);
            if (error != null) return error;

            lock (LockFor(companyCode))
            {
                appointment = GetAppointment(companyCode, appointmentId);
                if (appointment == null) return null;
                appointment.PaidAmount += amount;
                if (!appointment.PaidAt.HasValue) appointment.PaidAt = DateTime.Now;
                if (appointment.Status == AppointmentStatus.PendingPayment)
                    appointment.Status = AppointmentStatus.Confirmed;
                if (string.IsNullOrWhiteSpace(appointment.PaymentMessage))
                    appointment.PaymentMessage = "دریافت حضوری " + amount.ToString("N0") + " از صندوق/بانک";
                UpdateAppointment(companyCode, appointment);
            }
            return null;
        }

        public static long AverageCost(string companyCode, int productId, DbTx tx = null) =>
            AverageInboundCost(companyCode, "ProductId", productId, tx);

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
            var subtotal = invoice.Subtotal;
            var net = invoice.NetAmount;
            var lines = new List<StoreJournalLine>();
            long allocated = 0;
            for (var i = 0; i < invoice.Lines.Count; i++)
            {
                var line = invoice.Lines[i];
                var code = line.Kind == PurchaseLineKind.Expense ? AccountCodes.Expense
                    : line.Kind == PurchaseLineKind.Asset ? AccountCodes.FixedAssets
                    : AccountCodes.Inventory;
                long share;
                if (i == invoice.Lines.Count - 1) share = Math.Max(0, net - allocated);
                else if (subtotal <= 0) share = 0;
                else
                {
                    share = (long)Math.Round((decimal)line.Amount * net / subtotal, MidpointRounding.AwayFromZero);
                    allocated += share;
                }
                if (share > 0)
                    lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, code), Debit = share, Memo = line.Title });
            }
            if (invoice.VatAmount > 0)
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Vat), Debit = invoice.VatAmount, Memo = "مالیات ارزش افزوده" });
            lines.Add(new StoreJournalLine
            {
                AccountId = MoneyAccountId(companyCode, invoice.PayWith, invoice.BankId, false),
                Credit = invoice.Total,
                Memo = invoice.PartyName
            });
            TagParty(companyCode, lines, invoice.PartyId);
            return lines;
        }

        private static List<StoreJournalLine> SaleLines(string companyCode, StoreSalesInvoice invoice, DbTx tx = null)
        {
            var subtotal = invoice.Subtotal;
            var net = invoice.NetAmount;
            var kind = SiteKinds.Of(GetTenant(companyCode));
            var serviceKind = kind.ServiceBusiness;
            var serviceIncome = kind.IsBooking ? AccountCodes.Visit : AccountCodes.Tuition;
            long stockTotal = 0;
            long serviceTotal = 0;
            long cogs = 0;
            foreach (var line in invoice.Lines)
            {
                if (line.ProductId > 0 || line.GoodsId > 0) stockTotal += line.Amount;
                else serviceTotal += line.Amount;

                if (line.ProductId > 0)
                {
                    var product = tx != null
                        ? NikooDb.Get(NikooTables.Products, NikooDb.Key(companyCode, line.ProductId), tx)
                        : GetProduct(companyCode, line.ProductId);
                    if (product == null || product.IsDigital()) continue;
                    cogs += AverageCost(companyCode, line.ProductId, tx) * line.Qty;
                }
                else if (line.GoodsId > 0)
                {
                    cogs += AverageGoodsCost(companyCode, line.GoodsId, tx) * line.Qty;
                }
            }

            long stockNet = 0;
            long serviceNet = 0;
            if (subtotal > 0 && net > 0)
            {
                stockNet = (long)Math.Round((decimal)stockTotal * net / subtotal, MidpointRounding.AwayFromZero);
                if (stockNet > net) stockNet = net;
                serviceNet = net - stockNet;
            }

            var lines = new List<StoreJournalLine>
            {
                new StoreJournalLine { AccountId = MoneyAccountId(companyCode, invoice.PayWith, invoice.BankId, true), Debit = invoice.Total, Memo = invoice.PartyName }
            };
            // Booking: a service line is visit income. Campus: it stays on tuition. Stocked lines stay on Sales.
            if (serviceKind)
            {
                if (stockNet > 0)
                    lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Sales), Credit = stockNet, Memo = invoice.PartyName });
                if (serviceNet > 0)
                    lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, serviceIncome), Credit = serviceNet, Memo = invoice.PartyName });
            }
            else if (net > 0)
            {
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Sales), Credit = net, Memo = invoice.PartyName });
            }
            if (invoice.VatAmount > 0)
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Vat), Credit = invoice.VatAmount, Memo = "مالیات ارزش افزوده" });
            if (cogs > 0)
            {
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Cogs), Debit = cogs, Memo = "بهای تمام‌شده" });
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Inventory), Credit = cogs, Memo = "بهای تمام‌شده" });
            }
            TagParty(companyCode, lines, invoice.PartyId);
            return lines;
        }

        /// <summary>Clamps discount and VAT percent, then fills VatAmount and Total.</summary>
        private static void ApplyInvoiceTax(StoreSalesInvoice invoice)
        {
            var subtotal = invoice.Subtotal;
            if (invoice.DiscountAmount < 0) invoice.DiscountAmount = 0;
            if (invoice.DiscountAmount > subtotal) invoice.DiscountAmount = subtotal;
            if (invoice.VatPercent < 0) invoice.VatPercent = 0;
            if (invoice.VatPercent > 100) invoice.VatPercent = 100;
            var net = subtotal - invoice.DiscountAmount;
            invoice.VatAmount = (long)Math.Round(net * invoice.VatPercent / 100.0, MidpointRounding.AwayFromZero);
            invoice.Total = net + invoice.VatAmount;
        }

        private static void ApplyInvoiceTax(StorePurchaseInvoice invoice)
        {
            var subtotal = invoice.Subtotal;
            if (invoice.DiscountAmount < 0) invoice.DiscountAmount = 0;
            if (invoice.DiscountAmount > subtotal) invoice.DiscountAmount = subtotal;
            if (invoice.VatPercent < 0) invoice.VatPercent = 0;
            if (invoice.VatPercent > 100) invoice.VatPercent = 100;
            var net = subtotal - invoice.DiscountAmount;
            invoice.VatAmount = (long)Math.Round(net * invoice.VatPercent / 100.0, MidpointRounding.AwayFromZero);
            invoice.Total = net + invoice.VatAmount;
        }

        private static string PreparePurchase(string companyCode, StorePurchaseInvoice invoice)
        {
            EnsureAccounts(companyCode);
            var party = GetParty(companyCode, invoice.PartyId);
            if (party == null) return "تامین‌کننده را از فهرست انتخاب کنید.";
            if (!party.Supplies) return "«" + party.Name + "» در فهرست تامین‌کنندگان نیست.";
            invoice.PartyName = party.Name;
            PartyDetail(companyCode, party);
            var bankId = invoice.BankId;
            var bankError = ResolveBank(companyCode, invoice.PayWith, ref bankId);
            if (bankError != null) return bankError;
            invoice.BankId = bankId;
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
            ApplyInvoiceTax(invoice);
            return null;
        }

        private static string PrepareSale(string companyCode, StoreSalesInvoice invoice)
        {
            EnsureAccounts(companyCode);
            var party = GetParty(companyCode, invoice.PartyId);
            if (party == null) return "مشتری را از فهرست انتخاب کنید.";
            if (!party.Buys) return "«" + party.Name + "» در فهرست مشتریان نیست.";
            invoice.PartyName = party.Name;
            PartyDetail(companyCode, party);
            var bankId = invoice.BankId;
            var bankError = ResolveBank(companyCode, invoice.PayWith, ref bankId);
            if (bankError != null) return bankError;
            invoice.BankId = bankId;
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
            ApplyInvoiceTax(invoice);
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
                var cost = line.ProductId > 0
                    ? AverageCost(companyCode, line.ProductId, tx)
                    : AverageGoodsCost(companyCode, line.GoodsId, tx);
                if (line.ProductId > 0)
                {
                    var productKey = NikooDb.Key(companyCode, line.ProductId);
                    NikooDb.Increment(NikooTables.Products, productKey, "Stock", -sign * line.Qty, tx);
                    if (sign > 0)
                        NikooDb.Increment(NikooTables.Products, productKey, "SoldCount", line.Qty, tx);
                    else
                        NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.Products.Name)
                            + " SET [SoldCount] = CASE WHEN [SoldCount] - @Amount < 0 THEN 0 ELSE [SoldCount] - @Amount END"
                            + " WHERE [CompanyCode] = @CompanyCode AND [Id] = @Id",
                            new Row(productKey) { ["Amount"] = line.Qty }, tx);
                }
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
                Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-", tx),
                EntryDate = DateTime.Today,
                Description = note + " — " + title,
                Source = JournalSource.Adjust,
                SourceId = moveId,
                DocumentTypeId = TypeId(companyCode, DocumentTypeCodes.Adjust),
                Lines = (qty > 0 ? Pair(inventory, adjust, amount, title) : Pair(adjust, inventory, amount, title)).ToList()
            }, tx);
        }

        public static long AverageGoodsCost(string companyCode, int goodsId, DbTx tx = null) =>
            AverageInboundCost(companyCode, "GoodsId", goodsId, tx);

        private static long AverageInboundCost(string companyCode, string column, int id, DbTx tx)
        {
            var totals = NikooDb.Select(
                "SELECT ISNULL(SUM(CAST([Qty] AS BIGINT)), 0), ISNULL(SUM(CAST([UnitCost] AS BIGINT) * [Qty]), 0) FROM "
                + NikooDb.Table(NikooTables.StockMoves.Name)
                + " WHERE [CompanyCode] = @CompanyCode AND " + NikooDb.Name(column) + " = @Id AND [Qty] > 0 AND [UnitCost] > 0",
                new Row { ["CompanyCode"] = companyCode, ["Id"] = id },
                r => new[] { Convert.ToInt64(r.GetValue(0)), Convert.ToInt64(r.GetValue(1)) }, tx);
            var row = totals.Count == 0 ? null : totals[0];
            return row == null || row[0] <= 0 ? 0 : row[1] / row[0];
        }

        private static List<StoreStockMove> MovesFor(string companyCode, string kind, int refId, DbTx tx = null) =>
            NikooDb.Query(NikooTables.StockMoves, new Row { ["CompanyCode"] = companyCode, ["Kind"] = kind, ["RefId"] = refId }, tx: tx);

        private static void DeleteMoves(string companyCode, string kind, int refId, DbTx tx)
        {
            foreach (var move in MovesFor(companyCode, kind, refId, tx))
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
            NikooDb.Query(NikooTables.JournalEntries, new Row
            {
                ["CompanyCode"] = companyCode,
                ["Source"] = source.ToString(),
                ["SourceId"] = sourceId,
                ["Reversed"] = false
            }, "[EntryDate] DESC, [Id] DESC").FirstOrDefault();

        private static bool JournalLineExists(string companyCode, string column, int id)
        {
            if (id <= 0) return false;
            var found = NikooDb.Column<int>(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM " + NikooDb.Table("JournalLines")
                + " WHERE [CompanyCode] = @CompanyCode AND " + NikooDb.Name(column) + " = @Id) THEN 1 ELSE 0 END",
                new Row { ["CompanyCode"] = companyCode, ["Id"] = id });
            return found.Count > 0 && found[0] == 1;
        }

        /// <summary>Next <paramref name="prefix"/> number (SN-0007) from the column itself, without loading the rows.</summary>
        private static string NextSerial(TableMap map, string companyCode, string column, string prefix, DbTx tx = null)
        {
            var max = NikooDb.Column<int>(
                "SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(" + NikooDb.Name(column) + ", @Start, 20) AS INT)), 0) FROM "
                + NikooDb.Table(map.Name) + " WITH (UPDLOCK, HOLDLOCK) WHERE [CompanyCode] = @CompanyCode AND "
                + NikooDb.Name(column) + " LIKE @Prefix",
                new Row { ["Start"] = prefix.Length + 1, ["CompanyCode"] = companyCode, ["Prefix"] = prefix + "%" }, tx);
            var n = max.Count == 0 ? 0 : max[0];
            return prefix + (n + 1).ToString("0000", CultureInfo.InvariantCulture);
        }

        private static List<StoreJournalLine> CleanLines(IEnumerable<StoreJournalLine> lines) =>
            (lines ?? Enumerable.Empty<StoreJournalLine>()).Where(l => l.AccountId > 0 && (l.Debit > 0 || l.Credit > 0)).ToList();

        /// <summary>
        /// Checks a manual entry: two or more lines, each on an active subsidiary account with the detail accounts that
        /// account asks for, and equal debit and credit. Detail accounts on an account that takes none are dropped.
        /// </summary>
        private static string Balanced(string companyCode, List<StoreJournalLine> lines)
        {
            if (lines.Count < 2) return "سند حداقل دو ردیف دارد.";
            var accounts = GetAccounts(companyCode).ToDictionary(a => a.Id);
            var details = GetDetails(companyCode).ToDictionary(d => d.Id);
            long debit = 0, credit = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var row = "ردیف " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": ";
                if (line.Debit > 0 && line.Credit > 0) return row + "یک ردیف هم‌زمان بدهکار و بستانکار نیست.";
                if (line.Debit < 0 || line.Credit < 0) return row + "مبلغ منفی نیست.";
                StoreLedgerAccount account;
                if (!accounts.TryGetValue(line.AccountId, out account)) return row + "حساب معین را انتخاب کنید.";
                if (!account.IsPosting)
                    return row + "سند فقط به حساب معین می‌خورد؛ «" + account.Title + "» حساب " + AccountingText.Level(account.Level) + " است.";
                if (!account.IsActive) return row + "حساب «" + account.Title + "» غیرفعال است.";
                var error = CheckDetail(line.Detail1Id, 1, account.Detail1, account, details, row);
                if (error != null) return error;
                error = CheckDetail(line.Detail2Id, 2, account.Detail2, account, details, row);
                if (error != null) return error;
                if (account.Detail1 == DetailUse.None) line.Detail1Id = 0;
                if (account.Detail2 == DetailUse.None) line.Detail2Id = 0;
                debit += line.Debit;
                credit += line.Credit;
            }
            if (debit != credit || debit == 0) return "جمع بدهکار و بستانکار باید برابر و بیشتر از صفر باشد.";
            return null;
        }

        private static string CheckDetail(int detailId, int level, DetailUse use, StoreLedgerAccount account,
            Dictionary<int, StoreDetailAccount> details, string row)
        {
            var label = AccountingText.DetailLevel(level);
            if (use == DetailUse.None || detailId <= 0)
                return use == DetailUse.Required && detailId <= 0 ? row + "حساب «" + account.Title + "» " + label + " لازم دارد." : null;
            StoreDetailAccount detail;
            if (!details.TryGetValue(detailId, out detail) || detail.Level != level) return row + label + " انتخاب‌شده پیدا نشد.";
            if (!detail.IsActive) return row + "تفصیلی «" + detail.Title + "» غیرفعال است.";
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

        public static List<StoreDocumentType> GetDocumentTypes(string companyCode)
        {
            EnsureDocumentTypes(companyCode);
            return All(NikooTables.DocumentTypes, companyCode, "[Code], [Id]");
        }

        public static StoreDocumentType GetDocumentType(string companyCode, int id) => Find(NikooTables.DocumentTypes, companyCode, id);

        public static string SaveDocumentType(string companyCode, StoreDocumentType item)
        {
            item.Title = (item.Title ?? "").Trim();
            item.Code = string.IsNullOrWhiteSpace(item.Code) ? null : item.Code.Trim();
            if (item.Title.Length == 0) return "عنوان نوع سند را بنویسید.";
            var all = GetDocumentTypes(companyCode);
            var existing = item.Id > 0 ? all.FirstOrDefault(t => t.Id == item.Id) : null;
            if (existing != null && existing.IsSystem)
            {
                item.Code = existing.Code;
                item.IsSystem = true;
            }
            if (string.IsNullOrEmpty(item.Code))
                item.Code = NextNumber(all.Select(t => t.Code), "T");
            if (all.Any(t => t.Id != item.Id && string.Equals(t.Code, item.Code, StringComparison.OrdinalIgnoreCase)))
                return "این کد نوع سند قبلاً استفاده شده است.";
            Save(NikooTables.DocumentTypes, companyCode, item);
            return null;
        }

        public static string DeleteDocumentType(string companyCode, int id)
        {
            var item = GetDocumentType(companyCode, id);
            if (item == null) return null;
            if (item.IsSystem) return "نوع‌های پیش‌فرض حذف نمی‌شوند.";
            if (NikooDb.Count(NikooTables.JournalEntries, SqlFilter.ForStore(companyCode).Eq("DocumentTypeId", id)) > 0)
                return "این نوع در سندها آمده و حذف نمی‌شود.";
            if (NikooDb.Count(NikooTables.CashTransactions, SqlFilter.ForStore(companyCode).Eq("DocumentTypeId", id)) > 0)
                return "این نوع در دریافت و پرداخت آمده و حذف نمی‌شود.";
            Remove(NikooTables.DocumentTypes, companyCode, id);
            return null;
        }

        public static List<StoreCashTransaction> GetCash(string companyCode) =>
            All(NikooTables.CashTransactions, companyCode, "[TxDate] DESC, [Id] DESC");

        public static StoreCashTransaction GetCashItem(string companyCode, int id) => Find(NikooTables.CashTransactions, companyCode, id);

        public static string SaveCash(string companyCode, StoreCashTransaction item)
        {
            var fiscalError = CheckFiscalDate(companyCode, item.TxDate);
            if (fiscalError != null) return fiscalError;
            EnsureAccounts(companyCode);
            item.PartyName = (item.PartyName ?? "").Trim();
            item.Note = (item.Note ?? "").Trim();
            if (item.Amount <= 0) return "مبلغ باید بیشتر از صفر باشد.";
            if (item.PayWith == PayWith.Credit) return "دریافت و پرداخت نقدی یا بانکی است. نسیه را با فاکتور ثبت کنید.";
            var account = GetAccount(companyCode, item.AccountId);
            if (account == null || !account.IsActive || !account.IsPosting) return "حساب معین مقابل را انتخاب کنید.";
            if (item.PartyId <= 0 && account.Detail1 == DetailUse.Required && item.PartyName.Length > 0)
            {
                var named = PartyByName(companyCode, item.PartyName, item.Direction == CashDirection.In ? PartyKind.Customer : PartyKind.Supplier);
                if (named != null) item.PartyId = named.Id;
            }
            if (item.PartyId > 0)
            {
                var party = GetParty(companyCode, item.PartyId);
                if (party == null) return "طرف حساب انتخاب‌شده پیدا نشد.";
                item.PartyName = party.Name;
                PartyDetail(companyCode, party);
            }
            if (item.PartyName.Length == 0) return "طرف حساب را از فهرست انتخاب کنید یا نامش را بنویسید.";
            if (account.Detail1 == DetailUse.Required && item.PartyId <= 0)
                return "حساب «" + account.Title + "» تفصیلی شخص لازم دارد؛ طرف حساب را از فهرست انتخاب کنید.";
            if (account.Detail2 == DetailUse.None) item.Detail2Id = 0;
            var detail2Error = CheckDetail(item.Detail2Id, 2, account.Detail2, account, GetDetails(companyCode).ToDictionary(d => d.Id), "");
            if (detail2Error != null) return detail2Error;
            var bankId = item.BankId;
            var bankError = ResolveBank(companyCode, item.PayWith, ref bankId);
            if (bankError != null) return bankError;
            item.BankId = bankId;
            var money = GetAccount(companyCode, MoneyAccountId(companyCode, item.PayWith, item.BankId, item.Direction == CashDirection.In));
            if (money == null) return "سرفصل صندوق یا بانک پیدا نشد.";
            if (money.Id == account.Id) return "حساب مقابل نباید همان صندوق یا بانک باشد.";
            var debit = item.Direction == CashDirection.In ? money.Id : account.Id;
            var credit = item.Direction == CashDirection.In ? account.Id : money.Id;
            var cashLines = Pair(debit, credit, item.Amount, item.PartyName).ToList();
            foreach (var line in cashLines.Where(l => l.AccountId == account.Id)) line.Detail2Id = item.Detail2Id;
            TagParty(companyCode, cashLines, item.PartyId);
            var types = GetDocumentTypes(companyCode);
            var chosen = types.FirstOrDefault(t => t.Id == item.DocumentTypeId && t.IsActive);
            if (chosen == null)
                chosen = types.First(t => t.Code == (item.Direction == CashDirection.Out ? DocumentTypeCodes.Payment : DocumentTypeCodes.Receipt));
            item.DocumentTypeId = chosen.Id;
            var source = item.Direction == CashDirection.In ? JournalSource.Receipt : JournalSource.Payment;
            var prefix = item.Direction == CashDirection.In ? "DR-" : "PD-";
            var description = AccountingText.Direction(item.Direction) + " " + item.PartyName;
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var stored = item.Id > 0 ? NikooDb.Get(NikooTables.CashTransactions, NikooDb.Key(companyCode, item.Id), tx) : null;
                if (stored != null)
                {
                    DeleteEntry(companyCode, stored.EntryId, tx);
                    item.Number = stored.Number;
                }
                else
                {
                    item.Id = NikooDb.NextId(NikooTables.CashTransactions, companyCode, tx);
                    item.Number = NextSerial(NikooTables.CashTransactions, companyCode, "Number", prefix, tx);
                }
                item.EntryId = WriteEntry(companyCode, new StoreJournalEntry
                {
                    Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-", tx),
                    EntryDate = item.TxDate,
                    Description = description,
                    Source = source,
                    SourceId = item.Id,
                    DocumentTypeId = item.DocumentTypeId,
                    Lines = cashLines
                }, tx);
                NikooDb.Upsert(NikooTables.CashTransactions, item, companyCode, tx);
                tx.Commit();
                return null;
            }
        }

        public static string DeleteCash(string companyCode, int id)
        {
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var stored = NikooDb.Get(NikooTables.CashTransactions, NikooDb.Key(companyCode, id), tx);
                if (stored == null) return null;
                DeleteEntry(companyCode, stored.EntryId, tx);
                NikooDb.Delete(NikooTables.CashTransactions, NikooDb.Key(companyCode, id), tx);
                tx.Commit();
                return null;
            }
        }

        private static int TypeId(string companyCode, string code)
        {
            var type = GetDocumentTypes(companyCode).FirstOrDefault(t => t.Code == code);
            return type == null ? 0 : type.Id;
        }

        private static readonly StoreDocumentType[] SystemDocumentTypes =
        {
            Doc(DocumentTypeCodes.Manual, "سند دستی"),
            Doc(DocumentTypeCodes.Purchase, "فاکتور خرید"),
            Doc(DocumentTypeCodes.Sale, "فاکتور فروش"),
            Doc(DocumentTypeCodes.Online, "فروش آنلاین"),
            Doc(DocumentTypeCodes.Tuition, "شهریه و ثبت‌نام"),
            Doc(DocumentTypeCodes.Facility, "رزرو سالن"),
            Doc(DocumentTypeCodes.Appointment, "نوبت و ویزیت"),
            Doc(DocumentTypeCodes.Adjust, "تعدیل انبار"),
            Doc(DocumentTypeCodes.Receipt, "دریافت"),
            Doc(DocumentTypeCodes.Payment, "پرداخت"),
            Doc(DocumentTypeCodes.Return, "مرجوعی کالا"),
            Doc(DocumentTypeCodes.Freight, "هزینه حمل"),
            Doc(DocumentTypeCodes.Closing, "بستن سال مالی")
        };

        private static void EnsureDocumentTypes(string companyCode)
        {
            var existing = All(NikooTables.DocumentTypes, companyCode);
            if (existing.Count == 0)
            {
                foreach (var row in SystemDocumentTypes) Save(NikooTables.DocumentTypes, companyCode, row);
                return;
            }
            var codes = new HashSet<string>(existing.Select(t => t.Code), StringComparer.OrdinalIgnoreCase);
            foreach (var row in SystemDocumentTypes)
            {
                if (codes.Contains(row.Code)) continue;
                Save(NikooTables.DocumentTypes, companyCode, new StoreDocumentType
                {
                    Code = row.Code,
                    Title = row.Title,
                    IsSystem = true,
                    IsActive = true
                });
            }
        }

        private static StoreDocumentType Doc(string code, string title) =>
            new StoreDocumentType { Code = code, Title = title, IsSystem = true, IsActive = true };

        /// <summary>Rejects dates in a closed fiscal year, or in a future year when locking is on.</summary>
        public static string CheckFiscalDate(string companyCode, DateTime date)
        {
            var settings = GetSettings(companyCode);
            if (settings == null || !settings.FiscalLockDates) return null;
            var period = FiscalPeriod.For(date, settings.FiscalStartMonth);
            if (FiscalPeriod.IsClosed(settings, period.StartYear))
                return "سال مالی " + period.StartYear.ToString(CultureInfo.InvariantCulture).ToFaDigits()
                    + " بسته شده است؛ تاریخ را در سال باز بگذارید یا از صفحه سال مالی آن را باز کنید.";
            var current = FiscalPeriod.Current(settings);
            if (period.StartYear > current.StartYear)
                return "تاریخ در سال مالی آینده («" + period.Label + "») ثبت نمی‌شود.";
            return null;
        }

        /// <summary>
        /// Closes a finished fiscal year: zeros income/expense subsidiaries into retained earnings and locks that year.
        /// </summary>
        public static string CloseFiscalYear(string companyCode, int startYear)
        {
            var settings = GetSettings(companyCode);
            if (settings == null) return "تنظیمات پیدا نشد.";
            settings.FiscalStartMonth = FiscalPeriod.NormalizeMonth(settings.FiscalStartMonth);
            if (FiscalPeriod.IsClosed(settings, startYear))
                return "این سال مالی قبلاً بسته شده است.";
            var current = FiscalPeriod.Current(settings);
            if (startYear >= current.StartYear)
                return "فقط سال مالی تمام‌شده را می‌شود بست؛ سال جاری هنوز باز است.";
            var period = FiscalPeriod.OfYear(startYear, settings.FiscalStartMonth);
            EnsureAccounts(companyCode);
            EnsureDocumentTypes(companyCode);
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var retained = IdOf(companyCode, AccountCodes.RetainedEarnings);
                if (retained <= 0) return "سرفصل «سود و زیان انباشته» پیدا نشد.";
                var accounts = NikooDb.List(NikooTables.LedgerAccounts, companyCode, tx: tx)
                    .Where(a => a.IsPosting && (a.Kind == AccountKind.Income || a.Kind == AccountKind.Expense))
                    .ToDictionary(a => a.Id);
                var summed = NikooDb.Pairs<int, long>(
                    "SELECT l.[AccountId], SUM(l.[Debit] - l.[Credit]) FROM " + NikooDb.Table("JournalLines")
                    + " l INNER JOIN " + NikooDb.Table(NikooTables.JournalEntries.Name)
                    + " e ON e.[CompanyCode] = l.[CompanyCode] AND e.[Id] = l.[EntryId]"
                    + " WHERE e.[CompanyCode] = @CompanyCode AND e.[EntryDate] >= @Start AND e.[EntryDate] <= @End"
                    + " GROUP BY l.[AccountId]",
                    new Row { ["CompanyCode"] = companyCode, ["Start"] = period.Start, ["End"] = period.End }, tx);
                var nets = new Dictionary<int, long>();
                foreach (var pair in summed)
                    if (accounts.ContainsKey(pair.Key) && pair.Value != 0) nets[pair.Key] = pair.Value;
                var lines = new List<StoreJournalLine>();
                long retainedDebit = 0, retainedCredit = 0;
                foreach (var pair in nets.Where(p => p.Value != 0).OrderBy(p => accounts[p.Key].Code))
                {
                    var account = accounts[pair.Key];
                    var net = pair.Value;
                    if (net > 0)
                    {
                        lines.Add(new StoreJournalLine { AccountId = account.Id, Credit = net, Memo = "بستن " + account.Title });
                        retainedDebit += net;
                    }
                    else
                    {
                        var creditSide = -net;
                        lines.Add(new StoreJournalLine { AccountId = account.Id, Debit = creditSide, Memo = "بستن " + account.Title });
                        retainedCredit += creditSide;
                    }
                }
                if (lines.Count == 0)
                {
                    FiscalPeriod.MarkClosed(settings, startYear);
                    WriteSettings(settings, tx);
                    tx.Commit();
                    return null;
                }
                var plug = retainedDebit - retainedCredit;
                if (plug > 0)
                    lines.Add(new StoreJournalLine { AccountId = retained, Debit = plug, Memo = "سود و زیان سال " + startYear.ToString(CultureInfo.InvariantCulture) });
                else if (plug < 0)
                    lines.Add(new StoreJournalLine { AccountId = retained, Credit = -plug, Memo = "سود و زیان سال " + startYear.ToString(CultureInfo.InvariantCulture) });
                WriteEntry(companyCode, new StoreJournalEntry
                {
                    Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-", tx),
                    EntryDate = period.End,
                    Description = "بستن سال مالی " + startYear.ToString(CultureInfo.InvariantCulture),
                    Source = JournalSource.Closing,
                    SourceId = startYear,
                    DocumentTypeId = TypeId(companyCode, DocumentTypeCodes.Closing),
                    Lines = lines
                }, tx);
                FiscalPeriod.MarkClosed(settings, startYear);
                WriteSettings(settings, tx);
                tx.Commit();
                return null;
            }
        }

        /// <summary>Unlocks a closed year and removes its closing journal entry when present.</summary>
        public static string ReopenFiscalYear(string companyCode, int startYear)
        {
            var settings = GetSettings(companyCode);
            if (settings == null) return "تنظیمات پیدا نشد.";
            if (!FiscalPeriod.IsClosed(settings, startYear)) return "این سال مالی بسته نیست.";
            lock (LockFor(companyCode))
            using (var tx = NikooDb.Begin())
            {
                var closing = NikooDb.Query(NikooTables.JournalEntries, new Row
                {
                    ["CompanyCode"] = companyCode,
                    ["Source"] = JournalSource.Closing.ToString(),
                    ["SourceId"] = startYear
                }, "[EntryDate] DESC, [Id] DESC", tx).FirstOrDefault();
                if (closing != null) DeleteEntry(companyCode, closing.Id, tx);
                FiscalPeriod.MarkOpen(settings, startYear);
                WriteSettings(settings, tx);
                tx.Commit();
                return null;
            }
        }
    }
}
