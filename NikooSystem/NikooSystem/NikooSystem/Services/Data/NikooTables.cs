using NikooSystem.Models;
using NikooSystem.Models.Store;

namespace NikooSystem.Services.Data
{
    /// <summary>
    /// Every table of the nikoo database. Columns are named after the model properties (see <see cref="EntityMap{T}"/>);
    /// a property added to a model becomes a new column on the next start.
    /// </summary>
    internal static class NikooTables
    {
        // ---- Platform: registered stores and their settings
        public static readonly EntityMap<TenantInfo> Tenants = new EntityMap<TenantInfo>("Tenants", "CompanyCode")
            .Text("CompanyCode", 40).Text("StoreType", 20).Text("CustomDomain", 253).Text("OwnerMobile", 20)
            .Index("IX_Tenants_CustomDomain", "CustomDomain")
            .Index("IX_Tenants_OwnerMobile", "OwnerMobile");

        public static readonly EntityMap<StoreSettings> Settings = new EntityMap<StoreSettings>("StoreSettings", "CompanyCode")
            .Text("CompanyCode", 40)
            .Ignore("PageLayout");

        /// <summary>StoreSettings.PageLayout; no row means the template defaults.</summary>
        public static readonly EntityMap<StorePageLayout> PageLayouts = new EntityMap<StorePageLayout>("StorePageLayouts", "CompanyCode")
            .References(Settings)
            .Child("HomeSections", "StoreHomeSections", "CompanyCode");

        // ---- Per-store data, keyed by (CompanyCode, Id)
        public static readonly EntityMap<StoreCategory> Categories = Store<StoreCategory>("Categories");

        public static readonly EntityMap<StoreAttributeDef> AttributeDefs = Store<StoreAttributeDef>("AttributeDefs")
            .Values("CategoryIds", "AttributeDefCategories", "AttributeDefId", "CategoryId");

        public static readonly EntityMap<StoreProduct> Products = Store<StoreProduct>("Products")
            .Text("Slug", 400)
            .Index("IX_Products_Slug", "CompanyCode", "Slug")
            .Child("Images", "ProductImages", "ProductId")
            .Child("Attributes", "ProductAttributes", "ProductId")
            .Values("CollectionIds", "ProductCollections", "ProductId", "CollectionId")
            .Child("Files", "ProductFiles", "ProductId")
            .Child("Lessons", "ProductLessons", "ProductId");

        public static readonly EntityMap<StoreCollection> Collections = Store<StoreCollection>("Collections");
        public static readonly EntityMap<StoreMenuItem> MenuItems = Store<StoreMenuItem>("MenuItems");
        public static readonly EntityMap<StorePage> Pages = Store<StorePage>("Pages");
        public static readonly EntityMap<StoreGalleryItem> GalleryItems = Store<StoreGalleryItem>("GalleryItems");

        public static readonly EntityMap<StoreCustomer> Customers = Store<StoreCustomer>("Customers")
            .Text("Mobile", 20)
            .Index("IX_Customers_Mobile", "CompanyCode", "Mobile");

        public static readonly EntityMap<StoreOrder> Orders = Store<StoreOrder>("Orders")
            .Text("Authority", 100)
            .Index("IX_Orders_Authority", "Authority")
            .Child("Items", "OrderItems", "OrderId");

        public static readonly EntityMap<StoreProductReview> Reviews = Store<StoreProductReview>("Reviews")
            .Index("IX_Reviews_Product", "CompanyCode", "ProductId");

        // ---- Content sites: posts, readers' comments; contact-form messages for every kind of site
        public static readonly EntityMap<StorePost> Posts = Store<StorePost>("Posts")
            .Text("Slug", 400)
            .Index("IX_Posts_Slug", "CompanyCode", "Slug")
            .Index("IX_Posts_PublishedAt", "CompanyCode", "PublishedAt");

        public static readonly EntityMap<StorePostComment> PostComments = Store<StorePostComment>("PostComments")
            .Index("IX_PostComments_Post", "CompanyCode", "PostId");

        public static readonly EntityMap<StoreContactMessage> ContactMessages = Store<StoreContactMessage>("ContactMessages");

        // ---- Booking sites: services, staff with weekly hours, closed days and appointments
        public static readonly EntityMap<StoreService> Services = Store<StoreService>("Services")
            .Values("StaffIds", "ServiceStaff", "ServiceId", "StaffId");

        public static readonly EntityMap<StoreStaff> Staff = Store<StoreStaff>("Staff")
            .Child("Hours", "StaffHours", "StaffId");

        public static readonly EntityMap<StoreTimeOff> TimeOffs = Store<StoreTimeOff>("TimeOffs")
            .Index("IX_TimeOffs_Date", "CompanyCode", "Date");

        public static readonly EntityMap<StoreAppointment> Appointments = Store<StoreAppointment>("Appointments")
            .Text("Code", 12).Text("Authority", 100).Text("CustomerMobile", 20).Text("Source", 10)
            .Index("IX_Appointments_StartAt", "CompanyCode", "StartAt")
            .Index("IX_Appointments_Code", "CompanyCode", "Code")
            .Index("IX_Appointments_Authority", "Authority");

        public static readonly EntityMap<StoreCart> Carts = new EntityMap<StoreCart>("Carts", "CartId", "CompanyCode")
            .Text("CartId", 32).Text("CompanyCode", 40)
            .Index("IX_Carts_UpdatedAt", "UpdatedAt")
            .Child("Items", "CartItems", "CompanyCode");

        // ---- Main site: product catalogue, its orders and the payment hub
        public static readonly EntityMap<Product> SiteProducts = new EntityMap<Product>("SiteProducts", "Id");

