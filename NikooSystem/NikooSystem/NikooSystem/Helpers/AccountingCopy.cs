namespace NikooSystem.Helpers
{
    /// <summary>Kind-aware Persian labels for accounting screens (retail vs service businesses).</summary>
    public static class AccountingCopy
    {
        public static bool ServiceTone(SiteKindInfo kind) =>
            kind != null && (kind.IsBooking || kind.IsCampus);

        public static string SaleInvoice(SiteKindInfo kind) =>
            ServiceTone(kind) ? "فاکتور فروش و خدمت" : "فاکتور فروش";

        public static string SaleInvoices(SiteKindInfo kind) =>
            ServiceTone(kind) ? "فاکتورهای فروش و خدمت" : "فاکتورهای فروش";

        public static string Pos(SiteKindInfo kind) =>
            ServiceTone(kind) ? "صندوق فروش و دریافت" : "صندوق فروش";

        public static string Parties(SiteKindInfo kind) =>
            ServiceTone(kind) ? "طرف‌حساب‌ها" : "تامین‌کنندگان و مشتریان";

        public static string Inventory(SiteKindInfo kind) =>
            ServiceTone(kind) ? "لوازم و موجودی مصرفی" : "انبار";

        public static string LineColumn(SiteKindInfo kind) =>
            ServiceTone(kind) ? "موجودی (اختیاری)" : "کالا / موجودی";

        public static string NoStock(SiteKindInfo kind) =>
            ServiceTone(kind) ? "فقط درآمد (بدون انبار)" : "بدون کسر از موجودی";

        public static string LineHint(SiteKindInfo kind) =>
            ServiceTone(kind)
                ? "برای خدمت یا فروش متفرقه، شرح و مبلغ را بنویسید و موجودی را خالی بگذارید. اگر از انبار کم می‌شود، کالا را از فهرست انتخاب کنید."
                : "اگر موجودی کافی نباشد فاکتور ثبت نمی‌شود. ردیف بدون کالا فقط درآمد است و از انبار کم نمی‌شود.";

        public static string PosLead(SiteKindInfo kind) =>
            ServiceTone(kind)
                ? "فروش حضوری یا دریافت سریع: خدمت یا کالا را از فهرست بزنید، یا ردیف دستی با شرح و مبلغ اضافه کنید. نقد یا بانک را انتخاب کنید تا سند خودکار ثبت شود."
                : "برای فروش حضوری سریع: کالا را بزنید، تعداد را تنظیم کنید و با نقد یا بانک تسویه کنید. موجودی کم می‌شود و سند حسابداری خودکار ثبت می‌شود.";

        public static string PosEmptyCart => "هنوز ردیفی در سبد نیست.";

        public static string PosNeedLines => "حداقل یک ردیف فروش به سبد اضافه کنید.";

        public static string PosSearch(SiteKindInfo kind) =>
            ServiceTone(kind)
                ? "نام را بنویسید و Enter بزنید…"
                : "نام یا کد کالا را بنویسید و Enter بزنید…";

        public static string PosCatalogEmpty(SiteKindInfo kind) =>
            ServiceTone(kind)
                ? "فهرست کالا خالی است؛ از «ردیف دستی» پایین سبد برای ثبت خدمت یا مبلغ استفاده کنید."
                : "هنوز کالایی در فهرست محصولات یا انبار نیست. می‌توانید ردیف دستی هم اضافه کنید.";

        public static string PosBrowseHint => "۴۰ کالای پرفروش؛ بقیه را جستجو کنید یا گروه را بزنید.";

        public static string GuideTitle(SiteKindInfo kind)
        {
            if (kind == null) return "راهنمای حسابداری";
            if (kind.IsBooking) return "حسابداری نوبت‌دهی و دریافت";
            if (kind.IsCampus) return "حسابداری باشگاه و آموزشگاه";
            if (kind.IsDigital) return "حسابداری فروش دوره و فایل";
            return "حسابداری فروشگاه";
        }
    }
}
