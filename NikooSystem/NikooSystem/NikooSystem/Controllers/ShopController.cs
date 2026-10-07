using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    public class ShopController : ShopBaseController
    {
        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode).Where(c => c.IsActive && c.ParentId == null).ToList();
            ViewBag.Featured = TenantStore.GetProducts(CompanyCode).Where(p => p.IsActive && p.IsFeatured).Take(8).ToList();
            ViewBag.Gallery = TenantStore.GetGallery(CompanyCode).Take(6).ToList();
            return View();
        }

        [HttpGet]
        public ActionResult Catalog(int? categoryId, string q, string sort = "newest")
        {
            var products = TenantStore.GetProducts(CompanyCode).Where(p => p.IsActive).AsEnumerable();
            if (categoryId.HasValue)
            {
                var catIds = GetCategoryTreeIds(categoryId.Value);
                products = products.Where(p => catIds.Contains(p.CategoryId));
            }
            if (!string.IsNullOrWhiteSpace(q))
                products = products.Where(p =>
                    (p.Title ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (p.ShortDescription ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

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

            ViewBag.Categories = TenantStore.GetCategories(CompanyCode).Where(c => c.IsActive).ToList();
            ViewBag.CategoryId = categoryId;
            ViewBag.Q = q;
            ViewBag.Sort = sort;
            return View(products.ToList());
        }

        [HttpGet]
        public ActionResult Product(string slug)
        {
            var product = TenantStore.GetProductBySlug(CompanyCode, slug);
            if (product == null || !product.IsActive) return HttpNotFound();

            product.ViewCount++;
            TenantStore.UpsertProduct(CompanyCode, product);

            ViewBag.Category = TenantStore.GetCategories(CompanyCode).FirstOrDefault(c => c.Id == product.CategoryId);
            ViewBag.Related = TenantStore.GetProducts(CompanyCode)
                .Where(p => p.IsActive && p.CategoryId == product.CategoryId && p.Id != product.Id)
                .Take(4).ToList();
            ViewBag.Reviews = TenantStore.GetProductReviews(CompanyCode, product.Id);
            ViewBag.AvgRating = TenantStore.GetAverageRating(CompanyCode, product.Id);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddReview(int productId, string authorName, string comment, int rating = 5)
        {
            var product = TenantStore.GetProduct(CompanyCode, productId);
            if (product == null || !product.IsActive) return HttpNotFound();

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

            var cart = GetCart();
            var existing = cart.FirstOrDefault(x => x.ProductId == productId);
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
