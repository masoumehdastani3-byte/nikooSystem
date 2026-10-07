using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NikooSystem.Models.Store
{
    public class TenantInfo
    {
        public string CompanyCode { get; set; }
        public string Name { get; set; }
        public string CustomDomain { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class StoreSettings
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
        public string LayoutStyle { get; set; } = "organic"; // organic | fashion | minimal | marketplace | boutique
        public string HomeSectionsJson { get; set; } // ordered section keys
        public string AdminUsername { get; set; } = "admin";
        public string AdminPasswordHash { get; set; }
        public string ZarinPalMerchantId { get; set; }
        public bool ZarinPalSandbox { get; set; } = true;
        public bool EnablePayment { get; set; } = true;

        /// <summary>کد HTML اینماد (کپی‌شده از پنل اینماد)</summary>
        public string EnamadHtml { get; set; }

        public string InstagramUrl { get; set; }
        public string TelegramUrl { get; set; }
        public string WhatsAppUrl { get; set; }
        public string LinkedInUrl { get; set; }
        public string TwitterUrl { get; set; }
        public string FacebookUrl { get; set; }
        public string AparatUrl { get; set; }
        public string YouTubeUrl { get; set; }
    }

    public class StoreCategory
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class StoreAttributeDef
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

    public class StoreProduct
    {
        public int Id { get; set; }
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
        public int SoldCount { get; set; }
        public int ViewCount { get; set; }
        public List<StoreProductImage> Images { get; set; } = new List<StoreProductImage>();
        public List<StoreProductAttribute> Attributes { get; set; } = new List<StoreProductAttribute>();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class StoreProductReview
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

    public class StoreMenuItem
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

    public class StorePage
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string BodyHtml { get; set; }
        public string LayoutBlocksJson { get; set; }
        public bool ShowInMenu { get; set; }
        public bool IsPublished { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class StoreGalleryItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ThumbUrl { get; set; }
        public string LargeUrl { get; set; }
        public string Album { get; set; } = "عمومی";
        public int SortOrder { get; set; }
    }

    public class StoreCustomer
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

    public class StoreOrderItem
    {
        public int ProductId { get; set; }
        public string Title { get; set; }
        public long UnitPrice { get; set; }
        public int Quantity { get; set; }
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

    public class StoreOrder
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
}