        public static readonly EntityMap<Order> SiteOrders = new EntityMap<Order>("SiteOrders", "Id")
            .Text("Authority", 100)
            .Index("IX_SiteOrders_Authority", "Authority");

        public static readonly EntityMap<PaymentClient> PaymentClients = new EntityMap<PaymentClient>("PaymentClients", "AppId")
            .Text("AppId", 100)
            .Values("AllowedReturnHosts", "PaymentClientHosts", "AppId", "Host", 253);

        public static readonly EntityMap<HubPayment> HubPayments = new EntityMap<HubPayment>("HubPayments", "Id")
            .Text("Token", 64)
            .UniqueIndex("UX_HubPayments_Token", "Token");

        // ---- Main site content edited in /Admin
        public static readonly EntityMap<SiteSettings> SiteSettings = new EntityMap<SiteSettings>("SiteSettings", "Id");

        public static readonly EntityMap<SiteMessage> SiteMessages = new EntityMap<SiteMessage>("SiteMessages", "Id")
            .Index("IX_SiteMessages_IsRead", "IsRead");

        public static readonly EntityMap<SiteFaq> SiteFaqs = new EntityMap<SiteFaq>("SiteFaqs", "Id");
        public static readonly EntityMap<SiteTestimonial> SiteTestimonials = new EntityMap<SiteTestimonial>("SiteTestimonials", "Id");

        // ---- Admin-panel tutorials (the videos themselves stay on disk)
        public static readonly EntityMap<TutorialDefinition> Tutorials = new EntityMap<TutorialDefinition>("Tutorials", "Page")
            .Text("Page", 40)
            .Values("Views", "TutorialViews", "Page", "ViewPath", 200)
            .Child("Steps", "TutorialSteps", "Page");

        public static readonly EntityMap<TutorialVideo> TutorialVideos = new EntityMap<TutorialVideo>("TutorialVideos", "Page")
            .Text("Page", 40);

        // ---- Bale notifications of each store and of the main site
        public static readonly EntityMap<NotifySettings> NotifySettings = new EntityMap<NotifySettings>("NotifySettings", "CompanyCode")
            .Text("CompanyCode", 40).Text("LinkCode", 20)
            .Index("IX_NotifySettings_LinkCode", "LinkCode");

        public static readonly EntityMap<BaleRecipient> BaleRecipients = Store<BaleRecipient>("BaleRecipients");

        // ---- Support tickets of the store owners to Nikoo's support (Ids are unique across stores)
        public static readonly EntityMap<SupportTicket> SupportTickets = new EntityMap<SupportTicket>("SupportTickets", "Id")
            .Text("CompanyCode", 40)
            .Index("IX_SupportTickets_CompanyCode", "CompanyCode")
            .Index("IX_SupportTickets_Status", "Status");

        public static readonly EntityMap<SupportMessage> SupportMessages = new EntityMap<SupportMessage>("SupportMessages", "Id")
            .Text("CompanyCode", 40)
            .Index("IX_SupportMessages_TicketId", "TicketId")
            .Index("IX_SupportMessages_CompanyCode", "CompanyCode");

        public static readonly EntityMap<SupportBaleLink> SupportBaleLinks = new EntityMap<SupportBaleLink>("SupportBaleLinks", "ChatId", "MessageId")
            .Text("CompanyCode", 40)
            .Index("IX_SupportBaleLinks_CompanyCode", "CompanyCode");

        // ---- Daily visits of every store and of the main site (CompanyCode '')
        public static readonly EntityMap<SiteVisitDay> SiteVisits = new EntityMap<SiteVisitDay>("SiteVisits", "CompanyCode", "Day")
            .Text("CompanyCode", 40)
            .Index("IX_SiteVisits_Day", "Day");

        // ---- Upload space bought by the stores; kept with the site's payments when a store is deleted
        public static readonly EntityMap<StoragePurchase> StoragePurchases = new EntityMap<StoragePurchase>("StoragePurchases", "Id")
            .Text("CompanyCode", 40).Text("Authority", 100)
            .Index("IX_StoragePurchases_CompanyCode", "CompanyCode")
            .Index("IX_StoragePurchases_Authority", "Authority");

        /// <summary>In creation order: a table comes after the table it references.</summary>
        public static readonly TableMap[] All =
        {
            Tenants, Settings, PageLayouts, Categories, AttributeDefs, Products, Collections, MenuItems, Pages,
            GalleryItems, Customers, Orders, Reviews, Posts, PostComments, ContactMessages, Services, Staff, TimeOffs,
            Appointments, Carts, SiteProducts, SiteOrders, PaymentClients, HubPayments, SiteSettings, SiteMessages,
            SiteFaqs, SiteTestimonials, Tutorials, TutorialVideos, NotifySettings, BaleRecipients,
            SupportTickets, SupportMessages, SupportBaleLinks, SiteVisits, StoragePurchases
        };

        /// <summary>Tables holding one store's data; their rows are removed when the store is deleted.</summary>
        public static readonly TableMap[] StoreScoped =
        {
            Settings, Categories, AttributeDefs, Products, Collections, MenuItems, Pages, GalleryItems, Customers,
            Orders, Reviews, Posts, PostComments, ContactMessages, Services, Staff, TimeOffs, Appointments, Carts,
            NotifySettings, BaleRecipients, SupportTickets, SupportMessages, SupportBaleLinks, SiteVisits
        };

        static NikooTables()
        {
            foreach (var table in All) table.Validate();
        }

        private static EntityMap<T> Store<T>(string table) where T : class, new()
        {
            return new EntityMap<T>(table, "CompanyCode", "Id");
        }
    }
}
