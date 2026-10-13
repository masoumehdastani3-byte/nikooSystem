using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace NikooSystem.Models.Store
{
    /// <summary>A row of a per-store table, keyed by (CompanyCode, Id).</summary>
    public interface IStoreEntity
    {
        int Id { get; set; }
    }

    /// <summary>Categories and menu items: rows that hang under a parent row of the same table.</summary>
    public interface ITreeNode : IStoreEntity
    {
        int? ParentId { get; }
        string Title { get; }
    }

    public class TenantInfo
    {
        public string CompanyCode { get; set; }
        public string Name { get; set; }
        public string CustomDomain { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>fashion | grocery | digital | general</summary>
        public string StoreType { get; set; }
        public string OwnerName { get; set; }
        public string OwnerMobile { get; set; }
        public string OwnerEmail { get; set; }

        public bool IsTrial { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        /// <summary>The store has paid for a subscription at least once (it is no longer a trial).</summary>
        public bool IsPaid { get; set; }
        /// <summary>The last subscription payment.</summary>
        public DateTime? PaidAt { get; set; }

        /// <summary>End of the paid period; the owner renews for 3, 6 or 12 months at a time before it.</summary>
        public DateTime? SubscriptionEndsAt { get; set; }
        /// <summary>Length in months of the last period paid for.</summary>
        public int SubscriptionMonths { get; set; }
        /// <summary>The last Bale reminder to renew sent to the owner.</summary>
        public DateTime? RenewalRemindedAt { get; set; }

        /// <summary>Upload space bought by the owner or given by Nikoo's admin, on top of the base space of every store.</summary>
        public int StorageExtraMb { get; set; }

        /// <summary>When <see cref="CustomDomain"/> was entered; the domain fields below start over whenever it changes.</summary>
        public DateTime? DomainSetAt { get; set; }
        /// <summary>The first check that found the domain pointing at Nikoo's server (support was told to finish the connection).</summary>
        public DateTime? DomainDnsOkAt { get; set; }
        /// <summary>Support has issued the SSL certificate: the domain becomes the store's public address.</summary>
        public DateTime? DomainActivatedAt { get; set; }
        /// <summary>The last DNS check (JSON of <c>DomainCheckResult</c>).</summary>
        public string DomainCheck { get; set; }
        /// <summary>The one Bale reminder sent to an owner whose DNS still points elsewhere a day later.</summary>
        public DateTime? DomainRemindedAt { get; set; }

        /// <summary>Days after the end of a paid period in which the site still works while the owner renews.</summary>
        public const int RenewalGraceDays = 7;

        /// <summary>How long before the end of a paid period the panel starts asking for renewal.</summary>
        public const int RenewalNoticeDays = 14;

        public bool IsTrialExpired()
        {
            return IsTrial && !IsPaid && TrialEndsAt.HasValue && TrialEndsAt.Value <= DateTime.Now;
        }

        public int TrialDaysLeft()
        {
            if (!IsTrial || IsPaid || !TrialEndsAt.HasValue) return 0;
            return Math.Max(0, (int)Math.Ceiling((TrialEndsAt.Value - DateTime.Now).TotalDays));
        }

        public bool IsSubscriptionExpired()
        {
            return IsPaid && SubscriptionEndsAt.HasValue && SubscriptionEndsAt.Value <= DateTime.Now;
        }

        /// <summary>The grace days are over too: the site is closed (its data is kept) and the panel only offers renewal.</summary>
        public bool IsSubscriptionLocked()
        {
            return IsPaid && SubscriptionEndsAt.HasValue && SubscriptionEndsAt.Value.AddDays(RenewalGraceDays) <= DateTime.Now;
        }

        /// <summary>The paid period ends within <see cref="RenewalNoticeDays"/> days or has ended.</summary>
        public bool IsRenewalDue()
        {
            return IsPaid && SubscriptionEndsAt.HasValue && SubscriptionEndsAt.Value <= DateTime.Now.AddDays(RenewalNoticeDays);
        }

        public int SubscriptionDaysLeft()
        {
            if (!IsPaid || !SubscriptionEndsAt.HasValue) return 0;
            return Math.Max(0, (int)Math.Ceiling((SubscriptionEndsAt.Value - DateTime.Now).TotalDays));
        }

        /// <summary>When an expired subscription closes the site.</summary>
        public DateTime? ClosesAt()
        {
            return SubscriptionEndsAt?.AddDays(RenewalGraceDays);
        }

        /// <summary>Days left before an expired subscription closes the site.</summary>
        public int GraceDaysLeft()
        {
            if (!IsSubscriptionExpired()) return 0;
            return Math.Max(0, (int)Math.Ceiling((ClosesAt().Value - DateTime.Now).TotalDays));
        }

        public DomainState DomainStatus()
        {
            if (string.IsNullOrWhiteSpace(CustomDomain)) return DomainState.None;
            if (DomainActivatedAt.HasValue) return DomainState.Active;
            return DomainDnsOkAt.HasValue ? DomainState.WaitingSupport : DomainState.WaitingDns;
        }

        /// <summary>The custom domain once support has finished connecting it; null before that.</summary>
        public string ActiveDomain()
        {
            return DomainStatus() == DomainState.Active ? CustomDomain.Trim() : null;
        }
    }

    /// <summary>Where connecting a store's own domain stands.</summary>
    public enum DomainState
    {
        None,
        /// <summary>The owner has to point the domain's DNS at Nikoo's server.</summary>
        WaitingDns,
        /// <summary>DNS is right; Nikoo's support has to add the site binding and the SSL certificate.</summary>
        WaitingSupport,
        Active
    }

    public class StoreSettings : IVerifiedSite
    {
        public string CompanyCode { get; set; }
        public string StoreName { get; set; }
        public string Tagline { get; set; }
        public string About { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string LogoUrl { get; set; }
        public string FaviconUrl { get; set; }
        public string PrimaryColor { get; set; } = "#0d6efd";
        public string SecondaryColor { get; set; } = "#111827";
        public string HeroTitle { get; set; }
        public string HeroSubtitle { get; set; }
        public string HeroImageUrl { get; set; }
        public string FooterText { get; set; }
        /// <summary>لینک «ساخته شده با نیکو سیستم» در فوتر نمایش داده نشود</summary>
        public bool HidePoweredBy { get; set; }
        public string LayoutStyle { get; set; } = "organic"; // organic | fashion | minimal | marketplace | boutique
        /// <summary>چیدمان بخش‌های صفحه اول و گزینه‌های صفحه محصول؛ null = پیش‌فرض قالب</summary>
        public StorePageLayout PageLayout { get; set; }
        public string AdminUsername { get; set; } = "admin";
        public string AdminPasswordHash { get; set; }
        public string ZarinPalMerchantId { get; set; }
        public bool ZarinPalSandbox { get; set; } = true;
        public bool EnablePayment { get; set; } = true;

        public bool OnboardingCompleted { get; set; }

        /// <summary>آدرس عمومی فروشگاه (https) برای لینک محصول در ترب؛ اگر خالی باشد از دامنه اختصاصی یا مسیر Shop استفاده می‌شود.</summary>
        public string PublicStoreBaseUrl { get; set; }

        public bool EnableTorob { get; set; }

        /// <summary>توکن Bearer دریافتی از پشتیبانی ترب برای webhook به‌روزرسانی محصول</summary>
        public string TorobWebhookBearerToken { get; set; }

        /// <summary>کد HTML اینماد (کپی‌شده از پنل اینماد)؛ فقط شناسه و کد آن خوانده و نماد امن ساخته می‌شود (see <see cref="Services.SiteVerification"/>).</summary>
        public string EnamadHtml { get; set; }
        /// <summary>Enamad's code for &lt;meta name="enamad" content="…"&gt; on every page, which proves ownership of the domain.</summary>
        public string EnamadMeta { get; set; }
        /// <summary>A temporary title of the home page that a verifier (Enamad…) asked for; null = the usual title.</summary>
        public string VerifyHomeTitle { get; set; }
        /// <summary>JSON of the files served at the root of the site's domain for Enamad, Google… (see <see cref="Services.SiteVerification"/>).</summary>
        public string RootFiles { get; set; }

        /// <summary>عنوان صفحه اول در نتایج جستجو؛ خالی = نام و شعار سایت</summary>
        public string SeoTitle { get; set; }
        /// <summary>توضیح صفحه اول در نتایج جستجو؛ خالی = شعار یا متن درباره ما</summary>
        public string SeoDescription { get; set; }
        /// <summary>کد تأیید مالکیت گوگل سرچ کنسول (google-site-verification)</summary>
        public string GoogleVerification { get; set; }

        public string InstagramUrl { get; set; }
        public string TelegramUrl { get; set; }
        public string WhatsAppUrl { get; set; }
        public string LinkedInUrl { get; set; }
        public string TwitterUrl { get; set; }
        public string FacebookUrl { get; set; }
        public string AparatUrl { get; set; }
        public string YouTubeUrl { get; set; }

        /// <summary>هزینه ارسال کالای فیزیکی برای هر سفارش؛ ۰ یعنی ارسال رایگان</summary>
        public long ShippingCost { get; set; }
        /// <summary>سفارش‌های بالای این مبلغ ارسال رایگان دارند؛ ۰ یعنی همیشه هزینه ارسال گرفته می‌شود</summary>
        public long FreeShippingThreshold { get; set; }
        /// <summary>کالاهایی که موجودی‌شان به این عدد برسد در داشبورد هشدار دارند</summary>
        public int LowStockThreshold { get; set; } = 5;
        /// <summary>نظرهای خوانندگان پس از تأیید مدیر نمایش داده می‌شوند</summary>
        public bool ModerateComments { get; set; } = true;

        /// <summary>Booking sites: the step between offered start times, in minutes.</summary>
        public int BookingSlotMinutes { get; set; } = 30;
        /// <summary>Booking sites: how many days ahead customers can book.</summary>
        public int BookingDaysAhead { get; set; } = 30;
        /// <summary>Booking sites: the earliest bookable time is this many minutes from now.</summary>
        public int BookingMinNoticeMinutes { get; set; } = 60;
        /// <summary>Booking sites: percent of the price paid online when booking; 0 = no deposit.</summary>
        public int BookingDepositPercent { get; set; }
        /// <summary>Booking sites: customers may cancel up to this many hours before; 0 = only the owner cancels.</summary>
        public int BookingCancelHours { get; set; } = 24;
        /// <summary>Booking sites: a note shown on the booking page (what to bring, arrive early…).</summary>
        public string BookingNotice { get; set; }

        /// <summary>How images sit in the frames of cards and galleries: auto | cover | contain (see <see cref="StoreImageFit"/>).</summary>
        public string ImageFit { get; set; }

        public long ShippingFor(long subtotal) =>
            ShippingCost <= 0 || (FreeShippingThreshold > 0 && subtotal >= FreeShippingThreshold) ? 0 : ShippingCost;

        /// <summary>Copy of the scalar settings; PageLayout is shared with the original.</summary>
        public StoreSettings Clone()
        {
            return (StoreSettings)MemberwiseClone();
        }
    }

    public class StorePageLayout
    {
        /// <summary>به ترتیب نمایش؛ null یعنی بخش‌های پیش‌فرض قالب</summary>
        public List<StoreHomeSection> HomeSections { get; set; }

        public bool ShowRelatedProducts { get; set; } = true;
        public int RelatedProductsCount { get; set; } = 4;
        public bool ShowReviews { get; set; } = true;
        public bool ShowSpecifications { get; set; } = true;
        public bool ShowBuyPerks { get; set; } = true;
    }

    public class StoreHomeSection
    {
        /// <summary>برای بخش‌های قالب برابر Type است؛ برای بخش‌های ساخته‌شده توسط فروشنده یک شناسه تصادفی</summary>
        public string Id { get; set; }
        /// <summary>hero | features | categories | featured | promo | gallery | about | products | banner</summary>
        public string Type { get; set; }
        public bool IsVisible { get; set; } = true;
        public string Title { get; set; }
        public string Subtitle { get; set; }

        /// <summary>منبع کالاهای بخش products: collection | newest | bestseller | onsale | featured | category</summary>
        public string Source { get; set; }
        public int? CollectionId { get; set; }
        public int? CategoryId { get; set; }
        public int Count { get; set; } = 8;

        public string ImageUrl { get; set; }
        public string ButtonText { get; set; }
        public string LinkUrl { get; set; }

        /// <summary>
        /// ردیف‌های بخش‌های آمار، چرا ما، مراحل، نظر مشتریان و پرسش‌ها؛ هر خط یک ردیف و ستون‌ها با «|» جدا می‌شوند
        /// (<see cref="Helpers.HomeSections.ItemsOf"/>).
        /// </summary>
        public string Items { get; set; }
    }

    /// <summary>برچسب ویژه کالا مثل «روز دختر» یا «حراج یلدا»</summary>
    public class StoreCollection : IStoreEntity
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class StoreCategory : ITreeNode, ISeoFields
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public string SeoTitle { get; set; }
        public string SeoDescription { get; set; }
    }

    /// <summary>The title and description an owner can set for search results; empty = made from the item itself.</summary>
    public interface ISeoFields
    {
        string SeoTitle { get; set; }
        string SeoDescription { get; set; }
    }

    public class StoreAttributeDef : IStoreEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string InputType { get; set; } = "text"; // text | number | select
        public string Options { get; set; } // comma-separated for select
        public bool IsFilterable { get; set; }
        public int SortOrder { get; set; }
        /// <summary>گروه‌های کالایی که این خصوصیت به آن‌ها وصل است؛ خالی = همه گروه‌ها</summary>
        public List<int> CategoryIds { get; set; } = new List<int>();
    }

    public class StoreProductAttribute
    {
        public int AttributeId { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class StoreProductImage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ThumbUrl { get; set; }
        public string LargeUrl { get; set; }
        public string Alt { get; set; }
        public int SortOrder { get; set; }
    }

    public enum StoreProductType
    {
        Physical = 0,
        Ebook = 1,
        Course = 2,
        File = 3
    }

    /// <summary>فایل محافظت‌شده کالای دیجیتال (کتاب، ویدیوی جلسه، فایل دانلودی) که فقط خریداران به آن دسترسی دارند</summary>
    public class StoreProductFile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; }
        public string FileName { get; set; }
        public string StoredName { get; set; }
        public string ContentType { get; set; }
        public long Size { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }

    /// <summary>جلسه یک دوره آموزشی؛ ویدیو یا فایل آپلودشده است یا آدرس بیرونی</summary>
    public class StoreLesson
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 10);
        /// <summary>فصل یا سرفصلی که جلسه زیر آن نمایش داده می‌شود</summary>
        public string Section { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        /// <summary>مثل ۱۲:۳۰</summary>
        public string Duration { get; set; }
        public string FileId { get; set; }
        public string VideoUrl { get; set; }
        /// <summary>جلسه پیش‌نمایش که بدون خرید هم دیده می‌شود</summary>
        public bool IsFree { get; set; }
    }

    public class StoreProduct : IStoreEntity, ISeoFields
    {
        public string SeoTitle { get; set; }
        public string SeoDescription { get; set; }
        public int Id { get; set; }
        public StoreProductType ProductType { get; set; } = StoreProductType.Physical;
        public int CategoryId { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string ShortDescription { get; set; }
        public string Description { get; set; }
        public long Price { get; set; }
        public long? CompareAtPrice { get; set; }
        public int Stock { get; set; } = 100;
        public string Sku { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; }
        /// <summary>محصول نمونه‌ای که هنگام ساخت فروشگاه اضافه شده و مالک باید جایگزینش کند</summary>
        public bool IsSample { get; set; }
        public int SoldCount { get; set; }
        public int ViewCount { get; set; }
        public List<StoreProductImage> Images { get; set; } = new List<StoreProductImage>();
        public List<StoreProductAttribute> Attributes { get; set; } = new List<StoreProductAttribute>();
        public List<int> CollectionIds { get; set; } = new List<int>();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>نویسنده کتاب یا مدرس دوره</summary>
        public string Author { get; set; }
        /// <summary>ویدیو یا فایل معرفی که همه می‌بینند</summary>
        public string PreviewUrl { get; set; }
        public List<StoreProductFile> Files { get; set; } = new List<StoreProductFile>();
        public List<StoreLesson> Lessons { get; set; } = new List<StoreLesson>();

        /// <summary>کالای دانلودی یا دوره: موجودی و ارسال ندارد و فقط یک بار خریده می‌شود</summary>
        public bool IsDigital() => ProductType != StoreProductType.Physical;

        public bool IsOnSale() => CompareAtPrice.HasValue && CompareAtPrice.Value > Price;

        public int DiscountPercent() =>
            IsOnSale() ? (int)Math.Round(100.0 * (CompareAtPrice.Value - Price) / CompareAtPrice.Value) : 0;

        public bool InCollection(int collectionId) => CollectionIds != null && CollectionIds.Contains(collectionId);

        public StoreProductImage CoverImage() => Images?.OrderBy(i => i.SortOrder).FirstOrDefault();
    }

    public class StoreProductReview : IStoreEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int? CustomerId { get; set; }
        public string AuthorName { get; set; }
        public string AuthorMobile { get; set; }
        public int Rating { get; set; } = 5; // 1-5
        public string Comment { get; set; }
        public bool IsApproved { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class ProductRating
    {
        public static readonly ProductRating None = new ProductRating(0, 0);

        public ProductRating(double average, int count)
        {
            Average = average;
            Count = count;
        }

        public double Average { get; }
        public int Count { get; }
    }

    public class StoreMenuItem : ITreeNode
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string LinkType { get; set; } = "custom"; // custom | page | category | product
        public string LinkTargetId { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public string Location { get; set; } = "main"; // main | footer
    }

    public class StorePage : IStoreEntity, ISeoFields
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string BodyHtml { get; set; }
        public bool ShowInMenu { get; set; }
        public bool IsPublished { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string SeoTitle { get; set; }
        public string SeoDescription { get; set; }
    }

    public class StoreGalleryItem : IStoreEntity
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ThumbUrl { get; set; }
        public string LargeUrl { get; set; }
        public string Album { get; set; } = "عمومی";
        public int SortOrder { get; set; }
    }

    public class StoreCustomer : IStoreEntity
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Address { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class StoreCartItem
    {
        public int ProductId { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string ImageUrl { get; set; }
        public long UnitPrice { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>A visitor's cart in one store, identified by the cart cookie.</summary>
    public class StoreCart
    {
        public string CartId { get; set; }
        public string CompanyCode { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public List<StoreCartItem> Items { get; set; } = new List<StoreCartItem>();
    }

    public class StoreOrderItem
    {
        public int ProductId { get; set; }
        public string Title { get; set; }
        public long UnitPrice { get; set; }
        public int Quantity { get; set; }
        public bool IsDigital { get; set; }
    }

    public enum StoreOrderStatus
    {
        PendingPayment = 0,
        Paid = 1,
        Failed = 2,
        Processing = 3,
        Shipped = 4,
        Completed = 5,
        Cancelled = 6
    }

    public class StoreOrder : IStoreEntity
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerMobile { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerAddress { get; set; }
        public List<StoreOrderItem> Items { get; set; } = new List<StoreOrderItem>();
        public long Amount { get; set; }
        public StoreOrderStatus Status { get; set; } = StoreOrderStatus.PendingPayment;
        public string Authority { get; set; }
        public long? RefId { get; set; }
        public string PaymentMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? PaidAt { get; set; }

        /// <summary>جزء Amount است</summary>
        public long ShippingCost { get; set; }
        /// <summary>کد رهگیری مرسوله پست یا پیک</summary>
        public string TrackingCode { get; set; }
        public string AdminNote { get; set; }
        /// <summary>موجودی کالاهای این سفارش کم شده است (تا دوباره کم نشود)</summary>
        public bool StockDeducted { get; set; }

        public bool IsDigitalOnly() => Items != null && Items.Count > 0 && Items.All(i => i.IsDigital);

        /// <summary>پرداخت شده و لغو نشده؛ خریدار به کالاهای دیجیتال آن دسترسی دارد</summary>
        public static readonly StoreOrderStatus[] Fulfilled =
            { StoreOrderStatus.Paid, StoreOrderStatus.Processing, StoreOrderStatus.Shipped, StoreOrderStatus.Completed };
    }

    /// <summary>خبر، مقاله یا اطلاعیه سایت‌های محتوایی</summary>
    public class StorePost : IStoreEntity, ISeoFields
    {
        public string SeoTitle { get; set; }
        public string SeoDescription { get; set; }
        public int Id { get; set; }
        public int? CategoryId { get; set; }
        public string Title { get; set; }
        /// <summary>روتیتر خبر</summary>
        public string Kicker { get; set; }
        public string Slug { get; set; }
        /// <summary>لید یا خلاصه</summary>
        public string Summary { get; set; }
        public string BodyHtml { get; set; }
        public string CoverImageUrl { get; set; }
        public string Author { get; set; }
        public string Source { get; set; }
        /// <summary>برچسب‌ها، جداشده با ویرگول</summary>
        public string Tags { get; set; }
        public bool IsPublished { get; set; } = true;
        /// <summary>تیتر یک صفحه اول</summary>
        public bool IsFeatured { get; set; }
        /// <summary>در نوار خبر فوری</summary>
        public bool IsBreaking { get; set; }
        public bool AllowComments { get; set; } = true;
        /// <summary>مطلب نمونه‌ای که هنگام ساخت سایت اضافه شده و مالک باید جایگزینش کند</summary>
        public bool IsSample { get; set; }
        public DateTime PublishedAt { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public int ViewCount { get; set; }

        public IEnumerable<string> TagList() =>
            (Tags ?? "").Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => t.Length > 0);

        public bool IsLive() => IsPublished && PublishedAt <= DateTime.Now;
    }

    public class StorePostComment : IStoreEntity
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string AuthorName { get; set; }
        public string AuthorEmail { get; set; }
        public string Body { get; set; }
        public bool IsApproved { get; set; }
        /// <summary>پاسخ تحریریه زیر نظر نمایش داده می‌شود</summary>
        public string AdminReply { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>پیام فرم تماس با ما</summary>
    public class StoreContactMessage : IStoreEntity
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "نام الزامی است")]
        [StringLength(80)]
        public string FullName { get; set; }

        [EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
        [StringLength(120)]
        public string Email { get; set; }

        [RegularExpression(@"^0\d{10}$", ErrorMessage = "شماره تماس معتبر نیست")]
        public string Mobile { get; set; }

        [StringLength(120)]
        public string Subject { get; set; }

        [Required(ErrorMessage = "متن پیام الزامی است")]
        [StringLength(4000)]
        public string Message { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class StoreLoginVm
    {
        [Required(ErrorMessage = "نام کاربری الزامی است")]
        public string Username { get; set; }

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        public string Password { get; set; }
    }

    public class StoreRegisterVm
    {
        [Required(ErrorMessage = "نام الزامی است")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "موبایل الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "موبایل معتبر نیست")]
        public string Mobile { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [MinLength(6)]
        public string Password { get; set; }

        public string Address { get; set; }
    }

    public class StoreSignupVm
    {
        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
        [StringLength(80, ErrorMessage = "نام طولانی است")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "شماره موبایل الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست (مثال: 09121234567)")]
        public string Mobile { get; set; }

        [EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
        public string Email { get; set; }

        [Required(ErrorMessage = "نام فروشگاه الزامی است")]
        [StringLength(60, ErrorMessage = "نام فروشگاه حداکثر ۶۰ حرف باشد")]
        public string StoreName { get; set; }

        [Required(ErrorMessage = "آدرس فروشگاه الزامی است")]
        public string StoreCode { get; set; }

        public string StoreType { get; set; } = "general";

        /// <summary>قالب انتخاب‌شده در صفحه محصول؛ اگر برای نوع سایت نباشد قالب پیش‌فرض آن نوع استفاده می‌شود</summary>
        public string Template { get; set; }

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [MinLength(6, ErrorMessage = "رمز عبور حداقل ۶ کاراکتر باشد")]
        public string Password { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "پذیرش قوانین و مقررات الزامی است")]
        public bool AcceptTerms { get; set; }

        /// <summary>فیلد مخفی ضد ربات؛ انسان‌ها آن را خالی می‌گذارند</summary>
        public string Website { get; set; }
    }

    public class OwnerLoginVm
    {
        [Required(ErrorMessage = "شماره موبایل الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست")]
        public string Mobile { get; set; }

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        public string Password { get; set; }
    }

    public class SetupChecklistItem
    {
        public string Title { get; set; }
        public string Hint { get; set; }
        public bool Done { get; set; }
        public bool Optional { get; set; }
        public string Url { get; set; }
    }
}
