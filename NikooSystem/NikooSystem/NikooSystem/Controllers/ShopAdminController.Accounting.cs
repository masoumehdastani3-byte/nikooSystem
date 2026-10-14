using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
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
        public ActionResult Accounts()
        {
            return View(TenantStore.GetAccounts(CompanyCode));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditAccount(int? id)
        {
            var item = id.HasValue ? TenantStore.GetAccount(CompanyCode, id.Value) : new StoreLedgerAccount { IsActive = true };
            if (item == null) return ToAction("Accounts");
            ViewBag.Parents = TenantStore.GetAccounts(CompanyCode).Where(a => a.Id != item.Id).ToList();
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
            if (error != null) return Fail(error, "EditAccount", model.Id > 0 ? new { id = model.Id } : null);
            return Ok("سرفصل ذخیره شد.", "Accounts");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult DeleteAccount(int id)
        {
            var error = TenantStore.DeleteAccount(CompanyCode, id);
            if (error != null) Fail(error);
            else Ok("سرفصل حذف شد.");
            return ToAction("Accounts");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult Journal()
        {
            return View(TenantStore.GetJournal(CompanyCode));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditJournal(int? id)
        {
            var item = id.HasValue ? TenantStore.GetEntry(CompanyCode, id.Value) : new StoreJournalEntry { EntryDate = DateTime.Today };
            if (item == null) return ToAction("Journal");
            if (item.Id > 0 && item.Source != JournalSource.Manual)
                return Fail("سندهای خودکار از فاکتور، سفارش یا تعدیل انبار اصلاح می‌شوند.", "Journal");
            ViewBag.Accounts = TenantStore.GetAccounts(CompanyCode).Where(a => a.IsActive).ToList();
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
        public ActionResult Purchases()
        {
            return View(TenantStore.GetPurchases(CompanyCode));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditPurchase(int? id)
        {
            var item = id.HasValue ? TenantStore.GetPurchase(CompanyCode, id.Value) : new StorePurchaseInvoice { InvoiceDate = DateTime.Today };
            if (item == null) return ToAction("Purchases");
            StockBags();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditPurchase(StorePurchaseInvoice model, string invoiceDate)
        {
            model.InvoiceDate = ReadDate(invoiceDate);
            model.PartyName = Clip(model.PartyName, 120);
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
        public ActionResult SaleInvoices()
        {
            return View(TenantStore.GetSales(CompanyCode));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditSaleInvoice(int? id)
        {
            var item = id.HasValue ? TenantStore.GetSale(CompanyCode, id.Value) : new StoreSalesInvoice { InvoiceDate = DateTime.Today };
            if (item == null) return ToAction("SaleInvoices");
            StockBags();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult EditSaleInvoice(StoreSalesInvoice model, string invoiceDate)
        {
            model.InvoiceDate = ReadDate(invoiceDate);
            model.PartyName = Clip(model.PartyName, 120);
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
        public ActionResult Inventory()
        {
            ViewBag.Products = TenantStore.GetProducts(CompanyCode).Where(p => !p.IsDigital()).OrderBy(p => p.Title).ToList();
            ViewBag.Moves = TenantStore.GetStockMoves(CompanyCode).Take(40).ToList();
            return View(TenantStore.GetGoods(CompanyCode));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult SaveGoods(StoreGoodsItem model)
        {
            model.Title = Clip(model.Title, 150);
            model.Unit = Clip(model.Unit, 20);
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
            return ToAction("Inventory");
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
        public ActionResult Assets()
        {
            return View(TenantStore.GetAssets(CompanyCode));
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
        public ActionResult Books(int? accountId)
        {
            var accounts = TenantStore.GetAccounts(CompanyCode);
            var entries = TenantStore.GetJournal(CompanyCode).Where(e => !e.Reversed).ToList();
            var page = new BooksPage();
            foreach (var account in accounts)
            {
                long debit = 0, credit = 0;
                foreach (var entry in entries)
                    foreach (var line in entry.Lines ?? new List<StoreJournalLine>())
                        if (line.AccountId == account.Id)
                        {
                            debit += line.Debit;
                            credit += line.Credit;
                        }
                if (debit == 0 && credit == 0) continue;
                page.Totals.Add(new AccountBalance { Account = account, Debit = debit, Credit = credit });
                page.Debit += debit;
                page.Credit += credit;
            }
            if (accountId.HasValue)
            {
                page.Account = accounts.FirstOrDefault(a => a.Id == accountId.Value);
                if (page.Account != null)
                {
                    foreach (var entry in entries.OrderBy(e => e.EntryDate).ThenBy(e => e.Id))
                        foreach (var line in entry.Lines ?? new List<StoreJournalLine>())
                            if (line.AccountId == page.Account.Id)
                                page.Movements.Add(new LedgerMovement
                                {
                                    Date = entry.EntryDate,
                                    Number = entry.Number,
                                    Description = entry.Description,
                                    Memo = line.Memo,
                                    Debit = line.Debit,
                                    Credit = line.Credit
                                });
                }
            }
            return View(page);
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital, SiteKinds.Campus)]
        public ActionResult AccountingGuide()
        {
            return View();
        }

        private void StockBags()
        {
            ViewBag.Products = TenantStore.GetProducts(CompanyCode).Where(p => p.IsActive).OrderBy(p => p.Title).ToList();
            ViewBag.Goods = TenantStore.GetGoods(CompanyCode).Where(g => g.IsActive).ToList();
        }

        private List<StoreJournalLine> ReadJournalLines()
        {
            var accounts = FormValues("lineAccount");
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
                lines.Add(new StoreJournalLine { AccountId = accountId, Debit = debit, Credit = credit, Memo = Clip(At(memos, i), 150) });
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
