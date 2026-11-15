using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public partial class ShopAdminController
    {
        [HttpGet]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult Repairs(string status)
        {
            RepairStatus parsed;
            RepairStatus? filter = Enum.TryParse(status ?? "", true, out parsed) ? parsed : (RepairStatus?)null;
            var jobs = TenantStore.GetRepairJobs(CompanyCode);
            ViewBag.Filter = filter;
            ViewBag.OpenCount = jobs.Count(j => j.IsOpen);
            return View(filter.HasValue ? jobs.Where(j => j.Status == filter.Value).ToList() : jobs);
        }

        [HttpGet]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult NewRepair()
        {
            FillRepairForm();
            return View(new RepairJob { Status = RepairStatus.Received, DeviceType = "موبایل", Handover = "counter" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult NewRepair(RepairJob model, string promised, string[] partTitle, string[] partQty, string[] partPrice, string[] partGoods)
        {
            FillRepairForm();
            var error = ReadRepair(model, promised, partTitle, partQty, partPrice, partGoods);
            if (error != null)
            {
                Fail(error);
                return View(model);
            }
            if (model.Status == RepairStatus.Requested) model.Status = RepairStatus.Received;
            model.Source = "counter";
            if (string.IsNullOrWhiteSpace(model.Handover)) model.Handover = "counter";
            var saved = TenantStore.AddRepair(CompanyCode, model);
            SmsService.QueueRepairStatus(CompanyCode, saved);
            NikooSystem.Services.Notifications.RepairIntake(CompanyCode, saved);
            return Ok("برگه پذیرش ثبت شد. کد پیگیری: " + saved.Code, "RepairDetails", new { id = saved.Id });
        }

        [HttpGet]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult RepairDetails(int id)
        {
            var job = TenantStore.GetRepairJob(CompanyCode, id);
            if (job == null) return ToAction("Repairs");
            ViewBag.Banks = TenantStore.GetBanks(CompanyCode).Where(b => b.IsActive).ToList();
            ViewBag.Balance = TenantStore.RepairBalance(CompanyCode, job);
            ViewBag.Moves = TenantStore.NextMoves(job);
            ViewBag.Invoice = job.SaleInvoiceId > 0 ? TenantStore.GetSale(CompanyCode, job.SaleInvoiceId) : null;
            FillRepairForm();
            return View(job);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult UpdateRepair(RepairJob model, string promised, string[] partTitle, string[] partQty, string[] partPrice, string[] partGoods)
        {
            var existing = TenantStore.GetRepairJob(CompanyCode, model.Id);
            if (existing == null) return ToAction("Repairs");
            var error = ReadRepair(model, promised, partTitle, partQty, partPrice, partGoods);
            if (error != null) return Fail(error, "RepairDetails", new { id = model.Id });
            error = TenantStore.GuardRepairMove(CompanyCode, existing, model);
            if (error != null) return Fail(error, "RepairDetails", new { id = model.Id });
            var previous = existing.Status;
            var saved = TenantStore.UpdateRepair(CompanyCode, model);
            if (saved.Status != previous)
                SmsService.QueueRepairStatus(CompanyCode, saved);
            return Ok("برگه تعمیر ذخیره شد.", "RepairDetails", new { id = saved.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult CollectRepairPayment(int id, string amount, string payFrom)
        {
            var job = TenantStore.GetRepairJob(CompanyCode, id);
            if (job == null) return ToAction("Repairs");
            long money;
            if (!long.TryParse((amount ?? "").Replace(",", "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out money))
                money = TenantStore.RepairBalance(CompanyCode, job);
            PayWith pay;
            int bankId;
            ReadPay(payFrom, out pay, out bankId);
            var error = TenantStore.RecordRepairPayment(CompanyCode, id, money, pay, bankId);
            if (error != null) return Fail(error, "RepairDetails", new { id });
            return Ok("مبلغ به صندوق نشست و از طلب مشتری کم شد.", "RepairDetails", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult DispatchPickup(int id, string courierName, string courierMobile, string pickupWindow)
        {
            var error = TenantStore.DispatchPickup(CompanyCode, id, courierName, courierMobile, pickupWindow);
            if (error != null) return Fail(error, "RepairDetails", new { id });
            var saved = TenantStore.GetRepairJob(CompanyCode, id);
            SmsService.QueueRepairStatus(CompanyCode, saved);
            var who = saved != null && saved.NeedsVisit ? "کارشناس" : "پیک";
            return Ok(who + " هماهنگ شد و برای مشتری پیام می‌رود.", "RepairDetails", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult AdvanceRepair(int id, string status)
        {
            RepairStatus to;
            if (!Enum.TryParse(status ?? "", true, out to))
                return Fail("مرحله نامعتبر است.", "RepairDetails", new { id });
            var error = TenantStore.AdvanceRepair(CompanyCode, id, to);
            if (error != null) return Fail(error, "RepairDetails", new { id });
            SmsService.QueueRepairStatus(CompanyCode, TenantStore.GetRepairJob(CompanyCode, id));
            return Ok("وضعیت برگه به‌روز شد.", "RepairDetails", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult PostRepairInvoice(int id)
        {
            var error = TenantStore.PostRepairInvoice(CompanyCode, id);
            if (error != null) return Fail(error, "RepairDetails", new { id });
            var job = TenantStore.GetRepairJob(CompanyCode, id);
            var posted = job != null && job.ReturnRequested
                ? "صورتحساب حمل رفت و برگشت در فاکتور فروش نشست. اجرت و قطعه حساب نشد."
                : job != null && job.TravelAmount > 0
                ? "صورتحساب در فاکتور فروش نشست. اجرت به درآمد تعمیر، ایاب و ذهاب به درآمد حمل، و قطعه انبار از موجودی کم شد."
                : "صورتحساب در فاکتور فروش نشست. اجرت به درآمد تعمیر رفت و قطعه انبار از موجودی کم شد.";
            return Ok(posted, "RepairDetails", new { id });
        }

        [HttpGet]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult PrintRepair(int id)
        {
            var job = TenantStore.GetRepairJob(CompanyCode, id);
            if (job == null) return ToAction("Repairs");
            ViewBag.Balance = TenantStore.RepairBalance(CompanyCode, job);
            ViewBag.Invoice = job.SaleInvoiceId > 0 ? TenantStore.GetSale(CompanyCode, job.SaleInvoiceId) : null;
            return View(job);
        }

        private string ReadRepair(RepairJob model, string promised, string[] partTitle, string[] partQty, string[] partPrice, string[] partGoods)
        {
            model.CustomerName = (model.CustomerName ?? "").Trim();
            if (model.CustomerName.Length < 2) return "نام مشتری را بنویسید.";
            if (BookingTime.NormalizeMobile(model.CustomerMobile) == null) return "شماره موبایل معتبر نیست.";
            model.Problem = (model.Problem ?? "").Trim();
            if (model.Problem.Length < 3) return "شرح مشکل دستگاه را بنویسید.";
            DateTime promisedAt;
            model.PromisedAt = DateTime.TryParse(promised, CultureInfo.InvariantCulture, DateTimeStyles.None, out promisedAt)
                ? promisedAt.Date
                : (DateTime?)null;
            model.Parts = TenantStore.CleanParts(partTitle, partQty, partPrice, partGoods);
            var goods = TenantStore.GetGoods(CompanyCode);
            foreach (var part in model.Parts.Where(p => p.GoodsId > 0))
            {
                var item = goods.FirstOrDefault(g => g.Id == part.GoodsId);
                if (item == null) return "کالای انبار انتخاب‌شده پیدا نشد.";
                if (string.IsNullOrWhiteSpace(part.Title) || part.Title == "قطعه") part.Title = item.Title;
                if (part.UnitPrice <= 0)
                {
                    var cost = TenantStore.AverageGoodsCost(CompanyCode, part.GoodsId);
                    if (cost > 0) part.UnitPrice = cost;
                }
            }
            return null;
        }

        private void FillRepairForm()
        {
            var goods = TenantStore.GetGoods(CompanyCode).Where(g => g.IsActive).ToList();
            ViewBag.Goods = goods;
            ViewBag.GoodsCost = goods.ToDictionary(g => g.Id, g => TenantStore.AverageGoodsCost(CompanyCode, g.Id));
        }

        private static void ReadPay(string payFrom, out PayWith pay, out int bankId)
        {
            bankId = 0;
            var token = (payFrom ?? "").Trim();
            if (token.StartsWith("bank-", StringComparison.Ordinal))
            {
                int.TryParse(token.Substring(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out bankId);
                pay = PayWith.Bank;
                return;
            }
            if (!Enum.TryParse(token, true, out pay)) pay = PayWith.Cash;
        }
    }
}
