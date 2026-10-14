using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>Chart of accounts, journal, invoices, stock and fixed assets for a shop or a sports club.</summary>
    public partial class ShopAdminController
    {
        private const int AccountLineCount = 8;

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Accounts(AccountGridQuery query)
        {
            var page = TenantStore.AccountGrid(CompanyCode, query);
            ViewBag.ById = TenantStore.GetAccounts(CompanyCode).ToDictionary(a => a.Id);
            return View(page);
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditAccount(int? id, AccountLevel? level, int? parent)
        {
            var accounts = TenantStore.GetAccounts(CompanyCode);
            var item = id.HasValue ? accounts.FirstOrDefault(a => a.Id == id.Value)
                : new StoreLedgerAccount { IsActive = true, Level = level ?? AccountLevel.Subsidiary, ParentId = parent ?? 0 };
            if (item == null) return ToAction("Accounts");
            ViewBag.Accounts = accounts;
            ViewBag.Locked = item.IsSystem || (item.Id > 0 && accounts.Any(a => a.ParentId == item.Id));
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditAccount(StoreLedgerAccount model)
        {
            model.Code = Clip(model.Code, 20);
            model.Title = Clip(model.Title, 120);
            var error = TenantStore.SaveAccount(CompanyCode, model);
            if (error != null)
                return Fail(error, "EditAccount", model.Id > 0 ? (object)new { id = model.Id } : new { level = model.Level, parent = model.ParentId });
            return Ok("حساب " + AccountingText.Level(model.Level) + " «" + model.Title + "» با کد " + model.Code + " ذخیره شد.", "Accounts");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeleteAccount(int id)
        {
            var error = TenantStore.DeleteAccount(CompanyCode, id);
            if (error != null) Fail(error);
            else Ok("حساب حذف شد.");
            return ToAction("Accounts");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DetailAccounts(DetailGridQuery query)
        {
            ViewBag.Balances = TenantStore.DetailBalances(CompanyCode);
            ViewBag.PartyNames = TenantStore.GetParties(CompanyCode).ToDictionary(p => p.Id, p => p.Name);
            return View(TenantStore.DetailGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditDetail(int? id, int? level)
        {
            var item = id.HasValue ? TenantStore.GetDetail(CompanyCode, id.Value)
                : new StoreDetailAccount { IsActive = true, Level = level == 2 ? 2 : 1, Kind = level == 2 ? DetailKind.CostCenter : DetailKind.Person };
            if (item == null) return ToAction("DetailAccounts");
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditDetail(StoreDetailAccount model)
        {
            model.Code = Clip(model.Code, 20);
            model.Title = Clip(model.Title, 150);
            model.Note = Clip(model.Note, 300);
            var error = TenantStore.SaveDetail(CompanyCode, model);
            if (error != null) return Fail(error, "EditDetail", model.Id > 0 ? (object)new { id = model.Id } : new { level = model.Level });
            return Ok("حساب تفصیلی «" + model.Title + "» با کد " + model.Code + " ذخیره شد.", "DetailAccounts");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeleteDetail(int id)
        {
            var error = TenantStore.DeleteDetail(CompanyCode, id);
            if (error != null) Fail(error);
            else Ok("حساب تفصیلی حذف شد.");
            return ToAction("DetailAccounts");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Voucher(int id)
        {
            var page = TenantStore.Voucher(CompanyCode, id);
            if (page == null) return Fail("سند پیدا نشد.", "Journal");
            return View(page);
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Journal(JournalGridQuery query)
        {
            var page = TenantStore.JournalGrid(CompanyCode, query);
            ViewBag.Types = TenantStore.GetDocumentTypes(CompanyCode);
            return View(page);
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditJournal(int? id)
        {
            var item = id.HasValue ? TenantStore.GetEntry(CompanyCode, id.Value) : new StoreJournalEntry { EntryDate = DateTime.Today };
            if (item == null) return ToAction("Journal");
            if (item.Id > 0 && item.Source != JournalSource.Manual)
                return Fail("سندهای خودکار از فاکتور، سفارش یا تعدیل انبار اصلاح می‌شوند.", "Journal");
            var lines = item.Lines ?? new List<StoreJournalLine>();
            var accounts = TenantStore.GetAccounts(CompanyCode);
            ViewBag.Accounts = accounts.Where(a => a.IsPosting && (a.IsActive || lines.Any(l => l.AccountId == a.Id))).ToList();
            ViewBag.Generals = accounts.Where(a => a.Level == AccountLevel.General).ToDictionary(a => a.Id);
            ViewBag.Details = TenantStore.GetDetails(CompanyCode)
                .Where(d => d.IsActive || lines.Any(l => l.Detail1Id == d.Id || l.Detail2Id == d.Id)).ToList();
            ViewBag.Types = TenantStore.GetDocumentTypes(CompanyCode).Where(t => t.IsActive || t.Id == item.DocumentTypeId).ToList();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditJournal(StoreJournalEntry model, string entryDate)
        {
            model.EntryDate = ReadDate(entryDate);
            model.Description = Clip(model.Description, 300);
            model.Lines = ReadJournalLines();
            var error = TenantStore.SaveManualEntry(CompanyCode, model);
            if (error != null) return Fail(error, "EditJournal", model.Id > 0 ? new { id = model.Id } : null);
            return Ok("سند ثبت شد.", "Journal");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeleteJournal(int id)
        {
            var error = TenantStore.DeleteManualEntry(CompanyCode, id);
            if (error != null) Fail(error);
            else Ok("سند حذف شد.");
            return ToAction("Journal");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Purchases(InvoiceGridQuery query)
        {
            ViewBag.BankTitles = BankTitles();
            return View(TenantStore.PurchaseGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditPurchase(int? id)
        {
            var item = id.HasValue ? TenantStore.GetPurchase(CompanyCode, id.Value) : new StorePurchaseInvoice { InvoiceDate = DateTime.Today };
            if (item == null) return ToAction("Purchases");
            StockBags();
            BankBag(item.BankId);
            ViewBag.Parties = TenantStore.GetParties(CompanyCode).Where(p => p.Supplies && (p.IsActive || p.Id == item.PartyId)).ToList();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditPurchase(StorePurchaseInvoice model, string invoiceDate, string newParty, string payFrom)
        {
            model.InvoiceDate = ReadDate(invoiceDate);
            model.PayWith = ReadPay(payFrom, out var bankId);
            model.BankId = bankId;
            if (model.PartyId <= 0) model.PartyId = NewParty(newParty, PartyKind.Supplier);
            model.Note = Clip(model.Note, 500);
            model.Lines = ReadPurchaseLines();
            var error = TenantStore.SavePurchase(CompanyCode, model);
            if (error != null) return Fail(error, "EditPurchase", model.Id > 0 ? new { id = model.Id } : null);
            return Ok("فاکتور خرید ثبت شد و موجودی و سند حسابداری به‌روز شد.", "Purchases");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeletePurchase(int id)
        {
            var error = TenantStore.DeletePurchase(CompanyCode, id);
            if (error != null) Fail(error);
            else Ok("فاکتور خرید حذف شد و موجودی برگشت.");
            return ToAction("Purchases");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult SaleInvoices(InvoiceGridQuery query)
        {
            ViewBag.BankTitles = BankTitles();
            return View(TenantStore.SaleGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditSaleInvoice(int? id)
        {
            var item = id.HasValue ? TenantStore.GetSale(CompanyCode, id.Value) : new StoreSalesInvoice { InvoiceDate = DateTime.Today };
            if (item == null) return ToAction("SaleInvoices");
            StockBags();
            BankBag(item.BankId);
            ViewBag.Parties = TenantStore.GetParties(CompanyCode).Where(p => p.Buys && (p.IsActive || p.Id == item.PartyId)).ToList();
            ViewBag.SiteCustomers = TenantStore.CustomersWithoutParty(CompanyCode);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditSaleInvoice(StoreSalesInvoice model, string invoiceDate, string party, string newParty, string payFrom)
        {
            model.InvoiceDate = ReadDate(invoiceDate);
            model.PayWith = ReadPay(payFrom, out var bankId);
            model.BankId = bankId;
            model.PartyId = ReadParty(party);
            if (model.PartyId <= 0) model.PartyId = NewParty(newParty, PartyKind.Customer);
            model.Note = Clip(model.Note, 500);
            model.Lines = ReadSaleLines();
            var error = TenantStore.SaveSale(CompanyCode, model);
            if (error != null) return Fail(error, "EditSaleInvoice", model.Id > 0 ? new { id = model.Id } : null);
            return Ok("فاکتور فروش ثبت شد.", "SaleInvoices");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeleteSaleInvoice(int id)
        {
            var error = TenantStore.DeleteSale(CompanyCode, id);
            if (error != null) Fail(error);
            else Ok("فاکتور فروش حذف شد و موجودی برگشت.");
            return ToAction("SaleInvoices");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Inventory(InventoryGridQuery query)
        {
            var tab = query.Tab == "products" || query.Tab == "moves" ? query.Tab : "goods";
            ViewBag.Tab = tab;
            if (tab == "products")
            {
                ViewBag.Stock = TenantStore.StockGrid(CompanyCode, query);
            }
            else if (tab == "moves")
            {
                var moves = TenantStore.MoveGrid(CompanyCode, query);
                ViewBag.Moves = moves;
                ViewBag.MoveTitles = TenantStore.MoveTitles(CompanyCode, moves);
            }
            else
            {
                ViewBag.Goods = TenantStore.GoodsGrid(CompanyCode, query);
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult SaveGoods(StoreGoodsItem model)
        {
            model.Title = Clip(model.Title, 150);
            model.Unit = Clip(model.Unit, 20);
            model.Code = Clip(model.Code, 40);
            var error = TenantStore.SaveGoods(CompanyCode, model);
            if (error != null) Fail(error);
            else Ok("کالای انبار ذخیره شد. موجودی را با فاکتور خرید یا تعدیل عوض کنید.");
            return ToAction("Inventory");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult AdjustStock(int productId, int qty, string note)
        {
            var error = TenantStore.AdjustStock(CompanyCode, productId, qty, Clip(note, 200));
            if (error != null) Fail(error);
            else Ok("موجودی محصول تعدیل شد.");
            return ToAction("Inventory", new { tab = "products" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult AdjustGoods(int goodsId, int qty, string note)
        {
            var error = TenantStore.AdjustGoods(CompanyCode, goodsId, qty, Clip(note, 200));
            if (error != null) Fail(error);
            else Ok("موجودی کالا تعدیل شد.");
            return ToAction("Inventory");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Assets(GridQuery query)
        {
            return View(TenantStore.AssetGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditAsset(int? id)
        {
            var item = id.HasValue ? TenantStore.GetAsset(CompanyCode, id.Value) : new StoreFixedAsset { BoughtOn = DateTime.Today, IsActive = true };
            if (item == null) return ToAction("Assets");
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditAsset(StoreFixedAsset model, string boughtOn)
        {
            model.BoughtOn = ReadDate(boughtOn);
            model.Code = Clip(model.Code, 20);
            model.Title = Clip(model.Title, 150);
            model.Location = Clip(model.Location, 120);
            model.Note = Clip(model.Note, 500);
            var error = TenantStore.SaveAsset(CompanyCode, model);
            if (error != null) return Fail(error, "EditAsset", model.Id > 0 ? new { id = model.Id } : null);
            return Ok("مال ذخیره شد.", "Assets");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeleteAsset(int id)
        {
            TenantStore.DeleteAsset(CompanyCode, id);
            return Ok("مال از فهرست حذف شد. سند خرید آن سر جایش می‌ماند.", "Assets");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Books(BooksGridQuery query)
        {
            var page = new BooksPage { From = ReadOptionalDate(query.From), To = ReadOptionalDate(query.To) };
            var text = (query.Q ?? "").Trim();
            if (query.AccountId.HasValue) page.Account = TenantStore.GetAccount(CompanyCode, query.AccountId.Value);
            if (query.DetailId.HasValue) page.Detail = TenantStore.GetDetail(CompanyCode, query.DetailId.Value);

            if (page.Account == null && page.Detail == null)
            {
                page.Level = query.Level == "group" || query.Level == "subsidiary" || query.Level == "detail" ? query.Level : "general";
                if (query.Parent.HasValue) page.Parent = TenantStore.GetAccount(CompanyCode, query.Parent.Value);
                var totals = TenantStore.TrialBalance(CompanyCode, page.Level, page.Parent?.Id, page.From, page.To);
                page.Debit = totals.Sum(t => t.Debit);
                page.Credit = totals.Sum(t => t.Credit);
                page.BalanceDebit = totals.Where(t => t.Balance > 0).Sum(t => t.Balance);
                page.BalanceCredit = totals.Where(t => t.Balance < 0).Sum(t => -t.Balance);
                if (text.Length > 0)
                    totals = totals.Where(t => Has(t.Code, text) || Has(t.Title, text)).ToList();
                totals = Sorted(totals, query, "code", new Dictionary<string, Func<AccountBalance, object>>
                {
                    ["code"] = t => t.Code,
                    ["title"] = t => t.Title,
                    ["debit"] = t => t.Debit,
                    ["credit"] = t => t.Credit,
                    ["balance"] = t => t.Balance
                });
                page.Totals = PagedList<AccountBalance>.From(totals, query.Page, query.PageSize);
                return View(page);
            }

            var movements = TenantStore.Ledger(CompanyCode, page);
            if (text.Length > 0)
                movements = movements.Where(m => Has(m.Number, text) || Has(m.Description, text) || Has(m.Memo, text)
                    || Has(m.Account, text) || Has(m.Detail1, text) || Has(m.Detail2, text)).ToList();
            movements = Sorted(movements, query, "date", new Dictionary<string, Func<LedgerMovement, object>>
            {
                ["date"] = m => m.Date,
                ["number"] = m => m.Number,
                ["debit"] = m => m.Debit,
                ["credit"] = m => m.Credit
            });
            page.Movements = PagedList<LedgerMovement>.From(movements, query.Page, query.PageSize);
            return View(page);
        }

        private static DateTime? ReadOptionalDate(string text)
        {
            DateTime date;
            return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ? date.Date : (DateTime?)null;
        }

        private static bool Has(string value, string text) =>
            value != null && value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Orders an in-memory table like the SQL grids: the requested key and direction, or the default ascending.</summary>
        private static List<T> Sorted<T>(List<T> rows, GridQuery query, string defaultKey, Dictionary<string, Func<T, object>> keys)
        {
            Func<T, object> key;
            var desc = false;
            if (!string.IsNullOrEmpty(query.Sort) && keys.TryGetValue(query.Sort.ToLowerInvariant(), out key))
                desc = query.Descending;
            else
                key = keys[defaultKey];
            return (desc ? rows.OrderByDescending(key) : rows.OrderBy(key)).ToList();
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Parties(PartyGridQuery query)
        {
            return View(TenantStore.PartyGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditParty(int? id, PartyKind? kind)
        {
            var item = id.HasValue ? TenantStore.GetParty(CompanyCode, id.Value) : new StoreParty { Kind = kind ?? PartyKind.Supplier, IsActive = true };
            if (item == null) return ToAction("Parties");
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditParty(StoreParty model)
        {
            model.Code = Clip(model.Code, 40);
            model.Name = Clip(model.Name, 150);
            model.Mobile = Clip(model.Mobile, 20);
            model.Phone = Clip(model.Phone, 30);
            model.Address = Clip(model.Address, 300);
            model.Note = Clip(model.Note, 500);
            var error = TenantStore.SaveParty(CompanyCode, model);
            if (error != null) return Fail(error, "EditParty", model.Id > 0 ? new { id = model.Id } : null);
            return Ok("«" + model.Name + "» در فهرست تامین‌کنندگان و مشتریان ذخیره شد.", "Parties");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeleteParty(int id)
        {
            var error = TenantStore.DeleteParty(CompanyCode, id);
            if (error != null) Fail(error);
            else Ok("طرف حساب حذف شد.");
            return ToAction("Parties");
        }

        private int NewParty(string name, PartyKind kind)
        {
            var party = TenantStore.PartyByName(CompanyCode, Clip(name, 150), kind);
            return party == null ? 0 : party.Id;
        }

        /// <summary>«t-12» is a party; «c-7» is a site customer, added to the parties the first time it is invoiced.</summary>
        private int ReadParty(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return 0;
            int id;
            if (token.StartsWith("t-", StringComparison.Ordinal) && int.TryParse(token.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                return id;
            if (token.StartsWith("c-", StringComparison.Ordinal) && int.TryParse(token.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            {
                var party = TenantStore.PartyForCustomer(CompanyCode, id);
                return party == null ? 0 : party.Id;
            }
            return 0;
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult AccountingGuide()
        {
            return View();
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DocumentTypes(GridQuery query)
        {
            return View(TenantStore.DocumentTypeGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditDocumentType(int? id)
        {
            var item = id.HasValue ? TenantStore.GetDocumentType(CompanyCode, id.Value) : new StoreDocumentType { IsActive = true };
            if (item == null) return ToAction("DocumentTypes");
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditDocumentType(StoreDocumentType model)
        {
            model.Code = Clip(model.Code, 20);
            model.Title = Clip(model.Title, 80);
            var error = TenantStore.SaveDocumentType(CompanyCode, model);
            if (error != null) return Fail(error, "EditDocumentType", model.Id > 0 ? new { id = model.Id } : null);
            return Ok("نوع سند ذخیره شد.", "DocumentTypes");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeleteDocumentType(int id)
        {
            var error = TenantStore.DeleteDocumentType(CompanyCode, id);
            if (error != null) Fail(error);
            else Ok("نوع سند حذف شد.");
            return ToAction("DocumentTypes");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Payments(CashGridQuery query)
        {
            ViewBag.Types = TenantStore.GetDocumentTypes(CompanyCode);
            ViewBag.BankTitles = BankTitles();
            return View(TenantStore.CashGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditPayment(int? id)
        {
            var item = id.HasValue ? TenantStore.GetCashItem(CompanyCode, id.Value) : new StoreCashTransaction { TxDate = DateTime.Today, Direction = CashDirection.Out };
            if (item == null) return ToAction("Payments");
            ViewBag.Accounts = TenantStore.GetAccounts(CompanyCode).Where(a => a.IsPosting && (a.IsActive || a.Id == item.AccountId)).ToList();
            ViewBag.CostCenters = TenantStore.GetDetails(CompanyCode).Where(d => d.Level == 2 && (d.IsActive || d.Id == item.Detail2Id)).ToList();
            ViewBag.Types = TenantStore.GetDocumentTypes(CompanyCode).Where(t => t.IsActive || t.Id == item.DocumentTypeId).ToList();
            ViewBag.Parties = TenantStore.GetParties(CompanyCode).Where(p => p.IsActive || p.Id == item.PartyId).ToList();
            BankBag(item.BankId);
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditPayment(StoreCashTransaction model, string txDate, string payFrom)
        {
            model.TxDate = ReadDate(txDate);
            model.PayWith = ReadPay(payFrom, out var bankId);
            model.BankId = bankId;
            model.PartyName = Clip(model.PartyName, 120);
            model.Note = Clip(model.Note, 300);
            var error = TenantStore.SaveCash(CompanyCode, model);
            if (error != null) return Fail(error, "EditPayment", model.Id > 0 ? new { id = model.Id } : null);
            return Ok("تراکنش ثبت شد و سند حسابداری آن زده شد.", "Payments");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeletePayment(int id)
        {
            var error = TenantStore.DeleteCash(CompanyCode, id);
            if (error != null) Fail(error);
            else Ok("تراکنش حذف شد و سندش برگشت.");
            return ToAction("Payments");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Banks(BankGridQuery query)
        {
            var page = TenantStore.BankGrid(CompanyCode, query);
            ViewBag.Balances = TenantStore.BankBalances(CompanyCode, page);
            return View(page);
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditBank(int? id)
        {
            var item = id.HasValue ? TenantStore.GetBank(CompanyCode, id.Value) : new StoreBankAccount { IsActive = true };
            if (item == null) return ToAction("Banks");
            ViewBag.SiteMerchant = CurrentSettings.ZarinPalMerchantId;
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditBank(StoreBankAccount model, string gatewaySecretNew)
        {
            model.Title = Clip(model.Title, 120);
            model.BankName = Clip(model.BankName, 60);
            model.Branch = Clip(model.Branch, 80);
            model.AccountNumber = Clip(model.AccountNumber, 40);
            model.CardNumber = Clip(model.CardNumber, 30);
            model.Sheba = Clip(model.Sheba, 40);
            model.Owner = Clip(model.Owner, 120);
            model.Note = Clip(model.Note, 500);
            model.GatewayMerchant = Clip(model.GatewayMerchant, 100);
            model.GatewayUsername = Clip(model.GatewayUsername, 100);
            model.GatewaySecret = null;
            var error = TenantStore.SaveBank(CompanyCode, model, Clip(gatewaySecretNew, 120));
            if (error != null) return Fail(error, "EditBank", model.Id > 0 ? new { id = model.Id } : null);
            return Ok(model.IsSiteGateway
                ? "حساب ذخیره شد و مرچنت کد آن برای پرداخت آنلاین سایت تنظیم شد."
                : "حساب بانکی ذخیره شد.", "Banks");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeleteBank(int id)
        {
            var error = TenantStore.DeleteBank(CompanyCode, id);
            if (error != null) Fail(error);
            else Ok("حساب بانکی حذف شد.");
            return ToAction("Banks");
        }

        private Dictionary<int, string> BankTitles() =>
            TenantStore.GetBanks(CompanyCode).ToDictionary(b => b.Id, b => "بانک " + b.Title);

        private void BankBag(int currentBankId)
        {
            ViewBag.Banks = TenantStore.GetBanks(CompanyCode).Where(b => b.IsActive || b.Id == currentBankId).ToList();
        }

        /// <summary>Reads a «paid from» dropdown: Cash, Credit, Bank or bank-12.</summary>
        private static PayWith ReadPay(string token, out int bankId)
        {
            bankId = 0;
            token = (token ?? "").Trim();
            if (token.StartsWith("bank-", StringComparison.Ordinal))
            {
                bankId = ReadInt(token.Substring(5));
                return PayWith.Bank;
            }
            PayWith pay;
            return Enum.TryParse(token, true, out pay) ? pay : PayWith.Cash;
        }

        private void StockBags()
        {
            ViewBag.Products = TenantStore.GetProducts(CompanyCode).Where(p => p.IsActive).OrderBy(p => p.Title).ToList();
            ViewBag.Goods = TenantStore.GetGoods(CompanyCode).Where(g => g.IsActive).ToList();
        }

        private List<StoreJournalLine> ReadJournalLines()
        {
            var accounts = FormValues("lineAccount");
            var details1 = FormValues("lineDetail1");
            var details2 = FormValues("lineDetail2");
            var debits = FormValues("lineDebit");
            var credits = FormValues("lineCredit");
            var memos = FormValues("lineMemo");
            var lines = new List<StoreJournalLine>();
            for (var i = 0; i < accounts.Length && i < AccountLineCount; i++)
            {
                var accountId = ReadInt(accounts[i]);
                var debit = ReadMoney(At(debits, i));
                var credit = ReadMoney(At(credits, i));
                if (accountId <= 0 && debit == 0 && credit == 0) continue;
                lines.Add(new StoreJournalLine
                {
                    AccountId = accountId,
                    Detail1Id = ReadInt(At(details1, i)),
                    Detail2Id = ReadInt(At(details2, i)),
                    Debit = debit,
                    Credit = credit,
                    Memo = Clip(At(memos, i), 150)
                });
            }
            return lines;
        }

        private List<StorePurchaseLine> ReadPurchaseLines()
        {
            var titles = FormValues("lineTitle");
            var qtys = FormValues("lineQty");
            var costs = FormValues("lineCost");
            var stocks = FormValues("lineStock");
            var kinds = FormValues("lineKind");
            var lines = new List<StorePurchaseLine>();
            var n = Math.Max(titles.Length, Math.Max(qtys.Length, stocks.Length));
            for (var i = 0; i < n && i < AccountLineCount; i++)
            {
                int productId, goodsId;
                ReadStock(At(stocks, i), out productId, out goodsId);
                var title = At(titles, i);
                var qty = ReadInt(At(qtys, i));
                if (qty <= 0 && productId <= 0 && goodsId <= 0 && string.IsNullOrWhiteSpace(title)) continue;
                PurchaseLineKind kind;
                if (!Enum.TryParse(At(kinds, i), true, out kind)) kind = PurchaseLineKind.Goods;
                lines.Add(new StorePurchaseLine
                {
                    Title = Clip(title, 150),
                    Qty = qty,
                    UnitCost = ReadMoney(At(costs, i)),
                    ProductId = productId,
                    GoodsId = goodsId,
                    Kind = kind
                });
            }
            return lines;
        }

        private List<StoreSaleLine> ReadSaleLines()
        {
            var titles = FormValues("lineTitle");
            var qtys = FormValues("lineQty");
            var prices = FormValues("linePrice");
            var stocks = FormValues("lineStock");
            var lines = new List<StoreSaleLine>();
            var n = Math.Max(titles.Length, Math.Max(qtys.Length, stocks.Length));
            for (var i = 0; i < n && i < AccountLineCount; i++)
            {
                int productId, goodsId;
                ReadStock(At(stocks, i), out productId, out goodsId);
                var title = At(titles, i);
                var qty = ReadInt(At(qtys, i));
                if (qty <= 0 && productId <= 0 && goodsId <= 0 && string.IsNullOrWhiteSpace(title)) continue;
                lines.Add(new StoreSaleLine
                {
                    Title = Clip(title, 150),
                    Qty = qty,
                    UnitPrice = ReadMoney(At(prices, i)),
                    ProductId = productId,
                    GoodsId = goodsId
                });
            }
            return lines;
        }

        private string[] FormValues(string key)
        {
            return Request.Form.GetValues(key) ?? new string[0];
        }

        private static string At(string[] values, int index)
        {
            return values != null && index < values.Length ? values[index] : null;
        }

        private static void ReadStock(string token, out int productId, out int goodsId)
        {
            productId = 0;
            goodsId = 0;
            if (string.IsNullOrWhiteSpace(token)) return;
            int id;
            if (token.StartsWith("p-", StringComparison.Ordinal) && int.TryParse(token.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                productId = id;
            else if (token.StartsWith("g-", StringComparison.Ordinal) && int.TryParse(token.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                goodsId = id;
        }

        private static int ReadInt(string text)
        {
            int n;
            return int.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0;
        }

        private static long ReadMoney(string text)
        {
            var raw = (text ?? "").Trim().Replace(",", "").Replace("٬", "");
            long n;
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0;
        }

        private static DateTime ReadDate(string text)
        {
            DateTime date;
            return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                ? date.Date
                : DateTime.Today;
        }
    }
}
