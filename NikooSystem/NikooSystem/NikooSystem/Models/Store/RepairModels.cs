using System;
using System.Collections.Generic;
using System.Linq;

namespace NikooSystem.Models.Store
{
    /// <summary>Where a device is in the shop, from the customer's request to pickup.</summary>
    public enum RepairStatus
    {
        Requested,
        Received,
        Diagnosing,
        WaitingPart,
        Repairing,
        Ready,
        Delivered,
        Cancelled,
        /// <summary>A courier is assigned and a pickup window is set. Appended so older rows keep their numbers.</summary>
        PickupAssigned,
        /// <summary>The courier has the device and is bringing it to the shop.</summary>
        PickedUp,
        /// <summary>The shop sent a price and is waiting for the customer.</summary>
        QuoteSent,
        /// <summary>A courier is taking the repaired device back.</summary>
        Returning
    }

    public enum RepairQuoteState
    {
        None,
        Pending,
        Approved,
        Declined
    }

    /// <summary>One device left for repair. The customer follows it with the code and the mobile number.</summary>
    public class RepairJob : IStoreEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string CustomerName { get; set; }
        public string CustomerMobile { get; set; }
        /// <summary>One of <see cref="RepairText.DeviceTypes"/>.</summary>
        public string DeviceType { get; set; }
        public string Brand { get; set; }
        public string ModelName { get; set; }
        public string Serial { get; set; }
        public string Color { get; set; }
        /// <summary>What the customer left with the device (charger, case, SIM).</summary>
        public string Accessories { get; set; }
        public string Problem { get; set; }
        public string Diagnosis { get; set; }
        public RepairStatus Status { get; set; } = RepairStatus.Received;
        /// <summary>Quoted before the job is priced; used as the bill until labor or parts are entered.</summary>
        public long EstimateAmount { get; set; }
        public long LaborAmount { get; set; }
        /// <summary>Courier or on-site travel charged to the customer. Posted on the sales invoice as shipping income.</summary>
        public long TravelAmount { get; set; }
        public long PaidAmount { get; set; }
        public string Technician { get; set; }
        public DateTime? PromisedAt { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public DateTime? ReadyAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? PaidAt { get; set; }
        /// <summary>The status last told to the customer by SMS, so the same status is not sent twice.</summary>
        public string LastSmsStatus { get; set; }
        public string AdminNote { get; set; }
        /// <summary>online | counter</summary>
        public string Source { get; set; } = "counter";
        /// <summary>counter: the customer brings the device. pickup: a courier collects it. visit: an expert repairs it on site.</summary>
        public string Handover { get; set; } = "counter";
        public string PickupAddress { get; set; }
        public string PickupWindow { get; set; }
        public string CourierName { get; set; }
        public string CourierMobile { get; set; }
        public DateTime? PickedUpAt { get; set; }
        public bool ReturnByCourier { get; set; }
        /// <summary>The customer refused the repair and asked for the device to be sent back.</summary>
        public bool ReturnRequested { get; set; }
        /// <summary>The return trip, charged again on top of the outbound travel. Frozen when the customer asks for the device back.</summary>
        public long ReturnShipping { get; set; }
        public RepairQuoteState QuoteState { get; set; }
        /// <summary>The sales invoice posted for this job. Payments after that settle the receivable, not income again.</summary>
        public int SaleInvoiceId { get; set; }
        public int WarrantyDays { get; set; }
        public bool IsSample { get; set; }
        /// <summary>The gateway authority while the customer is paying to approve the repair or to send the device back.</summary>
        public string PaymentAuthority { get; set; }
        /// <summary>approve or return. Cleared once the payment is verified and the work starts.</summary>
        public string PaymentDecision { get; set; }
        public long PaymentAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<RepairPart> Parts { get; set; } = new List<RepairPart>();

        public long PartsTotal => (Parts ?? new List<RepairPart>()).Sum(p => p.Amount);

        /// <summary>Labor, parts and travel the customer pays. A walk-in has no travel. A return keeps only the transport.</summary>
        public long Charges => ReturnRequested
            ? (BringsInPerson ? 0 : Math.Max(0, TravelAmount) + Math.Max(0, ReturnShipping))
            : Math.Max(0, LaborAmount) + PartsTotal + TravelDue;

        /// <summary>What the customer owes: the entered charges, or the estimate while those are still empty. A return never falls back to the repair estimate.</summary>
        public long Bill => ReturnRequested ? Charges : (Charges > 0 ? Charges : Math.Max(0, EstimateAmount));

        /// <summary>The rows of the customer statement, in the order they should be read and printed.</summary>
        public List<RepairChargeLine> ChargeLines()
        {
            var lines = new List<RepairChargeLine>();
            if (ReturnRequested)
            {
                if (TravelDue > 0) lines.Add(new RepairChargeLine { Title = "حمل رفت", Qty = 1, UnitPrice = TravelDue });
                if (!BringsInPerson && ReturnShipping > 0) lines.Add(new RepairChargeLine { Title = "حمل برگشت", Qty = 1, UnitPrice = ReturnShipping });
                return lines;
            }
            if (LaborAmount > 0) lines.Add(new RepairChargeLine { Title = "اجرت تعمیر", Qty = 1, UnitPrice = LaborAmount });
            foreach (var part in Parts ?? new List<RepairPart>())
            {
                if (part == null || string.IsNullOrWhiteSpace(part.Title)) continue;
                lines.Add(new RepairChargeLine { Title = part.Title, Qty = part.Qty < 1 ? 1 : part.Qty, UnitPrice = part.UnitPrice });
            }
            if (TravelDue > 0) lines.Add(new RepairChargeLine { Title = "ایاب و ذهاب", Qty = 1, UnitPrice = TravelDue });
            if (lines.Count == 0 && EstimateAmount > 0)
                lines.Add(new RepairChargeLine { Title = "برآورد", Qty = 1, UnitPrice = EstimateAmount });
            return lines;
        }

        public long Due => Math.Max(0, Bill - PaidAmount);

        public string DeviceLabel
        {
            get
            {
                var bits = new[] { DeviceType, Brand, ModelName }.Where(s => !string.IsNullOrWhiteSpace(s));
                var text = string.Join(" ", bits);
                return text.Length == 0 ? "دستگاه" : text;
            }
        }

        public bool IsOpen => Status != RepairStatus.Delivered && Status != RepairStatus.Cancelled;

        public bool NeedsVisit => string.Equals(Handover, "visit", StringComparison.OrdinalIgnoreCase);

        public bool NeedsPickup => string.Equals(Handover, "pickup", StringComparison.OrdinalIgnoreCase);

        /// <summary>The customer brings the device to the shop, so travel is not charged.</summary>
        public bool BringsInPerson => string.Equals(Handover, "counter", StringComparison.OrdinalIgnoreCase);

        /// <summary>Travel the customer actually pays. Zero when they bring the device themselves.</summary>
        public long TravelDue => BringsInPerson ? 0 : Math.Max(0, TravelAmount);

        /// <summary>What a return would cost before it is requested: the outbound trip again, and nothing for a walk-in.</summary>
        public long ReturnPreview => BringsInPerson ? 0 : TravelDue * 2;

        /// <summary>Someone goes to the customer: a courier or an expert.</summary>
        public bool NeedsField => NeedsVisit || NeedsPickup;
    }

