using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public partial class ShopController
    {
        private ActionResult RepairHome()
        {
            ViewBag.Title = "تعمیرگاه";
            var sections = ViewBag.Sections as List<StoreHomeSection>;
            if (sections != null)
            {
                foreach (var sec in sections)
                {
                    string sample;
                    if (string.IsNullOrWhiteSpace(sec.Items) && RepairText.HomeSamples.TryGetValue(sec.Type, out sample))
                        sec.Items = sample;
                }
            }
            if (TenantStore.IsDemo(CompanyCode))
                ViewBag.Sample = TenantStore.GetRepairJobs(CompanyCode).Find(j => j.IsSample && j.Status == RepairStatus.Ready);
            return View("RepairHome");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult RepairRequest()
        {
            ViewBag.Title = "درخواست تعمیر";
            return View(new RepairJob { DeviceType = "موبایل", Handover = "pickup" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult RepairRequest(RepairJob model)
        {
            ViewBag.Title = "درخواست تعمیر";
            model.CustomerName = (model.CustomerName ?? "").Trim();
            model.Problem = (model.Problem ?? "").Trim();
            if (model.CustomerName.Length < 2)
            {
                Fail("نام را بنویسید.");
                return View(model);
            }
            if (BookingTime.NormalizeMobile(model.CustomerMobile) == null)
            {
                Fail("شماره موبایل معتبر نیست.");
                return View(model);
            }
            if (model.Problem.Length < 3)
            {
                Fail("شرح مشکل دستگاه را بنویسید.");
                return View(model);
            }
            var handover = (model.Handover ?? "").Trim();
            model.Handover = string.Equals(handover, "visit", StringComparison.OrdinalIgnoreCase) ? "visit"
                : string.Equals(handover, "counter", StringComparison.OrdinalIgnoreCase) ? "counter" : "pickup";
            model.PickupAddress = (model.PickupAddress ?? "").Trim();
            if (model.Handover == "counter")
            {
                model.PickupAddress = null;
                model.PickupWindow = null;
                model.ReturnByCourier = false;
            }
            else if (model.PickupAddress.Length < 8)
            {
                Fail(model.Handover == "visit" ? "آدرس محل را بنویسید." : "آدرس تحویل دستگاه به پیک را بنویسید.");
                return View(model);
            }
            else model.ReturnByCourier = model.Handover == "pickup";
            model.Status = RepairStatus.Requested;
            model.Source = "online";
            model.TravelAmount = 0;
            var saved = TenantStore.AddRepair(CompanyCode, model);
            SmsService.QueueRepairStatus(CompanyCode, saved);
            Notifications.RepairIntake(CompanyCode, saved);
            return RepairTicket(saved, "کد پیگیری");
        }

        [HttpGet]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult RepairTrack()
        {
            ViewBag.Title = "پیگیری تعمیر";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Repair)]
        public ActionResult RepairTrack(string code, string mobile)
        {
            var job = TenantStore.FindRepairForCustomer(CompanyCode, code, mobile);
            if (job == null)
            {
                Fail("برگه‌ای با این کد و شماره موبایل پیدا نشد.");
                ViewBag.Code = code;
                ViewBag.Title = "پیگیری تعمیر";
                return View();
            }
            return RepairTicket(job, "وضعیت تعمیر");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Repair)]
        public async Task<ActionResult> ApproveRepairQuote(string code, string mobile, string decision)
        {
            var returning = string.Equals(decision, "return", StringComparison.OrdinalIgnoreCase);
            var approve = string.Equals(decision, "yes", StringComparison.OrdinalIgnoreCase);
            var before = TenantStore.FindRepairForCustomer(CompanyCode, code, mobile);
            if (!approve && !returning)
            {
                Fail("تصمیم هزینه مشخص نیست.");
                return before == null ? View("RepairTrack") : RepairTicket(before, "وضعیت تعمیر");
            }

            RepairJob staged;
            long amount;
            var error = TenantStore.StageRepairPayment(CompanyCode, code, mobile, returning, out staged, out amount);
            if (error != null)
            {
                Fail(error);
                return staged == null ? View("RepairTrack") : RepairTicket(staged, "وضعیت تعمیر");
            }

            if (amount <= 0)
                return FinishRepairDecision(staged, null, null);

            if (!CurrentSettings.EnablePayment || string.IsNullOrWhiteSpace(CurrentSettings.ZarinPalMerchantId))
            {
                Fail("درگاه پرداخت وصل نیست. تا مبلغ پرداخت نشود، تعمیر یا مرجوعی شروع نمی‌شود.");
                return RepairTicket(staged, "وضعیت تعمیر");
            }

            var callback = Request.Url.GetLeftPart(UriPartial.Authority) + "/Shop/" + CompanyCode + "/RepairPayCallback?id=" + staged.Id;
            var pay = await Gateway().RequestPaymentAsync(
                amount,
                (returning ? "مرجوعی " : "تعمیر ") + staged.Code + " - " + CurrentSettings.StoreName,
                null,
                staged.CustomerMobile,
                staged.Id.ToString(),
                callback);
            if (!pay.Success)
            {
                Fail("اتصال به درگاه پرداخت انجام نشد: " + pay.Message);
                return RepairTicket(staged, "وضعیت تعمیر");
            }

            TenantStore.RememberRepairAuthority(CompanyCode, staged.Id, pay.Authority);
            return Redirect(pay.PaymentUrl);
        }

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Repair)]
        public async Task<ActionResult> RepairPayCallback(int? id, string Authority, string Status)
        {
            var job = id.HasValue ? TenantStore.GetRepairJob(CompanyCode, id.Value) : null;
            if (job == null)
            {
                Fail("برگه پیدا نشد.");
                ViewBag.Title = "پیگیری تعمیر";
                return View("RepairTrack");
            }
            if (string.IsNullOrEmpty(job.PaymentDecision) && (job.QuoteState == RepairQuoteState.Approved || job.ReturnRequested))
                return RepairTicket(job, "وضعیت تعمیر");

            var verify = await Gateway().VerifyCallbackAsync(job.PaymentAuthority, Authority, Status, job.PaymentAmount);
            if (verify.Forged || !verify.Success)
            {
                Fail(verify.Forged ? "اطلاعات بازگشت از درگاه معتبر نبود. کار شروع نشد." : "پرداخت انجام نشد و کار شروع نشد.");
                return RepairTicket(job, "وضعیت تعمیر");
            }
            return FinishRepairDecision(job, verify.RefId, verify.Message);
        }

        private ActionResult FinishRepairDecision(RepairJob staged, long? refId, string gatewayMessage)
        {
            var beforeStatus = staged.Status;
            var returning = staged.PaymentDecision == "return";
            var error = TenantStore.ApplyPaidRepairDecision(CompanyCode, staged.Id, refId, gatewayMessage);
            var job = TenantStore.GetRepairJob(CompanyCode, staged.Id) ?? staged;
            if (error != null) Fail(error);
            else if (returning)
            {
                NikooSystem.Services.Notifications.RepairReturn(CompanyCode, job);
                Ok(job.BringsInPerson
                    ? "دستگاه برای پس‌گرفتن حضوری آماده است. چون خودتان آن را آورده‌اید، ایاب و ذهاب گرفته نشد."
                    : "هزینه حمل پرداخت شد و مرجوعی دستگاه شروع شد.");
            }
            else
            {
                NikooSystem.Services.Notifications.RepairPaid(CompanyCode, job);
                Ok("مبلغ پرداخت شد و تعمیر شروع شد.");
            }
            if (job.Status != beforeStatus)
                SmsService.QueueRepairStatus(CompanyCode, job);
            return RepairTicket(job, "وضعیت تعمیر");
        }

        private ActionResult RepairTicket(RepairJob job, string title)
        {
            ViewBag.Title = title;
            ViewBag.Balance = TenantStore.RepairBalance(CompanyCode, job);
            return View("RepairTicket", job);
        }
    }
}
