using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>سبد خرید بازدیدکنندگان؛ هر سبد با شناسه کوکی و کد فروشگاه در دیتابیس ذخیره می‌شود</summary>
    public static class CartStore
    {
        private static readonly Regex CartIdPattern = new Regex("^[0-9a-f]{32}$");

        public static string NewCartId() => Guid.NewGuid().ToString("N");

        public static bool IsValidCartId(string cartId) => cartId != null && CartIdPattern.IsMatch(cartId);

        public static List<StoreCartItem> Get(string cartId, string companyCode)
        {
            if (!IsValidCartId(cartId) || string.IsNullOrEmpty(companyCode)) return new List<StoreCartItem>();
            var cart = NikooDb.Get(NikooTables.Carts, Key(cartId, companyCode));
            return cart?.Items ?? new List<StoreCartItem>();
        }

        /// <summary>An empty list removes the cart.</summary>
        public static void Save(string cartId, string companyCode, List<StoreCartItem> items)
        {
            if (!IsValidCartId(cartId) || string.IsNullOrEmpty(companyCode)) return;
            if (items == null || items.Count == 0)
            {
                NikooDb.Delete(NikooTables.Carts, Key(cartId, companyCode));
                return;
            }
            NikooDb.Upsert(NikooTables.Carts, new StoreCart
            {
                CartId = cartId,
                CompanyCode = companyCode,
                UpdatedAt = DateTime.Now,
                Items = items
            });
        }

        /// <summary>Removes carts nobody has touched since <paramref name="cutoff"/>.</summary>
        public static int PurgeOlderThan(DateTime cutoff)
        {
            return NikooDb.Execute("DELETE FROM [dbo].[Carts] WHERE [UpdatedAt] < @cutoff", new Row { ["cutoff"] = cutoff });
        }

        private static Row Key(string cartId, string companyCode) => new Row { ["CartId"] = cartId, ["CompanyCode"] = companyCode };
    }
}