    /// <summary>A part on the job. GoodsId, when set, is issued from the warehouse by the sales invoice.</summary>
    public class RepairPart
    {
        public string Title { get; set; }
        public int Qty { get; set; } = 1;
        public long UnitPrice { get; set; }
        public int GoodsId { get; set; }
        public long Amount => (long)Math.Max(0, Qty) * Math.Max(0, UnitPrice);
    }

    /// <summary>One row on the repair statement the customer sees and prints.</summary>
    public class RepairChargeLine
    {
        public string Title { get; set; }
        public int Qty { get; set; } = 1;
        public long UnitPrice { get; set; }
        public long Amount => (long)Math.Max(0, Qty) * Math.Max(0, UnitPrice);
    }

    public static class RepairText
    {
        public static readonly string[] DeviceTypes = { "موبایل", "تبلت", "لپ‌تاپ", "لوازم خانگی", "خودرو", "سایر" };

        /// <summary>The same status reads differently when an expert is on site.</summary>
        public static string StepTitle(this RepairStatus status, RepairJob job)
        {
            if (job != null && job.ReturnRequested && job.BringsInPerson && status == RepairStatus.Ready)
                return "برای پس‌گرفتن حضوری آماده است";
            if (job != null && job.ReturnRequested)
            {
                if (status == RepairStatus.QuoteSent) return "ارجاع درخواست شد";
                if (status == RepairStatus.Returning) return "دستگاه در راه برگشت";
            }
            if (job != null && job.QuoteState == RepairQuoteState.Approved && status == RepairStatus.QuoteSent)
                return "هزینه تأیید شد";
            var visit = job != null && job.NeedsVisit;
            switch (status)
            {
                case RepairStatus.PickupAssigned: return visit ? "کارشناس هماهنگ شد" : "پیک هماهنگ شد";
                case RepairStatus.PickedUp: return visit ? "کارشناس در محل" : "پیک دستگاه را گرفت";
                case RepairStatus.Repairing: return visit ? "تعمیر در محل" : "در حال تعمیر";
                case RepairStatus.Delivered: return visit ? "کار در محل تمام شد" : "تحویل شد";
                default: return status.Title();
            }
        }

