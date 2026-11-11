using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>
    /// اعتبار پیامک هر فروشگاه و ارسال از پنل پاپوس: شارژ آنلاین، بازیابی رمز و صورتحساب خرید.
    /// هزینه واقعی پاپوس (ریال) به تومان گرد می‌شود و از <see cref="TenantInfo.SmsCredit"/> کسر می‌گردد.
    /// </summary>
    public static class SmsService
    {
        public const long DefaultPricePerCredit = 1;
        public const string KindPassword = "password";
        public const string KindInvoice = "invoice";
        public const string KindBookingConfirm = "booking-confirm";
        public const string KindBookingReminder = "booking-reminder";
        public const string KindTest = "test";
        public const string KindOther = "other";

        /// <summary>بسته‌های شارژ: اعتبار تومان و درصد تخفیف قیمت.</summary>
        private static readonly long[][] PackageCredits =
        {
            new[] { 50000L, 0 },
            new[] { 100000L, 5 },
            new[] { 200000L, 10 },
            new[] { 500000L, 15 }
        };

        private static readonly object PurchaseLock = new object();
        private static readonly object CreditLock = new object();

        public static bool IsReady => PappusSmsClient.IsConfigured;

        public static long CreditOf(string companyCode)
        {
            return Math.Max(0, TenantStore.GetTenantForAdmin(companyCode)?.SmsCredit ?? 0);
        }

        public static IReadOnlyList<SmsPackage> Packages()
        {
            return PackageCredits.Select(p =>
            {
                var credit = p[0];
                var discount = (int)p[1];
                var full = credit;
                var price = (long)Math.Round(full * (100 - discount) / 100.0 / 1000) * 1000;
                return new SmsPackage
                {
                    Credit = credit,
                    DiscountPercent = discount,
                    FullPrice = full,
                    Price = price
                };
            }).ToList();
        }

        public static SmsPurchase StartPurchase(string companyCode, long credit)
        {
            var package = Packages().FirstOrDefault(p => p.Credit == credit);
            if (package == null) return null;
            return AddPurchase(new SmsPurchase
            {
                CompanyCode = companyCode,
                Credit = package.Credit,
                Amount = package.Price,
                Status = OrderStatus.PendingPayment
            });
        }

        public static bool CompletePurchase(int id, long? refId, string message)
        {
            lock (PurchaseLock)
            {
                var purchase = GetPurchase(id);
                if (purchase == null || purchase.Status == OrderStatus.Paid) return false;
                purchase.Status = OrderStatus.Paid;
                purchase.RefId = refId;
                purchase.PaymentMessage = message;
                purchase.PaidAt = DateTime.Now;
                NikooDb.Update(NikooTables.SmsPurchases, purchase);
                AddCredit(purchase.CompanyCode, purchase.Credit);
                return true;
            }
        }

        public static void FailPurchase(SmsPurchase purchase, string message)
        {
            lock (PurchaseLock)
            {
                var stored = GetPurchase(purchase.Id);
                if (stored == null || stored.Status == OrderStatus.Paid) return;
                stored.Status = OrderStatus.Failed;
                stored.PaymentMessage = message;
                NikooDb.Update(NikooTables.SmsPurchases, stored);
            }
        }

        public static void SetAuthority(SmsPurchase purchase, string authority)
        {
            purchase.Authority = authority;
            NikooDb.Update(NikooTables.SmsPurchases, purchase);
        }

        /// <summary>اعتبار رایگان یا کسر توسط مدیر نیکو.</summary>
        public static long Grant(string companyCode, long credit, string note)
        {
            lock (PurchaseLock)
            {
                var tenant = TenantStore.GetTenantForAdmin(companyCode);
                if (tenant == null || credit == 0) return 0;
                credit = Math.Max(credit, -Math.Max(0, tenant.SmsCredit));
                if (credit == 0) return 0;
                AddPurchase(new SmsPurchase
                {
                    CompanyCode = tenant.CompanyCode,
                    Credit = credit,
                    Amount = 0,
                    Status = OrderStatus.Paid,
                    PaidAt = DateTime.Now,
                    Note = string.IsNullOrWhiteSpace(note) ? "افزوده‌شده توسط مدیر نیکو" : note.Trim()
                });
                AddCredit(tenant.CompanyCode, credit);
                return credit;
            }
        }

        public static SmsPurchase GetPurchase(int id) => NikooDb.Get(NikooTables.SmsPurchases, NikooDb.Key(id));

        public static List<SmsPurchase> Purchases(string companyCode) =>
            NikooDb.Query(NikooTables.SmsPurchases, new Row { ["CompanyCode"] = companyCode }, "[CreatedAt] DESC");

        public static List<SmsLog> Recent(string companyCode, int take = 30) =>
            NikooDb.Page(NikooTables.SmsLogs, SqlFilter.ForStore(companyCode), "t.[CreatedAt] DESC", 1, Math.Max(1, take)).ToList();

        /// <summary>ریال پاپوس → تومان (گرد به بالا).</summary>
        public static long RialToToman(long rial) => rial <= 0 ? 0 : (rial + 9) / 10;

        public static async Task<SmsSendResult> SendAsync(string companyCode, string mobile, string text, string kind, string reference = null)
        {
            if (string.IsNullOrWhiteSpace(companyCode) || TenantStore.IsDemo(companyCode))
                return SmsSendResult.Fail("ارسال پیامک در سایت نمونه فعال نیست.");
            if (!IsReady)
                return SmsSendResult.Fail("سامانه پیامک هنوز توسط نیکو تنظیم نشده است.");

            mobile = BookingTime.NormalizeMobile(mobile);
            if (mobile == null)
                return SmsSendResult.Fail("شماره موبایل معتبر نیست.");
            text = (text ?? "").Trim();
            if (text.Length == 0)
                return SmsSendResult.Fail("متن پیامک خالی است.");

            var type = kind == KindPassword ? 1 : kind == KindInvoice ? 2 : 0;
            var estimate = await PappusSmsClient.EstimateAsync(mobile, text, type).ConfigureAwait(false);
            if (!estimate.Success)
                return SmsSendResult.Fail(estimate.Message ?? "محاسبه هزینه پیامک ممکن نشد.");

            var cost = RialToToman(estimate.CostRial);
            if (cost <= 0) cost = 1;

            lock (CreditLock)
            {
                if (CreditOf(companyCode) < cost)
                    return SmsSendResult.Fail("اعتبار پیامک سایت کافی نیست (" + cost.ToToman() + " لازم است). از پنل، بخش پیامک را شارژ کنید.");
            }

            var sent = await PappusSmsClient.SendAsync(mobile, text, type, reference).ConfigureAwait(false);
            if (!sent.Success)
            {
                Log(companyCode, mobile, kind, text, 0, null, false, sent?.Message ?? "ارسال ناموفق بود.");
                return SmsSendResult.Fail(sent?.Message ?? "ارسال پیامک ناموفق بود.");
            }

            var actual = Math.Max(cost, RialToToman(sent.CostRial));
            if (!TrySpend(companyCode, actual))
            {
                // پیامک رفته؛ فقط هشدار در لاگ
                Log(companyCode, mobile, kind, text, actual, sent.JobId, true,
                    "ارسال شد ولی کسر اعتبار همزمان ممکن نشد؛ دستی بررسی شود.");
                return SmsSendResult.Ok(actual, sent.JobId);
            }

            Log(companyCode, mobile, kind, text, actual, sent.JobId, true, null);
            return SmsSendResult.Ok(actual, sent.JobId);
        }

        /// <summary>یک پیامک آزمایشی به شماره داده‌شده می‌فرستد تا از اتصال پاپوس و کافی بودن اعتبار مطمئن شوید.</summary>
        public static Task<SmsSendResult> SendTestAsync(string companyCode, string mobile, string siteName)
        {
            var text = "پیامک آزمایشی نیکو سیستم\n"
                + "فروشگاه: " + (siteName ?? companyCode) + "\n"
                + "اگر این پیام را دریافت کردید، اتصال پیامک درست است.";
            return SendAsync(companyCode, mobile, text, KindTest, "test-" + companyCode);
        }

        /// <summary>رمز موقت را پیامک می‌کند؛ در صورت خطا متن خطا برمی‌گردد.</summary>
        public static Task<SmsSendResult> SendPasswordAsync(string companyCode, string mobile, string temporaryPassword, string siteName)
        {
            var text = new StringBuilder()
                .AppendLine("بازیابی رمز عبور")
                .AppendLine("فروشگاه: " + (siteName ?? companyCode))
                .AppendLine("رمز موقت: " + temporaryPassword)
                .Append("پس از ورود، رمز را عوض کنید.")
                .ToString();
            return SendAsync(companyCode, mobile, text, KindPassword, "pwd-" + companyCode);
        }

        /// <summary>صورتحساب خرید را برای مشتری می‌فرستد.</summary>
        public static Task<SmsSendResult> SendInvoiceAsync(string companyCode, StoreOrder order, string siteName)
        {
            if (order == null) return Task.FromResult(SmsSendResult.Fail("سفارش پیدا نشد."));
            var items = order.Items == null || order.Items.Count == 0
                ? null
                : string.Join("، ", order.Items.Take(3).Select(i => i.Title + "×" + i.Quantity.ToFa()));
            if (items != null && order.Items.Count > 3) items += " و …";

            var text = new StringBuilder()
                .AppendLine("صورتحساب خرید")
                .AppendLine((siteName ?? companyCode).Trim())
                .AppendLine("سفارش: " + order.OrderNumber)
                .AppendLine("مبلغ: " + order.Amount.ToToman());
            if (!string.IsNullOrEmpty(items)) text.AppendLine("اقلام: " + items);
            text.Append(order.RefId.HasValue
                ? "پیگیری پرداخت: " + order.RefId.Value.ToString(CultureInfo.InvariantCulture)
                : "پرداخت ثبت شد.");
            var body = text.ToString();

            return SendAsync(companyCode, order.CustomerMobile, body, KindInvoice, "order-" + order.Id);
        }

        /// <summary>صورتحساب را در پس‌زمینه می‌فرستد تا پرداخت مشتری معطل نشود.</summary>
        public static void QueueInvoice(string companyCode, StoreOrder order)
        {
            if (string.IsNullOrEmpty(companyCode) || order == null || TenantStore.IsDemo(companyCode)) return;
            if (!IsReady || CreditOf(companyCode) <= 0) return;

            var siteName = BaleNotifier.SiteName(companyCode);
            var snapshot = order;
            Func<CancellationToken, Task> work = async ct =>
            {
                try { await SendInvoiceAsync(companyCode, snapshot, siteName).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Sms invoice " + companyCode + ": " + ex.Message);
                }
            };
            try { HostingEnvironment.QueueBackgroundWorkItem(work); }
            catch (InvalidOperationException) { Task.Run(() => work(CancellationToken.None)); }
        }

        /// <summary>تأیید نوبت را برای مشتری پیامک می‌کند؛ فقط وقتی تنظیمات روشن است و هنوز ارسال نشده.</summary>
        public static Task<SmsSendResult> SendAppointmentConfirmAsync(string companyCode, StoreAppointment appointment, string siteName)
        {
            if (appointment == null) return Task.FromResult(SmsSendResult.Fail("نوبت پیدا نشد."));
            var text = new StringBuilder()
                .AppendLine("تأیید نوبت")
                .AppendLine((siteName ?? companyCode).Trim())
                .AppendLine("خدمت: " + appointment.ServiceTitle)
                .AppendLine("متخصص: " + appointment.StaffName)
                .AppendLine("زمان: " + BookingTime.DayNames[BookingTime.DayOf(appointment.StartAt)] + " " + appointment.StartAt.ToShamsiText() + " ساعت " + BookingTime.Clock(appointment.StartAt))
                .Append("کد پیگیری: " + appointment.Code)
                .ToString();
            return SendAsync(companyCode, appointment.CustomerMobile, text, KindBookingConfirm, "appt-c-" + appointment.Id);
        }

        /// <summary>یادآوری نزدیک شدن نوبت را پیامک می‌کند.</summary>
        public static Task<SmsSendResult> SendAppointmentReminderAsync(string companyCode, StoreAppointment appointment, string siteName)
        {
            if (appointment == null) return Task.FromResult(SmsSendResult.Fail("نوبت پیدا نشد."));
            var text = new StringBuilder()
                .AppendLine("یادآوری نوبت")
                .AppendLine((siteName ?? companyCode).Trim())
                .AppendLine("خدمت: " + appointment.ServiceTitle)
                .AppendLine("متخصص: " + appointment.StaffName)
                .AppendLine("زمان: " + BookingTime.DayNames[BookingTime.DayOf(appointment.StartAt)] + " " + appointment.StartAt.ToShamsiText() + " ساعت " + BookingTime.Clock(appointment.StartAt))
                .Append("کد پیگیری: " + appointment.Code)
                .ToString();
            return SendAsync(companyCode, appointment.CustomerMobile, text, KindBookingReminder, "appt-r-" + appointment.Id);
        }

        /// <summary>تأیید نوبت را در پس‌زمینه می‌فرستد و در صورت موفقیت زمان ارسال را روی نوبت می‌نویسد.</summary>
        public static void QueueAppointmentConfirm(string companyCode, StoreAppointment appointment)
        {
            if (string.IsNullOrEmpty(companyCode) || appointment == null || TenantStore.IsDemo(companyCode)) return;
            if (appointment.ConfirmSmsAt.HasValue) return;
            if (appointment.Status != AppointmentStatus.Confirmed) return;
            if (appointment.StartAt < DateTime.Now) return;
            var settings = TenantStore.GetSettings(companyCode);
            if (settings == null || !settings.SmsBookingConfirm) return;
            if (!IsReady || CreditOf(companyCode) <= 0) return;

            var siteName = BaleNotifier.SiteName(companyCode);
            var id = appointment.Id;
            Func<CancellationToken, Task> work = async ct =>
            {
                try
                {
                    var fresh = TenantStore.GetAppointment(companyCode, id);
                    if (fresh == null || fresh.ConfirmSmsAt.HasValue || fresh.Status != AppointmentStatus.Confirmed) return;
                    var result = await SendAppointmentConfirmAsync(companyCode, fresh, siteName).ConfigureAwait(false);
                    if (result.Success)
                    {
                        fresh.ConfirmSmsAt = DateTime.Now;
                        TenantStore.UpdateAppointment(companyCode, fresh);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Sms booking confirm " + companyCode + ": " + ex.Message);
                }
            };
            try { HostingEnvironment.QueueBackgroundWorkItem(work); }
            catch (InvalidOperationException) { Task.Run(() => work(CancellationToken.None)); }
        }

        /// <summary>
        /// Reminder SMSes due soon for every booking/campus store. Called from the hourly app timer.
        /// </summary>
        public static int SendDueBookingReminders()
        {
            if (!IsReady) return 0;
            var sent = 0;
            foreach (var tenant in TenantStore.GetTenants())
            {
                if (tenant == null || string.IsNullOrEmpty(tenant.CompanyCode) || TenantStore.IsDemo(tenant.CompanyCode)) continue;
                if (!tenant.IsActive) continue;
                var kind = SiteKinds.Of(tenant);
                if (!kind.Schedules) continue;
                if (CreditOf(tenant.CompanyCode) <= 0) continue;

                var settings = TenantStore.GetSettings(tenant.CompanyCode);
                var hours = settings?.SmsBookingReminderHours ?? 0;
                if (hours <= 0) continue;

                var now = DateTime.Now;
                var from = now;
                var to = now.AddHours(hours).AddMinutes(5);
                List<StoreAppointment> due;
                try { due = TenantStore.AppointmentsNeedingReminder(tenant.CompanyCode, from, to); }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Sms reminder list " + tenant.CompanyCode + ": " + ex.Message);
                    continue;
                }

                var siteName = BaleNotifier.SiteName(tenant.CompanyCode);
                foreach (var appointment in due)
                {
                    try
                    {
                        var result = SendAppointmentReminderAsync(tenant.CompanyCode, appointment, siteName).GetAwaiter().GetResult();
                        if (!result.Success) continue;
                        appointment.ReminderSmsAt = DateTime.Now;
                        if (!appointment.ConfirmSmsAt.HasValue) appointment.ConfirmSmsAt = appointment.ReminderSmsAt;
                        TenantStore.UpdateAppointment(tenant.CompanyCode, appointment);
                        sent++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError("Sms reminder " + tenant.CompanyCode + "/" + appointment.Id + ": " + ex.Message);
                    }
                }
            }
            return sent;
        }

        private static bool TrySpend(string companyCode, long toman)
        {
            if (toman <= 0) return true;
            lock (CreditLock)
            {
                var credit = CreditOf(companyCode);
                if (credit < toman) return false;
                // Increment فقط int می‌پذیرد؛ مبالغ بسته پیامک در محدوده int است.
                var delta = (int)Math.Min(toman, int.MaxValue);
                NikooDb.Increment(NikooTables.Tenants, NikooDb.Key(companyCode), "SmsCredit", -delta);
                return true;
            }
        }

        private static void AddCredit(string companyCode, long toman)
        {
            if (toman == 0) return;
            var left = toman;
            while (left != 0)
            {
                var step = (int)Math.Max(Math.Min(left, int.MaxValue), Math.Max(left, -int.MaxValue));
                NikooDb.Increment(NikooTables.Tenants, NikooDb.Key(companyCode), "SmsCredit", step);
                left -= step;
            }
        }

        private static SmsPurchase AddPurchase(SmsPurchase purchase)
        {
            lock (PurchaseLock)
            using (var tx = NikooDb.Begin())
            {
                purchase.Id = NikooDb.NextId(NikooTables.SmsPurchases, null, tx);
                purchase.CreatedAt = DateTime.Now;
                NikooDb.Insert(NikooTables.SmsPurchases, purchase, tx: tx);
                tx.Commit();
                return purchase;
            }
        }

        private static void Log(string companyCode, string mobile, string kind, string text, long cost, long? jobId, bool success, string error)
        {
            try
            {
                using (var tx = NikooDb.Begin())
                {
                    var row = new SmsLog
                    {
                        Id = NikooDb.NextId(NikooTables.SmsLogs, null, tx),
                        CompanyCode = companyCode,
                        Mobile = mobile,
                        Kind = kind ?? KindOther,
                        Text = text != null && text.Length > 500 ? text.Substring(0, 500) : text,
                        Cost = cost,
                        JobId = jobId,
                        Success = success,
                        Error = error,
                        CreatedAt = DateTime.Now
                    };
                    NikooDb.Insert(NikooTables.SmsLogs, row, tx: tx);
                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Sms log: " + ex.Message);
            }
        }
    }
}
