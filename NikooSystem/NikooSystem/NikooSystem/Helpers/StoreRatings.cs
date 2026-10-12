using System.Collections.Generic;
using System.Web;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Helpers
{
    /// <summary>
    /// The store's product ratings, read once per request: product lists and cards ask for many products,
    /// and one query for all reviews is far cheaper than one per product.
    /// </summary>
    public sealed class StoreRatings
    {
        private readonly Dictionary<int, ProductRating> _byProduct;

        private StoreRatings(Dictionary<int, ProductRating> byProduct)
        {
            _byProduct = byProduct;
        }

        public static StoreRatings For(string companyCode)
        {
            var items = HttpContext.Current?.Items;
            var key = "nikoo_ratings_" + companyCode;
            if (items?[key] is StoreRatings cached) return cached;

            var ratings = new StoreRatings(TenantStore.GetRatings(companyCode));
            if (items != null) items[key] = ratings;
            return ratings;
        }

        public ProductRating Of(int productId)
        {
            return _byProduct.TryGetValue(productId, out var rating) ? rating : ProductRating.None;
        }
    }
}