        public static string Title(this RepairStatus status)
        {
            switch (status)
            {
                case RepairStatus.Requested: return "درخواست ثبت شد";
                case RepairStatus.PickupAssigned: return "پیک هماهنگ شد";
                case RepairStatus.PickedUp: return "پیک دستگاه را گرفت";
                case RepairStatus.Received: return "رسید تعمیرگاه";
                case RepairStatus.Diagnosing: return "عیب‌یابی";
                case RepairStatus.QuoteSent: return "منتظر تأیید هزینه";
                case RepairStatus.WaitingPart: return "منتظر قطعه";
                case RepairStatus.Repairing: return "در حال تعمیر";
                case RepairStatus.Ready: return "آماده تحویل";
                case RepairStatus.Returning: return "پیک در راه بازگشت";
                case RepairStatus.Delivered: return "تحویل شد";
                default: return "لغو";
            }
        }

        public static string Badge(this RepairStatus status)
        {
            switch (status)
            {
                case RepairStatus.Ready: return "bg-success";
                case RepairStatus.Delivered: return "bg-secondary";
                case RepairStatus.Cancelled: return "bg-dark";
                case RepairStatus.WaitingPart:
                case RepairStatus.QuoteSent: return "bg-warning text-dark";
                case RepairStatus.Requested:
                case RepairStatus.PickupAssigned:
                case RepairStatus.PickedUp:
                case RepairStatus.Returning: return "bg-info text-dark";
                default: return "bg-primary";
            }
        }

        /// <summary>The steps the customer should see, in order. Status numbers are not this order.</summary>
        public static RepairStatus[] PathOf(RepairJob job)
        {
            var steps = new List<RepairStatus>();
            if (job != null && job.NeedsVisit)
            {
                steps.Add(RepairStatus.Requested);
                steps.Add(RepairStatus.PickupAssigned);
                steps.Add(RepairStatus.PickedUp);
                steps.Add(RepairStatus.Diagnosing);
                steps.Add(RepairStatus.QuoteSent);
                steps.Add(RepairStatus.WaitingPart);
                steps.Add(RepairStatus.Repairing);
                steps.Add(RepairStatus.Delivered);
                return FinishPath(steps, job);
            }
            if (job != null && job.NeedsPickup)
            {
                steps.Add(RepairStatus.Requested);
                steps.Add(RepairStatus.PickupAssigned);
                steps.Add(RepairStatus.PickedUp);
            }
            else if (job == null || job.Source == "online" || job.Status == RepairStatus.Requested)
                steps.Add(RepairStatus.Requested);
            steps.Add(RepairStatus.Received);
            steps.Add(RepairStatus.Diagnosing);
            steps.Add(RepairStatus.QuoteSent);
            steps.Add(RepairStatus.WaitingPart);
            steps.Add(RepairStatus.Repairing);
            steps.Add(RepairStatus.Ready);
            if (job != null && job.NeedsPickup && job.ReturnByCourier)
                steps.Add(RepairStatus.Returning);
            steps.Add(RepairStatus.Delivered);
            return FinishPath(steps, job);
        }

