using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    public static partial class TenantStore
    {
        /// <summary>The demo of the repair shop.</summary>
        internal static void SeedRepairDemo()
        {
            if (CompanyCodeExists("REPAIR")) return;
            AddDemo("REPAIR", "تعمیرگاه همراه", "repair");
        }

        /// <summary>Two sample jobs so a new shop is not an empty list. Called after the store lock is released.</summary>
        internal static void SeedSampleRepairs(string companyCode)
        {
            if (string.IsNullOrWhiteSpace(companyCode)) return;
            if (All(NikooTables.RepairJobs, companyCode).Count > 0) return;
            AddRepair(companyCode, new RepairJob
            {
                CustomerName = "نمونه: رضا محمدی",
                CustomerMobile = "09120000000",
                DeviceType = "موبایل",
                Brand = "سامسونگ",
                ModelName = "A54",
                Problem = "صفحه خاموش می‌شود و شارژ نگه نمی‌دارد.",
                Diagnosis = "باتری باد کرده؛ نیاز به تعویض.",
                Status = RepairStatus.Ready,
                LaborAmount = 350000,
                Parts = new List<RepairPart> { new RepairPart { Title = "باتری", Qty = 1, UnitPrice = 900000 } },
                Technician = "امیر",
                Source = "counter",
                IsSample = true,
                LastSmsStatus = RepairStatus.Ready.ToString(),
                PromisedAt = DateTime.Today
            });
            AddRepair(companyCode, new RepairJob
            {
                CustomerName = "نمونه: سارا کریمی",
                CustomerMobile = "09121111111",
                DeviceType = "لپ‌تاپ",
                Brand = "لنوو",
                ModelName = "IdeaPad",
                Problem = "روشن نمی‌شود.",
                Status = RepairStatus.Requested,
                EstimateAmount = 500000,
                Source = "online",
                Handover = "pickup",
                PickupAddress = "تهران، خیابان نمونه، پلاک ۱۲",
                PickupWindow = "فردا ۱۶ تا ۱۸",
                ReturnByCourier = true,
                IsSample = true,
                LastSmsStatus = RepairStatus.Requested.ToString()
            });
        }

        public static List<RepairJob> GetRepairJobs(string companyCode)
        {
            return All(NikooTables.RepairJobs, companyCode, "[CreatedAt] DESC, [Id] DESC");
        }

        public static RepairJob GetRepairJob(string companyCode, int id) => Find(NikooTables.RepairJobs, companyCode, id);

        public static RepairJob GetRepairByCode(string companyCode, string code)
        {
            code = NormalizeRepairCode(code);
            if (code == null) return null;
            return FindBy(NikooTables.RepairJobs, companyCode, "Code", code);
        }

        /// <summary>The public tracking page: the code alone is not enough.</summary>
        public static RepairJob FindRepairForCustomer(string companyCode, string code, string mobile)
        {
            var job = GetRepairByCode(companyCode, code);
            var phone = BookingTime.NormalizeMobile(mobile);
            if (job == null || phone == null || job.CustomerMobile != phone) return null;
            return job;
        }

        public static int CountOpenRepairs(string companyCode) =>
            GetRepairJobs(companyCode).Count(j => j.IsOpen);

        public static int CountRepairs(string companyCode, bool samples) =>
            GetRepairJobs(companyCode).Count(j => j.IsSample == samples);

        public static RepairJob AddRepair(string companyCode, RepairJob job)
        {
            job.Id = 0;
            PrepareRepair(job);
            return Save(NikooTables.RepairJobs, companyCode, job, (item, stored) =>
            {
                item.CreatedAt = DateTime.Now;
                item.Code = NewRepairCode(companyCode);
                StampRepair(item, null);
            });
        }

        public static RepairJob UpdateRepair(string companyCode, RepairJob job)
        {
            PrepareRepair(job);
            return Save(NikooTables.RepairJobs, companyCode, job, (item, stored) =>
            {
                if (stored == null) return;
                item.CreatedAt = stored.CreatedAt;
                item.Code = stored.Code;
                item.PaidAmount = stored.PaidAmount;
                item.PaidAt = stored.PaidAt;
                item.LastSmsStatus = stored.LastSmsStatus;
                item.IsSample = stored.IsSample;
                item.Source = string.IsNullOrWhiteSpace(stored.Source) ? item.Source : stored.Source;
                item.ReceivedAt = stored.ReceivedAt;
                item.ReadyAt = stored.ReadyAt;
                item.DeliveredAt = stored.DeliveredAt;
                item.CancelledAt = stored.CancelledAt;
                item.PickedUpAt = stored.PickedUpAt;
                item.SaleInvoiceId = stored.SaleInvoiceId;
                item.QuoteState = stored.QuoteState;
                item.ReturnRequested = stored.ReturnRequested;
                item.ReturnShipping = stored.ReturnShipping;
                item.PaymentAuthority = stored.PaymentAuthority;
                item.PaymentDecision = stored.PaymentDecision;
                item.PaymentAmount = stored.PaymentAmount;
                if (stored.ReturnRequested)
                {
                    item.TravelAmount = item.BringsInPerson ? 0 : stored.TravelAmount;
                    item.LaborAmount = stored.LaborAmount;
                    item.Parts = stored.Parts;
                    item.ReturnShipping = item.BringsInPerson ? 0 : stored.ReturnShipping;
                    item.ReturnByCourier = !item.BringsInPerson && stored.ReturnByCourier;
                }
                if (stored.SaleInvoiceId > 0)
                {
                    item.LaborAmount = stored.LaborAmount;
                    item.TravelAmount = stored.TravelAmount;
                    item.Parts = stored.Parts;
                    item.WarrantyDays = stored.WarrantyDays;
                }
                StampRepair(item, stored.Status);
            });
        }

        /// <summary>What is still owed. After the sales invoice, tax on that invoice is part of the balance.</summary>
        public static long RepairBalance(string companyCode, RepairJob job)
        {
            if (job == null) return 0;
            if (job.SaleInvoiceId > 0)
            {
                var sale = GetSale(companyCode, job.SaleInvoiceId);
                if (sale != null) return Math.Max(0, sale.Total - job.PaidAmount);
            }
            return job.Due;
        }

        /// <summary>Rejects a status jump that skips the courier, the quote, or an unpaid delivery.</summary>
        public static string GuardRepairMove(string companyCode, RepairJob stored, RepairJob next)
        {
            if (stored == null || next == null || stored.Status == next.Status) return null;
            if (stored.SaleInvoiceId > 0 && next.Status == RepairStatus.Cancelled)
                return "بعد از ثبت صورتحساب، برگه را لغو نکنید. فاکتور را از حسابداری اصلاح کنید.";
            if (!CanAdvance(stored, next.Status))
                return "از «" + stored.Status.StepTitle(stored) + "» فقط مرحله بعد مجاز است.";
            if (next.Status == RepairStatus.QuoteSent && next.Charges <= 0)
                return "قبل از پیش‌فاکتور، اجرت، قطعه یا ایاب و ذهاب را بنویسید.";
            if (next.Status == RepairStatus.Delivered && stored.SaleInvoiceId <= 0 && !(stored.ReturnRequested && stored.Charges <= 0))
                return "اول صورتحساب را در حسابداری ثبت کنید.";
            if (next.Status == RepairStatus.Delivered && RepairBalance(companyCode, stored) > 0)
                return "قبل از تحویل، مانده صورتحساب را دریافت کنید.";
            if (stored.ReturnRequested && (next.Status == RepairStatus.WaitingPart || next.Status == RepairStatus.Repairing || next.Status == RepairStatus.Ready))
                return "مشتری ارجاع دستگاه را خواسته است. تعمیر انجام نمی‌شود.";
            if (next.Status == RepairStatus.Returning && stored.ReturnRequested)
            {
                if (stored.Charges > 0 && stored.SaleInvoiceId <= 0)
                    return "اول صورتحساب حمل رفت و برگشت را ثبت کنید.";
                if (RepairBalance(companyCode, stored) > 0)
                    return "قبل از برگشت دستگاه، هزینه حمل را دریافت کنید.";
            }
            if (stored.NeedsField && !stored.ReturnRequested && stored.QuoteState != RepairQuoteState.Approved && stored.SaleInvoiceId <= 0
                && (next.Status == RepairStatus.WaitingPart || next.Status == RepairStatus.Repairing || next.Status == RepairStatus.Ready))
                return stored.NeedsVisit
                    ? "برای اعزام کارشناس، اول مشتری باید هزینه را تأیید کند."
                    : "برای درخواست با پیک، اول مشتری باید هزینه را تأیید کند.";
            return null;
        }

        /// <summary>Assigns the courier and tells the customer a pickup window.</summary>
        public static string DispatchPickup(string companyCode, int id, string courierName, string courierMobile, string window)
        {
            var job = GetRepairJob(companyCode, id);
            if (job == null) return "برگه تعمیر پیدا نشد.";
            if (job.Status != RepairStatus.Requested && job.Status != RepairStatus.PickupAssigned)
                return "هماهنگی اعزام فقط پیش از شروع کار ممکن است.";
            var who = job.NeedsVisit ? "کارشناس" : "پیک";
            courierName = (courierName ?? "").Trim();
            if (courierName.Length < 2) return "نام " + who + " را بنویسید.";
            var phone = BookingTime.NormalizeMobile(courierMobile);
            if (phone == null) return "موبایل " + who + " معتبر نیست.";
            job.CourierName = Clip(courierName, 60);
            job.CourierMobile = phone;
            job.PickupWindow = Clip(window, 80);
            if (!job.NeedsVisit) job.Handover = "pickup";
            var previous = job.Status;
            job.Status = RepairStatus.PickupAssigned;
            StampRepair(job, previous);
            Update(NikooTables.RepairJobs, companyCode, job);
            return null;
        }

        /// <summary>Moves the job one allowed step. Delivery stays closed until the invoice is settled.</summary>
        public static string AdvanceRepair(string companyCode, int id, RepairStatus to)
        {
            var job = GetRepairJob(companyCode, id);
            if (job == null) return "برگه تعمیر پیدا نشد.";
            if (job.Status == to) return null;
            var error = GuardRepairMove(companyCode, job, new RepairJob
            {
                Status = to,
                LaborAmount = job.LaborAmount,
                TravelAmount = job.TravelAmount,
                Parts = job.Parts
            });
            if (error != null) return error;
            var previous = job.Status;
            job.Status = to;
            if (to == RepairStatus.QuoteSent && !job.ReturnRequested && job.QuoteState != RepairQuoteState.Approved)
                job.QuoteState = RepairQuoteState.Pending;
            StampRepair(job, previous);
            Update(NikooTables.RepairJobs, companyCode, job);
            return null;
        }

        /// <summary>The customer accepts the quote. The shop posts the invoice after a yes.</summary>
        public static string DecideRepairQuote(string companyCode, string code, string mobile, bool approve)
        {
            var job = FindRepairForCustomer(companyCode, code, mobile);
            if (job == null) return "برگه پیدا نشد.";
            if (job.ReturnRequested) return "برای این دستگاه ارجاع ثبت شده است.";
            if (job.SaleInvoiceId > 0) return "این هزینه قبلاً در حسابداری ثبت شده است.";
            if (job.Status != RepairStatus.QuoteSent && job.QuoteState != RepairQuoteState.Pending
                && job.Status != RepairStatus.Diagnosing && job.Status != RepairStatus.Received)
                return "پیش‌فاکتوری برای تأیید نیست.";
            if (job.Charges <= 0) return "هنوز هزینه‌ای برای تأیید نیست.";
            if (!approve) return "اگر تعمیر را نمی‌خواهید، ارجاع دستگاه را درخواست کنید.";
            job.QuoteState = RepairQuoteState.Approved;
            if (job.Status == RepairStatus.Diagnosing || job.Status == RepairStatus.Received)
                job.Status = RepairStatus.QuoteSent;
            Update(NikooTables.RepairJobs, companyCode, job);
            return null;
        }

        /// <summary>The customer refuses the repair. The device goes back and the return trip is charged again.</summary>
        public static string RequestRepairReturn(string companyCode, string code, string mobile)
        {
            var job = FindRepairForCustomer(companyCode, code, mobile);
            if (job == null) return "برگه پیدا نشد.";
            if (job.NeedsVisit) return "در تعمیر حضوری دستگاهی برای ارجاع نیست. هزینه را تأیید کنید یا با تعمیرگاه تماس بگیرید.";
            if (job.SaleInvoiceId > 0) return "این هزینه قبلاً در حسابداری ثبت شده است.";
            if (job.ReturnRequested) return null;
            if (job.Charges <= 0) return "هنوز هزینه‌ای برای تصمیم نیست.";
            if (job.Status != RepairStatus.QuoteSent && job.QuoteState != RepairQuoteState.Pending
                && job.Status != RepairStatus.Diagnosing && job.Status != RepairStatus.Received)
                return "پیش‌فاکتوری برای تصمیم نیست.";
            job.QuoteState = RepairQuoteState.Declined;
            job.ReturnRequested = true;
            job.ReturnShipping = Math.Max(0, job.TravelAmount);
            job.ReturnByCourier = true;
            if (job.Status == RepairStatus.Diagnosing || job.Status == RepairStatus.Received)
                job.Status = RepairStatus.QuoteSent;
            Update(NikooTables.RepairJobs, companyCode, job);
            return null;
        }

        /// <summary>Remembers the customer's choice and the amount, without starting the repair or the return.</summary>
        public static string StageRepairPayment(string companyCode, string code, string mobile, bool returning, out RepairJob job, out long amount)
        {
            amount = 0;
            job = FindRepairForCustomer(companyCode, code, mobile);
            if (job == null) return "برگه پیدا نشد.";
            if (job.SaleInvoiceId > 0 || job.QuoteState == RepairQuoteState.Approved || job.ReturnRequested)
                return "برای این برگه قبلاً تصمیم ثبت شده است.";
            if (returning && job.NeedsVisit) return "در تعمیر در محل دستگاهی برای ارجاع نیست. هزینه را تأیید کنید یا با تعمیرگاه تماس بگیرید.";
            if (job.Status != RepairStatus.QuoteSent && job.QuoteState != RepairQuoteState.Pending
                && job.Status != RepairStatus.Diagnosing && job.Status != RepairStatus.Received)
                return "پیش‌فاکتوری برای تصمیم نیست.";
            amount = returning ? job.ReturnPreview : job.Charges;
            if (!returning && amount <= 0) return "هنوز هزینه‌ای برای تأیید نیست.";
            job.PaymentDecision = returning ? "return" : "approve";
            job.PaymentAmount = amount;
            Update(NikooTables.RepairJobs, companyCode, job);
            return null;
        }

        public static void RememberRepairAuthority(string companyCode, int id, string authority)
        {
            var job = GetRepairJob(companyCode, id);
            if (job == null) return;
            job.PaymentAuthority = Clip(authority, 80);
            Update(NikooTables.RepairJobs, companyCode, job);
        }

        /// <summary>After the gateway confirms the money: the repair starts, or the device is sent back. A walk-in return has nothing to pay.</summary>
        public static string ApplyPaidRepairDecision(string companyCode, int id, long? refId, string gatewayMessage)
        {
            var job = GetRepairJob(companyCode, id);
            if (job == null) return "برگه پیدا نشد.";
            if (string.IsNullOrEmpty(job.PaymentDecision) && (job.QuoteState == RepairQuoteState.Approved || job.ReturnRequested))
                return null;
            var decision = job.PaymentDecision;
            if (decision != "approve" && decision != "return") return "تصمیم پرداخت پیدا نشد.";
            var previous = job.Status;
            if (decision == "return")
            {
                job.QuoteState = RepairQuoteState.Declined;
                job.ReturnRequested = true;
                job.ReturnShipping = job.BringsInPerson ? 0 : Math.Max(0, job.TravelDue);
                job.ReturnByCourier = !job.BringsInPerson;
                if (job.BringsInPerson) job.TravelAmount = 0;
                job.Status = job.BringsInPerson ? RepairStatus.Ready : RepairStatus.Returning;
            }
            else
            {
                job.QuoteState = RepairQuoteState.Approved;
                if (job.BringsInPerson) job.TravelAmount = 0;
                job.Status = RepairStatus.Repairing;
            }
            job.PaymentDecision = null;
            if (refId.HasValue)
                job.AdminNote = Clip(((job.AdminNote ?? "") + " پرداخت آنلاین " + refId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Trim(), 1000);
            else if (!string.IsNullOrWhiteSpace(gatewayMessage))
                job.AdminNote = Clip(((job.AdminNote ?? "") + " " + gatewayMessage).Trim(), 1000);
            StampRepair(job, previous);
            Update(NikooTables.RepairJobs, companyCode, job);

            if (job.Charges <= 0) return null;
            var invoiceError = PostRepairInvoice(companyCode, id);
            if (invoiceError != null) return invoiceError;
            var fresh = GetRepairJob(companyCode, id);
            var due = RepairBalance(companyCode, fresh);
            if (due <= 0) return null;
            var banks = GetBanks(companyCode);
            var gateway = banks.FirstOrDefault(b => b.IsSiteGateway && b.IsActive);
            var only = banks.Where(b => b.IsActive).ToList();
            var bank = gateway ?? (only.Count == 1 ? only[0] : null);
            var payError = RecordRepairPayment(companyCode, id, due, bank == null ? PayWith.Cash : PayWith.Bank, bank == null ? 0 : bank.Id);
            if (payError != null && payError.Contains("کامل دریافت")) return null;
            return payError;
        }

        /// <summary>
        /// Posts one sales invoice: labor to repair income, travel to shipping income, stocked parts through the warehouse.
        /// The whole bill stays on the customer until a payment is taken.
        /// </summary>
        public static string PostRepairInvoice(string companyCode, int id)
        {
            var job = GetRepairJob(companyCode, id);
            if (job == null) return "برگه تعمیر پیدا نشد.";
            if (job.SaleInvoiceId > 0) return null;
            if (job.Status == RepairStatus.Cancelled) return "برگه لغوشده صورتحساب ندارد.";
            if (job.Charges <= 0) return "اجرت، قطعه یا ایاب و ذهاب را ثبت کنید.";
            if (!job.ReturnRequested && job.NeedsField && job.QuoteState != RepairQuoteState.Approved)
                return "اول مشتری باید پیش‌فاکتور را تأیید کند.";

            var party = PartyByName(companyCode, string.IsNullOrWhiteSpace(job.CustomerName) ? "مشتری تعمیر" : job.CustomerName, PartyKind.Customer);
            if (party == null) return "طرف حساب مشتری ساخته نشد.";
            if (string.IsNullOrWhiteSpace(party.Mobile) && !string.IsNullOrWhiteSpace(job.CustomerMobile))
            {
                party.Mobile = job.CustomerMobile;
                SaveParty(companyCode, party);
            }

            var lines = new List<StoreSaleLine>();
            if (job.ReturnRequested)
            {
                if (job.TravelDue > 0)
                {
                    lines.Add(new StoreSaleLine
                    {
                        Title = "حمل رفت",
                        Qty = 1,
                        UnitPrice = job.TravelDue,
                        IsShipping = true
                    });
                }
                if (!job.BringsInPerson && job.ReturnShipping > 0)
                {
                    lines.Add(new StoreSaleLine
                    {
                        Title = "حمل برگشت",
                        Qty = 1,
                        UnitPrice = job.ReturnShipping,
                        IsShipping = true
                    });
                }
            }
            else
            {
                if (job.LaborAmount > 0)
                {
                    lines.Add(new StoreSaleLine
                    {
                        Title = Clip("اجرت تعمیر " + job.DeviceLabel, 120),
                        Qty = 1,
                        UnitPrice = job.LaborAmount
                    });
                }
                if (!job.BringsInPerson && job.TravelAmount > 0)
                {
                    lines.Add(new StoreSaleLine
                    {
                        Title = "ایاب و ذهاب",
                        Qty = 1,
                        UnitPrice = job.TravelAmount,
                        IsShipping = true
                    });
                }
                foreach (var part in job.Parts ?? new List<RepairPart>())
                {
                    lines.Add(new StoreSaleLine
                    {
                        Title = part.Title,
                        Qty = part.Qty,
                        UnitPrice = part.UnitPrice,
                        GoodsId = part.GoodsId
                    });
                }
            }

            var invoice = new StoreSalesInvoice
            {
                InvoiceDate = DateTime.Today,
                PartyId = party.Id,
                PayWith = PayWith.Credit,
                Note = "برگه تعمیر " + job.Code,
                Lines = lines
            };
            var error = SaveSale(companyCode, invoice);
            if (error != null) return error;

            job = GetRepairJob(companyCode, id);
            if (job == null || job.SaleInvoiceId > 0) return null;
            job.SaleInvoiceId = invoice.Id;
            if (!job.ReturnRequested && job.QuoteState != RepairQuoteState.Approved) job.QuoteState = RepairQuoteState.Approved;
            Update(NikooTables.RepairJobs, companyCode, job);
            return null;
        }

        /// <summary>The steps the desk can take from here, in the order the buttons should show them.</summary>
        public static RepairStatus[] NextMoves(RepairJob job)
        {
            if (job == null) return new RepairStatus[0];
            var order = new[]
            {
                RepairStatus.PickedUp, RepairStatus.Received, RepairStatus.Diagnosing, RepairStatus.QuoteSent,
                RepairStatus.WaitingPart, RepairStatus.Repairing, RepairStatus.Ready, RepairStatus.Returning,
                RepairStatus.Delivered, RepairStatus.Cancelled
            };
            return order.Where(status => CanAdvance(job, status)).ToArray();
        }

        /// <summary>
        /// Cash or bank against the customer's receivable, after the sales invoice exists.
        /// </summary>
        public static string RecordRepairPayment(string companyCode, int jobId, long amount, PayWith payWith, int bankId)
        {
            if (amount <= 0) return "مبلغ دریافت باید بیشتر از صفر باشد.";
            RepairJob job;
            lock (LockFor(companyCode))
            {
                job = GetRepairJob(companyCode, jobId);
                if (job == null) return "برگه تعمیر پیدا نشد.";
                if (job.Status == RepairStatus.Cancelled) return "برگه لغوشده قابل تسویه نیست.";
            }

            if (job.SaleInvoiceId <= 0) return "اول صورتحساب این برگه را در حسابداری ثبت کنید.";
            var due = RepairBalance(companyCode, job);
            if (due <= 0) return "مبلغ این برگه کامل دریافت شده است.";
            if (amount > due) amount = due;

            EnsureAccounts(companyCode);
            var receivable = AccountByCode(companyCode, AccountCodes.Receivable);
            if (receivable == null) return "سرفصل حساب‌های دریافتنی پیدا نشد.";
            var party = PartyByName(companyCode, string.IsNullOrWhiteSpace(job.CustomerName) ? "مشتری تعمیر" : job.CustomerName, PartyKind.Customer);
            var cash = new StoreCashTransaction
            {
                Direction = CashDirection.In,
                Amount = amount,
                PayWith = payWith == PayWith.Credit ? PayWith.Cash : payWith,
                BankId = bankId,
                AccountId = receivable.Id,
                PartyId = party == null ? 0 : party.Id,
                PartyName = job.CustomerName,
                Note = "تعمیر " + job.Code + " — " + job.DeviceLabel,
                TxDate = DateTime.Today
            };
            var error = SaveCash(companyCode, cash);
            if (error != null) return error;

            lock (LockFor(companyCode))
            {
                job = GetRepairJob(companyCode, jobId);
                if (job == null) return null;
                job.PaidAmount += amount;
                if (!job.PaidAt.HasValue) job.PaidAt = DateTime.Now;
                Update(NikooTables.RepairJobs, companyCode, job);
            }
            return null;
        }

        public static void MarkRepairSms(string companyCode, int id, RepairStatus status)
        {
            var job = GetRepairJob(companyCode, id);
            if (job == null || job.Status != status) return;
            job.LastSmsStatus = status.ToString();
            Update(NikooTables.RepairJobs, companyCode, job);
        }

        public static List<RepairPart> CleanParts(IList<string> titles, IList<string> qtys, IList<string> prices, IList<string> goodsIds)
        {
            var parts = new List<RepairPart>();
            var count = Math.Max(titles == null ? 0 : titles.Count, goodsIds == null ? 0 : goodsIds.Count);
            for (var i = 0; i < count; i++)
            {
                var title = (At(titles, i) ?? "").Trim();
                int goodsId;
                if (!int.TryParse(At(goodsIds, i), out goodsId) || goodsId < 0) goodsId = 0;
                if (title.Length == 0 && goodsId == 0) continue;
                if (title.Length == 0) title = "قطعه";
                if (title.Length > 120) title = title.Substring(0, 120);
                int qty;
                if (!int.TryParse(At(qtys, i), out qty) || qty < 1) qty = 1;
                if (qty > 999) qty = 999;
                long price;
                long.TryParse((At(prices, i) ?? "").Replace(",", ""), out price);
                if (price < 0) price = 0;
                parts.Add(new RepairPart { Title = title, Qty = qty, UnitPrice = price, GoodsId = goodsId });
            }
            return parts;
        }

        private static string At(IList<string> values, int index) =>
            values != null && index < values.Count ? values[index] : null;

        private static void PrepareRepair(RepairJob job)
        {
            job.CustomerName = (job.CustomerName ?? "").Trim();
            job.CustomerMobile = BookingTime.NormalizeMobile(job.CustomerMobile) ?? "";
            job.DeviceType = RepairText.Device(job.DeviceType);
            job.Brand = Clip(job.Brand, 60);
            job.ModelName = Clip(job.ModelName, 60);
            job.Serial = Clip(job.Serial, 60);
            job.Color = Clip(job.Color, 40);
            job.Accessories = Clip(job.Accessories, 300);
            job.Problem = Clip(job.Problem, 1000);
            job.Diagnosis = Clip(job.Diagnosis, 1000);
            job.Technician = Clip(job.Technician, 60);
            job.AdminNote = Clip(job.AdminNote, 1000);
            job.Handover = string.Equals(job.Handover, "visit", StringComparison.OrdinalIgnoreCase) ? "visit"
                : string.Equals(job.Handover, "pickup", StringComparison.OrdinalIgnoreCase) ? "pickup" : "counter";
            if (job.Handover == "counter")
            {
                job.TravelAmount = 0;
                job.ReturnShipping = 0;
                job.ReturnByCourier = false;
                job.PickupAddress = null;
                job.PickupWindow = null;
            }
            job.PickupAddress = Clip(job.PickupAddress, 300);
            job.PickupWindow = Clip(job.PickupWindow, 80);
            job.CourierName = Clip(job.CourierName, 60);
            if (!string.IsNullOrWhiteSpace(job.CourierMobile))
            {
                var courierPhone = BookingTime.NormalizeMobile(job.CourierMobile);
                job.CourierMobile = courierPhone ?? Clip(job.CourierMobile, 20);
            }
            if (job.WarrantyDays < 0) job.WarrantyDays = 0;
            if (job.WarrantyDays > 3650) job.WarrantyDays = 3650;
            if (job.ReturnShipping < 0) job.ReturnShipping = 0;
            job.Parts = (job.Parts ?? new List<RepairPart>()).Where(p => p != null && !string.IsNullOrWhiteSpace(p.Title)).ToList();
            if (job.EstimateAmount < 0) job.EstimateAmount = 0;
            if (job.LaborAmount < 0) job.LaborAmount = 0;
            if (job.TravelAmount < 0) job.TravelAmount = 0;
        }

        private static string Clip(string text, int max)
        {
            text = (text ?? "").Trim();
            return text.Length <= max ? text : text.Substring(0, max);
        }

        private static void StampRepair(RepairJob job, RepairStatus? previous)
        {
            var changed = !previous.HasValue || previous.Value != job.Status;
            if (!changed) return;
            if (job.Status == RepairStatus.Received && !job.ReceivedAt.HasValue) job.ReceivedAt = DateTime.Now;
            if (job.Status == RepairStatus.PickedUp && !job.PickedUpAt.HasValue) job.PickedUpAt = DateTime.Now;
            if (job.Status == RepairStatus.Ready) job.ReadyAt = DateTime.Now;
            if (job.Status == RepairStatus.Delivered) job.DeliveredAt = DateTime.Now;
            if (job.Status == RepairStatus.Cancelled) job.CancelledAt = DateTime.Now;
            if (!previous.HasValue && job.Status == RepairStatus.Received && !job.ReceivedAt.HasValue)
                job.ReceivedAt = DateTime.Now;
        }

        private static bool CanAdvance(RepairJob job, RepairStatus to)
        {
            if (job == null) return false;
            if (to == RepairStatus.Cancelled) return job.IsOpen && job.SaleInvoiceId <= 0;
            switch (job.Status)
            {
                case RepairStatus.Requested: return !job.NeedsField && to == RepairStatus.Received;
                case RepairStatus.PickupAssigned: return to == RepairStatus.PickedUp;
                case RepairStatus.PickedUp: return job.NeedsVisit ? to == RepairStatus.Diagnosing : to == RepairStatus.Received;
                case RepairStatus.Received: return to == RepairStatus.Diagnosing;
                case RepairStatus.Diagnosing:
                    if (job.ReturnRequested) return to == RepairStatus.Returning || to == RepairStatus.QuoteSent;
                    if (job.QuoteState == RepairQuoteState.Approved)
                        return to == RepairStatus.WaitingPart || to == RepairStatus.Repairing || to == RepairStatus.QuoteSent;
                    return to == RepairStatus.QuoteSent
                        || (!job.NeedsField && (to == RepairStatus.WaitingPart || to == RepairStatus.Repairing));
                case RepairStatus.QuoteSent:
                    if (job.ReturnRequested) return to == RepairStatus.Returning;
                    return to == RepairStatus.Diagnosing
                        || (job.QuoteState == RepairQuoteState.Approved && (to == RepairStatus.WaitingPart || to == RepairStatus.Repairing));
                case RepairStatus.WaitingPart: return to == RepairStatus.Repairing;
                case RepairStatus.Repairing:
                    return to == RepairStatus.WaitingPart || (job.NeedsVisit ? to == RepairStatus.Delivered : to == RepairStatus.Ready);
                case RepairStatus.Ready:
                    return to == RepairStatus.Delivered
                        || (job.NeedsPickup && job.ReturnByCourier && to == RepairStatus.Returning);
                case RepairStatus.Returning: return to == RepairStatus.Delivered;
                default: return false;
            }
        }

        private static string NormalizeRepairCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var chars = new char[code.Length];
            var n = 0;
            foreach (var ch in code.Trim())
            {
                char digit;
                if (ch >= '۰' && ch <= '۹') digit = (char)('0' + (ch - '۰'));
                else if (ch >= '٠' && ch <= '٩') digit = (char)('0' + (ch - '٠'));
                else if (char.IsLetterOrDigit(ch)) digit = char.ToUpperInvariant(ch);
                else continue;
                chars[n++] = digit;
            }
            if (n < 4 || n > 12) return null;
            return new string(chars, 0, n);
        }

        /// <summary>Five digits, easy to read back on the phone. Older letter codes still match.</summary>
        private static string NewRepairCode(string companyCode)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[4];
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    rng.GetBytes(bytes);
                    var code = (BitConverter.ToUInt32(bytes, 0) % 90000u + 10000u).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (GetRepairByCode(companyCode, code) == null) return code;
                }
                rng.GetBytes(bytes);
                return (BitConverter.ToUInt32(bytes, 0) % 900000u + 100000u).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Gives existing repair shops the company intro the public homepage now shows.</summary>
        public static void ApplyRepairIntro()
        {
            const string oldAbout = "تعمیرگاه همراه دستگاه را پذیرش می‌کند، وضعیت کار را به مشتری نشان می‌دهد و اجرت و قطعه را موقع تحویل تسویه می‌کند.";
            const string about = "تعمیرگاه همراه از سال ۱۳۹۲ موبایل، لپ‌تاپ، لوازم خانگی و خودرو را تعمیر می‌کند. "
                + "دستگاه را حضوری بیاورید، با پیک بفرستید، یا از کارشناس بخواهید به محل شما بیاید. "
                + "قبل از شروع، هزینه را می‌بینید و با کد پیگیری هر مرحله را دنبال می‌کنید.";
            foreach (var tenant in GetTenants().Where(t => string.Equals(t.StoreType, "repair", StringComparison.OrdinalIgnoreCase)))
            {
                var settings = GetSettings(tenant.CompanyCode);
                var changed = false;
                if (string.IsNullOrWhiteSpace(settings.About) || settings.About.Trim() == oldAbout)
                {
                    settings.About = about;
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(settings.OpeningHours))
                {
                    settings.OpeningHours = "شنبه تا پنجشنبه ۹ تا ۲۰";
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(settings.Address) || settings.Address.Trim() == "تهران، نارمک")
                {
                    settings.Address = "تهران، نارمک، خیابان گلستان، پلاک ۱۸";
                    changed = true;
                }
                if (changed) SaveSettings(settings);

                var page = GetPages(tenant.CompanyCode).FirstOrDefault(p => p.Slug == "about");
                if (page != null && !string.IsNullOrEmpty(page.BodyHtml) && page.BodyHtml.Contains(oldAbout))
                {
                    page.BodyHtml = page.BodyHtml.Replace(oldAbout, about);
                    SavePage(tenant.CompanyCode, page);
                }
            }
        }
    }
}
