namespace NikooSystem.Models.Store
{
    public class ProductGridQuery : GridQuery
    {
        public int? CategoryId { get; set; }
        /// <summary>active | inactive | featured | lowstock | sample</summary>
        public string Status { get; set; }
        public StoreProductType? Type { get; set; }
    }

    public class OrderGridQuery : GridQuery
    {
        public StoreOrderStatus? Status { get; set; }
    }

    public class ReviewGridQuery : GridQuery
    {
        /// <summary>pending | approved</summary>
        public string State { get; set; }
    }

    public class PostGridQuery : GridQuery
    {
        public int? CategoryId { get; set; }
        /// <summary>published | draft | scheduled | featured | breaking</summary>
        public string State { get; set; }
    }

    public class CommentGridQuery : GridQuery
    {
        /// <summary>pending | approved</summary>
        public string State { get; set; }
    }

    public class MessageGridQuery : GridQuery
    {
        /// <summary>unread | read</summary>
        public string State { get; set; }
    }

    public class GalleryGridQuery : GridQuery
    {
        public string Album { get; set; }
    }

    /// <summary>Filters of the storefront catalogue; pages are loaded one after another while the visitor scrolls.</summary>
    public class CatalogQuery
    {
        public const int PageSize = 24;

        public int? CategoryId { get; set; }
        public int? CollectionId { get; set; }
        public string Q { get; set; }
        /// <summary>newest | price_asc | price_desc | popular | bestseller | rating</summary>
        public string Sort { get; set; } = "newest";
        public long? MinPrice { get; set; }
        public long? MaxPrice { get; set; }
        public bool InStock { get; set; }
        public bool OnSale { get; set; }
        public StoreProductType? Type { get; set; }
        public int Page { get; set; } = 1;
    }

    /// <summary>Filters of the storefront post list (blog / news archive).</summary>
    public class PostQuery
    {
        public const int PageSize = 12;

        public int? CategoryId { get; set; }
        public string Q { get; set; }
        public string Tag { get; set; }
        public int Page { get; set; } = 1;
    }
}