        /// <summary>A return skips the repair and sends the device back.</summary>
        private static RepairStatus[] FinishPath(List<RepairStatus> steps, RepairJob job)
        {
            if (job != null && job.ReturnRequested)
            {
                steps.Remove(RepairStatus.WaitingPart);
                steps.Remove(RepairStatus.Repairing);
                if (job.BringsInPerson)
                {
                    steps.Remove(RepairStatus.Returning);
                    if (!steps.Contains(RepairStatus.Ready))
                    {
                        var at = steps.IndexOf(RepairStatus.Delivered);
                        if (at < 0) steps.Add(RepairStatus.Ready);
                        else steps.Insert(at, RepairStatus.Ready);
                    }
                }
                else
                {
                    steps.Remove(RepairStatus.Ready);
                    if (!steps.Contains(RepairStatus.Returning))
                    {
                        var at = steps.IndexOf(RepairStatus.Delivered);
                        if (at < 0) steps.Add(RepairStatus.Returning);
                        else steps.Insert(at, RepairStatus.Returning);
                    }
                }
            }
            return steps.ToArray();
        }

        /// <summary>Which desk tab holds this step. The tabs follow the same order as <see cref="PathOf"/>.</summary>
        public static string StageOf(this RepairStatus status)
        {
            switch (status)
            {
                case RepairStatus.PickupAssigned:
                case RepairStatus.PickedUp:
                    return "field";
                case RepairStatus.Received:
                case RepairStatus.Diagnosing:
                case RepairStatus.Repairing:
                    return "work";
                case RepairStatus.QuoteSent:
                case RepairStatus.WaitingPart:
                    return "cost";
                case RepairStatus.Ready:
                case RepairStatus.Returning:
                case RepairStatus.Delivered:
                    return "done";
                default:
                    return "intake";
            }
        }

        /// <summary>The tab the desk opens on. A new job starts at intake.</summary>
        public static string OpenStage(this RepairJob job)
        {
            if (job == null || job.Id == 0) return "intake";
            var stage = job.Status.StageOf();
            return stage == "field" && !job.NeedsField ? "intake" : stage;
        }

        public static bool Reached(RepairJob job, RepairStatus step)
        {
            if (job == null) return false;
            var path = PathOf(job);
            var current = Array.IndexOf(path, job.Status);
            var at = Array.IndexOf(path, step);
            if (current < 0 || at < 0) return job.Status == step;
            return at <= current;
        }

