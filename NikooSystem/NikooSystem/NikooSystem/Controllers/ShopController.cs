using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class ShopController : ShopBaseController
    {
        [HttpGet]
        public ActionResult Index()
        {
            var products = TenantStore.GetProducts(CompanyCode).Where(p => p.IsActive).ToList();
            var collections = TenantStore.GetCollections(CompanyCode).Where(c => c.IsActive).ToList();
            var sections = HomeSections.Resolve(CurrentSettings).Where(s => s.IsVisible).ToList();

            var sectionProducts = new Dictionary<string, List<StoreProduct>>();
            foreach (var sec in sections.Where(s => s.Type == "products"))
                sectionProducts[sec.Id] = SectionProducts(sec, products, collections);

            ViewBag.Categories = TenantStore.GetCategories(CompanyCode).Where(c => c.IsActive && c.ParentId == null).ToList();
            ViewBag.Featured = products.Where(p => p.IsFeatured).Take(8).ToList();
            ViewBag.Gallery = TenantStore.GetGallery(CompanyCode).Take(6).ToList();
            ViewBag.Sections = sections;
            ViewBag.SectionProducts = sectionProducts;
            ViewBag.Collections = collections;
            return View();
        }

        private List<StoreProduct> SectionProducts(StoreHomeSection sec, List<StoreProduct> products, List<StoreCollection> collections)
        {
            IEnumerable<StoreProduct> q;
            switch (sec.Source)
            {
                case "collection":
                    if (!sec.CollectionId.HasValue || collections.All(c => c.Id != sec.CollectionId.Value))
                        return new List<StoreProduct>();
                    q = products.Where(p => p.CollectionIds != null && p.CollectionIds.Contains(sec.CollectionId.Value))
                        .OrderByDescending(p => p.CreatedAt);
                    break;
                case "category":
                    if (!sec.CategoryId.HasValue) return new List<StoreProduct>();
                    var catIds = GetCategoryTreeIds(sec.CategoryId.Value);
                    q = products.Where(p => catIds.Contains(p.CategoryId)).OrderByDescending(p => p.CreatedAt);
                    break;
                case "bestseller":
                    q = products.OrderByDescending(p => p.SoldCount);
                    break;
                case "onsale":
                    q = products.Where(p => p.CompareAtPrice.HasValue && p.CompareAtPrice > p.Price)
                        .OrderByDescending(p => (p.CompareAtPrice.Value - p.Price) / (double)p.CompareAtPrice.Value);
                    break;
                case "featured":
                    q = products.Where(p => p.IsFeatured);
                    break;
                default:
                    q = products.OrderByDescending(p => p.CreatedAt);
                    break;
            }
            return q.Take(Math.Min(HomeSections.MaxProductsPerSection, Math.Max(1, sec.Count))).ToList();
        }

        private const int CatalogPageSize = 24;

        [HttpGet]
        public ActionResult Catalog(int? categoryId, string q, string sort = "newest",
            long? minPrice = null, long? maxPrice = null, bool inStock = false, bool onSale = false, int page = 1,
            int? collectionId = null)
        {
            var activeProducts = TenantStore.GetProducts(CompanyCode).Where(p => p.IsActive).ToList();
            var categories = TenantStore.GetCategories(CompanyCode).Where(c => c.IsActive).ToList();
            var collection = collectionId.HasValue
                ? TenantStore.GetCollections(CompanyCode).FirstOrDefault(c => c.IsActive && c.Id == collectionId.Value)
                : null;

            var products = activeProducts.AsEnumerable();
            if (collection != null)
                products = products.Where(p => p.CollectionIds != null && p.CollectionIds.Contains(collection.Id));
            if (categoryId.HasValue)
            {
                var catIds = GetCategoryTreeIds(categoryId.Value);
                products = products.Where(p => catIds.Contains(p.CategoryId));
            }
            if (!string.IsNullOrWhiteSpace(q))
                products = products.Where(p =>
                    (p.Title ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (p.ShortDescription ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (p.Sku ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            if (minPrice.HasValue) products = products.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue) products = products.Where(p => p.Price <= maxPrice.Value);
            if (inStock) products = products.Where(p => p.Stock > 0);
            if (onSale) products = products.Where(p => p.CompareAtPrice.HasValue && p.CompareAtPrice > p.Price);

            switch ((sort ?? "").ToLowerInvariant())
            {
                case "price_asc":
                    products = products.OrderBy(p => p.Price);
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.Price);
                    break;
                case "popular":
                    products = products.OrderByDescending(p => p.ViewCount).ThenByDescending(p => p.IsFeatured);
                    break;
                case "bestseller":
                    products = products.OrderByDescending(p => p.SoldCount);
                    break;
                case "rating":
                    products = products.OrderByDescending(p => TenantStore.GetAverageRating(CompanyCode, p.Id))
                        .ThenByDescending(p => p.SoldCount);
                    break;
                default:
                    products = products.OrderByDescending(p => p.CreatedAt);
                    break;
            }

            var all = products.ToList();
            var totalPages = Math.Max(1, (int)Math.Ceiling(all.Count / (double)CatalogPageSize));
            page = Math.Min(Math.Max(1, page), totalPages);

            ViewBag.Categories = categories;
            ViewBag.CategoryCounts = categories.ToDictionary(
                c => c.Id,
                c => { var ids = GetCategoryTreeIds(c.Id); return activeProducts.Count(p => ids.Contains(p.CategoryId)); });
            ViewBag.CategoryId = categoryId;
            ViewBag.Category = categoryId.HasValue ? categories.FirstOrDefault(c => c.Id == categoryId.Value) : null;
            ViewBag.Collection = collection;
            ViewBag.CollectionId = collection?.Id;
            ViewBag.Q = q;
            ViewBag.Sort = sort;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.InStock = inStock;
            ViewBag.OnSale = onSale;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = all.Count;
            return View(all.Skip((page - 1) * CatalogPageSize).Take(CatalogPageSize).ToList());
        }

        [HttpGet]
        public ActionResult Product(string slug)
        {
            var product = TenantStore.GetProductBySlug(CompanyCode, slug);
            if (product == null || !product.IsActive) return HttpNotFound();

            product.ViewCount++;
            TenantStore.UpsertProduct(CompanyCode, product);

            var options = HomeSections.LayoutOf(CurrentSettings);
            var productTags = product.CollectionIds ?? new List<int>();
            ViewBag.Category = TenantStore.GetCategories(CompanyCode).FirstOrDefault(c => c.Id == product.CategoryId);
            ViewBag.Collections = productTags.Any()
                ? TenantStore.GetCollections(CompanyCode).Where(c => c.IsActive && productTags.Contains(c.Id)).ToList()
                : new List<StoreCollection>();
            ViewBag.Related = new List<StoreProduct>();
            if (options.ShowRelatedProducts)
            {
                var others = TenantStore.GetProducts(CompanyCode).Where(p => p.IsActive && p.Id != product.Id).ToList();
                var sameTag = others.Where(p => p.CollectionIds != null && p.CollectionIds.Any(productTags.Contains));
                ViewBag.Related = others.Where(p => p.CategoryId == product.CategoryId)
                    .Concat(sameTag)
                    .Distinct()
                    .Take(Math.Min(12, Math.Max(1, options.RelatedProductsCount)))
                    .ToList();
            }
            ViewBag.Reviews = TenantStore.GetProductReviews(CompanyCode, product.Id);
            ViewBag.AvgRating = TenantStore.GetAverageRating(CompanyCode, product.Id);
            ViewBag.MetaDescription = string.IsNullOrWhiteSpace(product.ShortDescription) ? product.Title : product.ShortDescription;
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddReview(int productId, string authorName, string comment, int rating = 5)
        {
            var product = TenantStore.GetProduct(CompanyCode, productId);
            if (product == null || !product.IsActive) return HttpNotFound();
            if (!HomeSections.LayoutOf(CurrentSettings).ShowReviews)
                return RedirectToAction("Product", new { company = CompanyCode, slug = product.Slug });

            if (string.IsNullOrWhiteSpace(authorName) || string.IsNullOrWhiteSpace(comment))
            {
                TempData["Error"] = "نام و متن نظر الزامی است.";
                return RedirectToAction("Product", new { company = CompanyCode, slug = product.Slug });
            }

            string mobile = null;
            if (CurrentCustomerId.HasValue)
            {
                var cust = TenantStore.GetCustomers(CompanyCode).FirstOrDefault(c => c.Id == CurrentCustomerId.Value);
                if (cust != null)
                {
                    mobile = cust.Mobile;
                    if (string.IsNullOrWhiteSpace(authorName)) authorName = cust.FullName;
                }
            }

            TenantStore.AddReview(CompanyCode, new StoreProductReview
            {
                ProductId = productId,
                CustomerId = CurrentCustomerId,
                AuthorName = authorName.Trim(),
                AuthorMobile = mobile,
                Rating = rating,
                Comment = comment.Trim(),
                IsApproved = true
            });

            TempData["Ok"] = "نظر شما ثبت شد.";
            return RedirectToAction("Product", new { company = CompanyCode, slug = product.Slug });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddToCart(int productId, int quantity = 1)
        {
            var product = TenantStore.GetProduct(CompanyCode, productId);
            if (product == null || !product.IsActive) return HttpNotFound();
            if (quantity < 1) quantity = 1;
            if (product.Stock <= 0)
            {
                TempData["Error"] = "این محصول در حال حاضر موجود نیست.";
                return RedirectToAction("Product", new { company = CompanyCode, slug = product.Slug });
            }

            var cart = GetCart();
            var existing = cart.FirstOrDefault(x => x.ProductId == productId);
            var inCart = existing?.Quantity ?? 0;
            if (inCart + quantity > product.Stock)
            {
                quantity = product.Stock - inCart;
                if (quantity <= 0)
                {
                    TempData["Error"] = "حداکثر موجودی این محصول (" + product.Stock + " عدد) در سبد شماست.";
                    return RedirectToAction("Cart", new { company = CompanyCode });
                }
            }
            if (existing != null) existing.Quantity += quantity;
            else
            {
                var img = product.Images?.OrderBy(i => i.SortOrder).FirstOrDefault();
                cart.Add(new StoreCartItem
                {
                    ProductId = product.Id,
                    Title = product.Title,
                    Slug = product.Slug,
                    ImageUrl = img?.ThumbUrl ?? img?.LargeUrl,
                    UnitPrice = product.Price,
                    Quantity = quantity
                });
            }
            SaveCart(cart);
            TempData["Ok"] = "به سبد خرید اضافه شد.";
            return RedirectToAction("Cart", new { company = CompanyCode });
        }

        [HttpGet]
        public ActionResult Cart()
        {
            return View(GetCart());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateCart(int productId, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(x => x.ProductId == productId);
            if (item != null)
            {
                if (quantity <= 0) cart.Remove(item);
                else item.Quantity = quantity;
                SaveCart(cart);
            }
            return RedirectToAction("Cart", new { company = CompanyCode });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveFromCart(int productId)
        {
            var cart = GetCart();
            cart.RemoveAll(x => x.ProductId == productId);
            SaveCart(cart);
            return RedirectToAction("Cart", new { company = CompanyCode });
        }

        [HttpGet]
        public ActionResult Checkout()
        {
            var cart = GetCart();
            if (!cart.Any()) return RedirectToAction("Cart", new { company = CompanyCode });

            StoreCustomer customer = null;
            if (CurrentCustomerId.HasValue)
                customer = TenantStore.GetCustomers(CompanyCode).FirstOrDefault(c => c.Id == CurrentCustomerId.Value);

            ViewBag.Cart = cart;
            ViewBag.Customer = customer;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Checkout(string customerName, string customerMobile, string customerEmail, string customerAddress)
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                TempData["Error"] = "سبد خرید خالی است.";
                return RedirectToAction("Cart", new { company = CompanyCode });
            }

            if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(customerMobile))
            {
                TempData["Error"] = "نام و موبایل الزامی است.";
                return RedirectToAction("Checkout", new { company = CompanyCode });
            }

            var order = new StoreOrder
            {
                CustomerId = CurrentCustomerId,
                CustomerName = customerName,
                CustomerMobile = customerMobile,
                CustomerEmail = customerEmail,
                CustomerAddress = customerAddress,
                Items = cart.Select(c => new StoreOrderItem
                {
                    ProductId = c.ProductId,
                    Title = c.Title,
                    UnitPrice = c.UnitPrice,
                    Quantity = c.Quantity
                }).ToList(),
                Amount = cart.Sum(c => c.UnitPrice * c.Quantity),
                Status = StoreOrderStatus.PendingPayment
            };
            order = TenantStore.AddOrder(CompanyCode, order);

            if (!CurrentSettings.EnablePayment)
            {
                order.Status = StoreOrderStatus.Processing;
                order.PaymentMessage = "پرداخت غیرفعال — سفارش ثبت شد.";
                TenantStore.UpdateOrder(CompanyCode, order);
                SaveCart(new List<StoreCartItem>());
                return RedirectToAction("OrderDone", new { company = CompanyCode, id = order.Id, ok = 1 });
            }

            var zarin = new ZarinPalService(CurrentSettings.ZarinPalMerchantId, CurrentSettings.ZarinPalSandbox);
            var callback = Request.Url.GetLeftPart(UriPartial.Authority) + "/Shop/" + CompanyCode + "/PaymentCallback";
            var pay = await zarin.RequestPaymentAsync(
                order.Amount,
                "سفارش " + order.OrderNumber + " - " + CurrentSettings.StoreName,
                order.CustomerEmail,
                order.CustomerMobile,
                order.Id.ToString(),
                callback);

            if (!pay.Success)
            {
                order.Status = StoreOrderStatus.Failed;
                order.PaymentMessage = pay.Message;
                TenantStore.UpdateOrder(CompanyCode, order);
                TempData["Error"] = pay.Message;
                return RedirectToAction("Checkout", new { company = CompanyCode });
            }

            order.Authority = pay.Authority;
            TenantStore.UpdateOrder(CompanyCode, order);
            return Redirect(pay.PaymentUrl);
        }

        [HttpGet]
        public async Task<ActionResult> PaymentCallback(int? orderId, string Authority, string Status)
        {
            if (orderId == null) return RedirectToAction("OrderDone", new { company = CompanyCode, ok = 0 });
            var order = TenantStore.GetOrder(CompanyCode, orderId.Value);
            if (order == null) return RedirectToAction("OrderDone", new { company = CompanyCode, ok = 0 });

            if (!string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = StoreOrderStatus.Failed;
                order.PaymentMessage = "پرداخت لغو شد.";
                TenantStore.UpdateOrder(CompanyCode, order);
                return RedirectToAction("OrderDone", new { company = CompanyCode, id = order.Id, ok = 0 });
            }

            var zarin = new ZarinPalService(CurrentSettings.ZarinPalMerchantId, CurrentSettings.ZarinPalSandbox);
            var verify = await zarin.VerifyPaymentAsync(Authority ?? order.Authority, order.Amount);
            if (!verify.Success)
            {
                order.Status = StoreOrderStatus.Failed;
                order.PaymentMessage = verify.Message;
                TenantStore.UpdateOrder(CompanyCode, order);
                return RedirectToAction("OrderDone", new { company = CompanyCode, id = order.Id, ok = 0 });
            }

            order.Status = StoreOrderStatus.Paid;
            order.RefId = verify.RefId;
            order.PaidAt = DateTime.Now;
            order.PaymentMessage = verify.Message;
            TenantStore.UpdateOrder(CompanyCode, order);
            foreach (var item in order.Items ?? new List<StoreOrderItem>())
            {
                var p = TenantStore.GetProduct(CompanyCode, item.ProductId);
                if (p != null)
                {
                    p.SoldCount += item.Quantity;
                    TenantStore.UpsertProduct(CompanyCode, p);
                }
            }
            SaveCart(new List<StoreCartItem>());
            return RedirectToAction("OrderDone", new { company = CompanyCode, id = order.Id, ok = 1 });
        }

        [HttpGet]
        public ActionResult OrderDone(int? id, int? ok)
        {
            StoreOrder order = null;
            if (id.HasValue) order = TenantStore.GetOrder(CompanyCode, id.Value);
            ViewBag.Ok = ok == 1 || (order != null && (order.Status == StoreOrderStatus.Paid || order.Status == StoreOrderStatus.Processing));
            return View(order);
        }

        [HttpGet]
        public ActionResult Gallery(string album = null)
        {
            var items = TenantStore.GetGallery(CompanyCode);
            ViewBag.Albums = items.Select(i => i.Album).Distinct().ToList();
            ViewBag.Album = album;
            if (!string.IsNullOrWhiteSpace(album))
                items = items.Where(i => string.Equals(i.Album, album, StringComparison.OrdinalIgnoreCase)).ToList();
            return View(items);
        }

        [HttpGet]
        public ActionResult Page(string slug)
        {
            var page = TenantStore.GetPageBySlug(CompanyCode, slug);
            if (page == null) return HttpNotFound();
            return View(page);
        }

        private HashSet<int> GetCategoryTreeIds(int rootId)
        {
            var all = TenantStore.GetCategories(CompanyCode);
            var set = new HashSet<int> { rootId };
            bool added;
            do
            {
                added = false;
                foreach (var c in all.Where(c => c.ParentId.HasValue && set.Contains(c.ParentId.Value)))
                {
                    if (set.Add(c.Id)) added = true;
                }
            } while (added);
            return set;
        }
    }
}