        /// <summary>What the shop should do next. Empty when the job is finished.</summary>
        public static string NextStep(this RepairJob job)
        {
            if (job == null || job.Status == RepairStatus.Cancelled || job.Status == RepairStatus.Delivered) return "";
            if (job.ReturnRequested)
            {
                if (job.BringsInPerson && job.Status == RepairStatus.Ready) return "مشتری دستگاه را حضوری پس می‌گیرد. ایاب و ذهاب گرفته نشده است.";
                if (job.Charges > 0 && job.SaleInvoiceId <= 0) return "مشتری ارجاع دستگاه را خواسته است. صورتحساب فقط حمل رفت و برگشت است؛ آن را ثبت کنید.";
                if (job.Due > 0) return "هزینه حمل رفت و برگشت را بگیرید. دستگاه بعد از تسویه برمی‌گردد.";
                if (job.Status != RepairStatus.Returning) return "هزینه حمل تسویه شده است. برگشت دستگاه را راه بیندازید.";
                return "با رسیدن دستگاه به مشتری، «تحویل شد» را بزنید.";
            }
            if (job.NeedsVisit && job.Status == RepairStatus.Requested) return "نام کارشناس، موبایل و بازه حضور را بنویسید تا برای مشتری هماهنگ شود.";
            if (job.NeedsPickup && job.Status == RepairStatus.Requested) return "نام پیک، موبایل و بازه حضور را بنویسید تا برای مشتری هماهنگ شود.";
            if (job.Status == RepairStatus.Requested) return "وقتی مشتری دستگاه را به تعمیرگاه آورد، «رسید تعمیرگاه» را بزنید.";
            if (job.Status == RepairStatus.PickupAssigned) return job.NeedsVisit
                ? "وقتی کارشناس به محل رسید، «کارشناس در محل» را بزنید."
                : "وقتی پیک دستگاه را از مشتری گرفت، «پیک دستگاه را گرفت» را بزنید.";
            if (job.Status == RepairStatus.PickedUp) return job.NeedsVisit
                ? "عیب‌یابی در محل را شروع کنید."
                : "با رسیدن دستگاه به تعمیرگاه، «رسید تعمیرگاه» را بزنید.";
            if (job.Charges <= 0) return "بعد از عیب‌یابی، اجرت، قطعه و در صورت اعزام، ایاب و ذهاب را بنویسید و پیش‌فاکتور را برای مشتری بفرستید.";
            if (job.NeedsField && job.QuoteState == RepairQuoteState.Pending) return "پیش‌فاکتور برای مشتری رفته است. بعد از تأیید او، صورتحساب را در حسابداری ثبت کنید.";
            if (job.NeedsField && job.QuoteState != RepairQuoteState.Approved) return "پیش‌فاکتور را بفرستید. تعمیر بعد از تأیید مشتری در حسابداری ثبت می‌شود.";
            if (job.SaleInvoiceId <= 0) return "صورتحساب را در حسابداری ثبت کنید تا قطعه از انبار کم شود و طلب مشتری در دفتر بنشیند.";
            if (job.Status == RepairStatus.WaitingPart) return "با رسیدن قطعه، تعمیر را شروع کنید.";
            if (job.Status == RepairStatus.Repairing) return job.NeedsVisit
                ? "با پایان کار در محل، «کار در محل تمام شد» را بزنید."
                : "پایان تعمیر را با «آماده تحویل» علامت بزنید.";
            if (job.Due > 0) return "مانده را نقد یا بانک بگیرید. تحویل بعد از تسویه باز می‌شود.";
            if (job.NeedsPickup && job.ReturnByCourier && job.Status == RepairStatus.Ready) return "پیک برگشت را راه بیندازید.";
            if (job.Status == RepairStatus.Ready || job.Status == RepairStatus.Returning) return "با دادن دستگاه به مشتری، «تحویل شد» را بزنید.";
            return "مرحله بعد را از دکمه‌های همین صفحه انتخاب کنید.";
        }

        public static readonly Dictionary<string, string> HomeSamples = new Dictionary<string, string>
        {
            { "stats", "+۱۲ | سال تجربه\n+۸۰۰۰ | دستگاه تعمیرشده\n۹۰ روز | گارانتی تعمیر" },
            { "whyus", "قطعه اصلی | قطعه را از انبار خودمان برمی‌داریم و روی برگه می‌نویسیم\nهزینه شفاف | قبل از شروع تعمیر، مبلغ را می‌بینید و تأیید می‌کنید\nپیگیری ساده | با یک کد پنج‌رقمی هر مرحله را در سایت می‌بینید" },
            { "steps", "درخواست | پیک، کارشناس در محل، یا آوردن حضوری را انتخاب می‌کنید\nهماهنگی | زمان حضور را با شما تنظیم می‌کنیم\nتعمیر | بعد از تأیید هزینه، کار شروع می‌شود\nتحویل | دستگاه را پس می‌گیرید یا کار در محل تمام می‌شود" },
            { "reviews", "رضا محمدی | مشتری موبایل | باتری را همان روز عوض کردند و تا آخر با کد پیگیری در جریان بودم.\nسارا کریمی | مشتری لپ‌تاپ | کارشناس آمد دفتر، عیب را گفت و بعد از تأیید من تعمیر کرد." },
            { "faq", "دستگاه را چطور بسپارم؟ | سه راه دارید: پیک دستگاه را می‌برد، کارشناس به محل می‌آید، یا خودتان به تعمیرگاه می‌آورید.\nهزینه از کجا مشخص می‌شود؟ | بعد از عیب‌یابی پیش‌فاکتور در سایت می‌آید و تا تأیید نکنید تعمیر شروع نمی‌شود.\nچطور وضعیت را ببینم؟ | کد پنج‌رقمی و همان شماره موبایل را در صفحه پیگیری وارد کنید." }
        };

        public static string Device(string value)
        {
            var text = (value ?? "").Trim();
            return DeviceTypes.Contains(text) ? text : DeviceTypes[DeviceTypes.Length - 1];
        }
    }
}
