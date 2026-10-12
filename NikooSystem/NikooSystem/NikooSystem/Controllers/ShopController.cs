using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Xml.Linq;
using NikooSystem.Filters;
using NikooSystem.Helpers;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>The public site of a store; the booking pages are in ShopController.Booking.cs.</summary>
    [CountVisits]
    public partial class ShopController : ShopBaseController
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            if (filterContext.Result == null && Kind.IsContent)
                ViewBag.Breaking = TenantStore.TopPosts(CompanyCode, 6, "breaking");
        }

        [HttpGet]
        public ActionResult Index()
        {
            var sections = HomeSections.Resolve(CurrentSettings).Where(s => s.IsVisible).ToList();
            ViewBag.Sections = sections;
            ViewBag.Categories = TenantStore.GetCategories(CompanyCode).Where(c => c.IsActive && c.ParentId == null).ToList();
            ViewBag.Gallery = TenantStore.GetGallery(CompanyCode).Take(6).ToList();
            SeoHome();
            if (Kind.IsBooking) return BookingHome();
            return Kind.IsContent ? ContentHome(sections) : ShopHome(sections);
        }

        private ActionResult ShopHome(List<StoreHomeSection> sections)
        {
            var collections = TenantStore.GetCollections(CompanyCode).Where(c => c.IsActive).ToList();
            var sectionProducts = new Dictionary<string, List<StoreProduct>>();
            foreach (var sec in sections.Where(s => s.Type == "products"))
            {
                var hidden = sec.Source == "collection" && collections.All(c => c.Id != sec.CollectionId);
                sectionProducts[sec.Id] = hidden
                    ? new List<StoreProduct>()
                    : TenantStore.TopProducts(CompanyCode, sec.Source, sec.CollectionId, sec.CategoryId,
                        Math.Min(HomeSections.MaxProductsPerSection, Math.Max(1, sec.Count)));
            }

            var featured = TenantStore.TopProducts(CompanyCode, "featured", null, null, 8);
            ViewBag.Featured = featured;
            ViewBag.SectionProducts = sectionProducts;
            ViewBag.Collections = collections;
            if (sections.Any(s => s.Type == "deals"))
                ViewBag.Deals = TenantStore.TopProducts(CompanyCode, "onsale", null, null, 10);
            if (sections.Any(s => s.Type == "newest"))
            {
                var shown = new HashSet<int>(sections.Any(s => s.Type == "featured") ? featured.Select(p => p.Id) : Enumerable.Empty<int>());
                ViewBag.Newest = TenantStore.TopProducts(CompanyCode, "newest", null, null, 8 + shown.Count)
                    .Where(p => !shown.Contains(p.Id)).Take(8).ToList();
            }
            if (sections.Any(s => s.Type == "reviews"))
                ViewBag.HomeReviews = TenantStore.HomeReviews(CompanyCode, 6);
            ViewBag.SignedIn = CurrentCustomer != null;
            return View("Index");
        }

        private ActionResult ContentHome(List<StoreHomeSection> sections)
        {
            var headlines = TenantStore.TopPosts(CompanyCode, 5, "featured");
            ViewBag.Headlines = headlines.Any() ? headlines : TenantStore.TopPosts(CompanyCode, 5);
            ViewBag.Latest = TenantStore.TopPosts(CompanyCode, 10);
            ViewBag.Popular = TenantStore.TopPosts(CompanyCode, 5, "popular");

            var categories = (List<StoreCategory>)ViewBag.Categories;
            ViewBag.Topics = categories.Take(6)
                .Select(c => new KeyValuePair<StoreCategory, List<StorePost>>(c, TenantStore.TopPosts(CompanyCode, 4, "newest", c.Id)))
                .Where(t => t.Value.Any())
                .ToList();

            var sectionPosts = new Dictionary<string, List<StorePost>>();
            foreach (var sec in sections.Where(s => s.Type == "posts"))
            {
                var take = Math.Min(HomeSections.MaxProductsPerSection, Math.Max(1, sec.Count));
                sectionPosts[sec.Id] = sec.Source == "category" && !sec.CategoryId.HasValue
                    ? new List<StorePost>()
                    : TenantStore.TopPosts(CompanyCode, take, sec.Source == "popular" ? "popular" : "newest", sec.Source == "category" ? sec.CategoryId : null);
            }
            ViewBag.SectionPosts = sectionPosts;
            return View("ContentHome");
        }

        // ---- Catalogue (physical and digital stores)

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Catalog(CatalogQuery query)
        {
            var collection = query.CollectionId.HasValue ? TenantStore.GetCollection(CompanyCode, query.CollectionId.Value) : null;
            if (collection != null && !collection.IsActive) collection = null;
            var page = TenantStore.CatalogPage(CompanyCode, query, collection);
            Response.AppendHeader("Vary", "X-Requested-With");
            if (Request.IsAjaxRequest()) return query.Page > page.Page ? Content("") : (ActionResult)PartialView("_CatalogItems", page);

            var categories = TenantStore.GetCategories(CompanyCode).Where(c => c.IsActive).ToList();
            var direct = TenantStore.ProductCountsByCategory(CompanyCode);
            ViewBag.Categories = categories;
            ViewBag.CategoryCounts = categories.ToDictionary(
                c => c.Id,
                c => TreeHelper.SubtreeIds(categories, c.Id).Sum(id => direct.TryGetValue(id, out var n) ? n : 0));
            ViewBag.Category = query.CategoryId.HasValue ? categories.FirstOrDefault(c => c.Id == query.CategoryId.Value) : null;
            ViewBag.Collection = collection;
            ViewBag.Query = query;
            SeoCatalog(query, ViewBag.Category as StoreCategory, collection, page.Page);
            return View(page);
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Product(string slug)
        {
            var product = ActiveProduct(TenantStore.GetProductBySlug(CompanyCode, slug));
            if (product == null) return HttpNotFound();
            TenantStore.CountProductView(CompanyCode, product.Id);

            var options = HomeSections.LayoutOf(CurrentSettings);
            var productTags = product.CollectionIds ?? new List<int>();
            ViewBag.Category = TenantStore.GetCategory(CompanyCode, product.CategoryId);
            ViewBag.Collections = productTags.Any()
                ? TenantStore.GetCollections(CompanyCode).Where(c => c.IsActive && productTags.Contains(c.Id)).ToList()
                : new List<StoreCollection>();
            ViewBag.Related = options.ShowRelatedProducts
                ? TenantStore.RelatedProducts(CompanyCode, product, HomeSections.ClampRelatedCount(options.RelatedProductsCount))
                : new List<StoreProduct>();
            var reviews = TenantStore.GetProductReviews(CompanyCode, product.Id);
            ViewBag.Reviews = reviews;
            ViewBag.AvgRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
            ViewBag.Owned = product.IsDigital() && TenantStore.OwnsProduct(CompanyCode, CurrentCustomerId, product.Id);
            SeoProduct(product, ViewBag.Category as StoreCategory, reviews);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult AddReview(int productId, string authorName, string comment, int rating = 5)
        {
            var product = ActiveProduct(TenantStore.GetProduct(CompanyCode, productId));
            if (product == null) return HttpNotFound();
            var backToProduct = new { slug = product.Slug };
            if (!HomeSections.LayoutOf(CurrentSettings).ShowReviews)
                return ToAction("Product", backToProduct);

            if (string.IsNullOrWhiteSpace(authorName) || string.IsNullOrWhiteSpace(comment))
                return Fail("نام و متن نظر الزامی است.", "Product", backToProduct);

            var review = TenantStore.AddReview(CompanyCode, new StoreProductReview
            {
                ProductId = productId,
                CustomerId = CurrentCustomer?.Id,
                AuthorName = authorName.Trim(),
                AuthorMobile = CurrentCustomer?.Mobile,
                Rating = rating,
                Comment = comment.Trim(),
                IsApproved = true
            });
            Notifications.StoreReview(CompanyCode, product.Title, review);

            return Ok("نظر شما ثبت شد.", "Product", backToProduct);
        }

        // ---- Cart

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult AddToCart(int productId, int quantity = 1)
        {
            var product = ActiveProduct(TenantStore.GetProduct(CompanyCode, productId));
            if (product == null) return HttpNotFound();
            if (quantity < 1) quantity = 1;

            var cart = GetCart();
            var existing = cart.FirstOrDefault(x => x.ProductId == productId);
            if (product.IsDigital())
            {
                if (TenantStore.OwnsProduct(CompanyCode, CurrentCustomerId, product.Id))
                    return Fail("این مورد را قبلاً خریده‌اید؛ از «کتابخانه من» به آن دسترسی دارید.", "Product", new { slug = product.Slug });
                if (existing != null) return Ok("این مورد در سبد خرید شما هست.", "Cart");
                quantity = 1;
            }
            else
            {
                if (product.Stock <= 0)
                    return Fail("این محصول در حال حاضر موجود نیست.", "Product", new { slug = product.Slug });
                var inCart = existing?.Quantity ?? 0;
                if (inCart + quantity > product.Stock)
                {
                    quantity = product.Stock - inCart;
                    if (quantity <= 0)
                        return Fail("حداکثر موجودی این محصول (" + product.Stock + " عدد) در سبد شماست.", "Cart");
                }
            }

            if (existing != null) existing.Quantity += quantity;
            else
            {
                var img = product.CoverImage();
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
            return Ok("به سبد خرید اضافه شد.", "Cart");
        }

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Cart()
        {
            var cart = GetCart();
            var products = CartProducts(cart);
            ViewBag.Products = products;
            ViewBag.Shipping = ShippingFor(cart, products);
            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult UpdateCart(int productId, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(x => x.ProductId == productId);
            if (item != null)
            {
                var product = TenantStore.GetProduct(CompanyCode, productId);
                if (quantity <= 0) cart.Remove(item);
                else item.Quantity = product != null && product.IsDigital() ? 1 : quantity;
                SaveCart(cart);
            }
            return ToAction("Cart");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult RemoveFromCart(int productId)
        {
            var cart = GetCart();
            cart.RemoveAll(x => x.ProductId == productId);
            SaveCart(cart);
            return ToAction("Cart");
        }

        private Dictionary<int, StoreProduct> CartProducts(List<StoreCartItem> cart) =>
            TenantStore.GetProductsByIds(CompanyCode, cart.Select(i => i.ProductId)).ToDictionary(p => p.Id);

        /// <summary>Shipping is charged once per order and only when something has to be sent.</summary>
        private long ShippingFor(List<StoreCartItem> cart, Dictionary<int, StoreProduct> products)
        {
            var physical = cart.Where(i => !products.TryGetValue(i.ProductId, out var p) || !p.IsDigital()).ToList();
            return physical.Any() ? CurrentSettings.ShippingFor(cart.Sum(i => i.UnitPrice * i.Quantity)) : 0;
        }

        // ---- Checkout and payment

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Checkout()
        {
            var cart = GetCart();
            if (!cart.Any()) return ToAction("Cart");
            var products = CartProducts(cart);
            var hasDigital = cart.Any(i => products.TryGetValue(i.ProductId, out var p) && p.IsDigital());
            if (hasDigital && CurrentCustomer == null)
            {
                Fail("برای خرید کتاب، دوره یا فایل ابتدا وارد شوید یا ثبت‌نام کنید؛ خریدها در حساب شما قرار می‌گیرد.");
                return ToLogin();
            }

            ViewBag.Cart = cart;
            ViewBag.Customer = CurrentCustomer;
            ViewBag.HasPhysical = cart.Any(i => !products.TryGetValue(i.ProductId, out var p) || !p.IsDigital());
            ViewBag.Shipping = ShippingFor(cart, products);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public async Task<ActionResult> Checkout(string customerName, string customerMobile, string customerEmail, string customerAddress)
        {
            var cart = GetCart();
            if (!cart.Any())
                return Fail("سبد خرید خالی است.", "Cart");

            var products = CartProducts(cart);
            var missing = cart.FirstOrDefault(i => !products.TryGetValue(i.ProductId, out var p) || !p.IsActive);
            if (missing != null)
                return Fail("«" + missing.Title + "» دیگر فروخته نمی‌شود؛ آن را از سبد حذف کنید.", "Cart");

            var hasDigital = cart.Any(i => products[i.ProductId].IsDigital());
            var hasPhysical = cart.Any(i => !products[i.ProductId].IsDigital());
            if (hasDigital && CurrentCustomer == null) return ToLogin();
            if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(customerMobile))
                return Fail("نام و موبایل الزامی است.", "Checkout");
            if (hasPhysical && string.IsNullOrWhiteSpace(customerAddress))
                return Fail("آدرس ارسال الزامی است.", "Checkout");

            var shortage = cart.FirstOrDefault(i => !products[i.ProductId].IsDigital() && products[i.ProductId].Stock < i.Quantity);
            if (shortage != null)
                return Fail("موجودی «" + shortage.Title + "» کافی نیست (" + products[shortage.ProductId].Stock + " عدد).", "Cart");
            var owned = hasDigital ? TenantStore.OwnedProductIds(CompanyCode, CurrentCustomer.Id) : new HashSet<int>();
            var already = cart.FirstOrDefault(i => owned.Contains(i.ProductId));
            if (already != null)
                return Fail("«" + already.Title + "» را قبلاً خریده‌اید؛ آن را از سبد حذف کنید.", "Cart");

            var subtotal = cart.Sum(c => c.UnitPrice * c.Quantity);
            var shipping = ShippingFor(cart, products);
            var order = TenantStore.AddOrder(CompanyCode, new StoreOrder
            {
                CustomerId = CurrentCustomerId,
                CustomerName = customerName,
                CustomerMobile = customerMobile,
                CustomerEmail = customerEmail,
                CustomerAddress = hasPhysical ? customerAddress : null,
                Items = cart.Select(c => new StoreOrderItem
                {
                    ProductId = c.ProductId,
                    Title = c.Title,
                    UnitPrice = c.UnitPrice,
                    Quantity = c.Quantity,
                    IsDigital = products[c.ProductId].IsDigital()
                }).ToList(),
                ShippingCost = shipping,
                Amount = subtotal + shipping,
                Status = StoreOrderStatus.PendingPayment
            });

            if (!CurrentSettings.EnablePayment)
            {
                TenantStore.ConfirmOrderWithoutPayment(CompanyCode, order);
                Notifications.StoreOrder(CompanyCode, order, false);
                SaveCart(new List<StoreCartItem>());
                return OrderDone(order, true);
            }

            var callback = Request.Url.GetLeftPart(UriPartial.Authority) + "/Shop/" + CompanyCode + "/PaymentCallback?orderId=" + order.Id;
            var pay = await Gateway().RequestPaymentAsync(
                order.Amount,
                "سفارش " + order.OrderNumber + " - " + CurrentSettings.StoreName,
                order.CustomerEmail,
                order.CustomerMobile,
                order.Id.ToString(),
                callback);

            if (!pay.Success)
            {
                FailOrder(order, pay.Message);
                return Fail(pay.Message, "Checkout");
            }

            order.Authority = pay.Authority;
            TenantStore.UpdateOrder(CompanyCode, order);
            return Redirect(pay.PaymentUrl);
        }

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public async Task<ActionResult> PaymentCallback(int? orderId, string Authority, string Status)
        {
            var order = orderId.HasValue ? TenantStore.GetOrder(CompanyCode, orderId.Value) : null;
            if (order == null) return ToAction("OrderDone", new { ok = 0 });
            if (StoreOrder.Fulfilled.Contains(order.Status)) return OrderDone(order, true);

            var verify = await Gateway().VerifyCallbackAsync(order.Authority, Authority, Status, order.Amount);
            if (verify.Forged) return ToAction("OrderDone", new { ok = 0 });
            if (!verify.Success)
            {
                FailOrder(order, verify.Message);
                return OrderDone(order, false);
            }

            if (TenantStore.MarkOrderPaid(CompanyCode, order.Id, verify.RefId, verify.Message))
                Notifications.StoreOrder(CompanyCode, TenantStore.GetOrder(CompanyCode, order.Id) ?? order, true);
            SaveCart(new List<StoreCartItem>());
            return OrderDone(order, true);
        }

        private ZarinPalService Gateway() => new ZarinPalService(CurrentSettings.ZarinPalMerchantId, CurrentSettings.ZarinPalSandbox);

        private void FailOrder(StoreOrder order, string message)
        {
            order.Status = StoreOrderStatus.Failed;
            order.PaymentMessage = message;
            TenantStore.UpdateOrder(CompanyCode, order);
        }

        private ActionResult OrderDone(StoreOrder order, bool ok) => ToAction("OrderDone", new { id = order.Id, ok = ok ? 1 : 0 });

        [HttpGet]
        [NoIndex]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult OrderDone(int? id, int? ok)
        {
            var order = id.HasValue ? TenantStore.GetOrder(CompanyCode, id.Value) : null;
            ViewBag.Ok = ok == 1 || (order != null && StoreOrder.Fulfilled.Contains(order.Status));
            return View(order);
        }

        // ---- Posts (content sites)

        [HttpGet]
        [ForKinds(SiteKinds.Content)]
        public ActionResult Blog(PostQuery query)
        {
            var page = TenantStore.PostsPage(CompanyCode, query);
            var categories = TenantStore.GetCategories(CompanyCode).Where(c => c.IsActive).ToList();
            ViewBag.Categories = categories;
            Response.AppendHeader("Vary", "X-Requested-With");
            if (Request.IsAjaxRequest()) return query.Page > page.Page ? Content("") : (ActionResult)PartialView("_PostItems", page);

            ViewBag.Category = query.CategoryId.HasValue ? categories.FirstOrDefault(c => c.Id == query.CategoryId.Value) : null;
            ViewBag.Popular = TenantStore.TopPosts(CompanyCode, 5, "popular");
            ViewBag.Query = query;
            SeoBlog(query, ViewBag.Category as StoreCategory, page.Page);
            return View(page);
        }

        [HttpGet]
        [ForKinds(SiteKinds.Content)]
        public ActionResult Post(string slug)
        {
            var post = TenantStore.GetLivePostBySlug(CompanyCode, slug);
            if (post == null) return HttpNotFound();
            TenantStore.CountPostView(CompanyCode, post.Id);

            ViewBag.Category = post.CategoryId.HasValue ? TenantStore.GetCategory(CompanyCode, post.CategoryId.Value) : null;
            ViewBag.Comments = post.AllowComments ? TenantStore.ApprovedComments(CompanyCode, post.Id) : new List<StorePostComment>();
            ViewBag.Related = TenantStore.TopPosts(CompanyCode, 4, "newest", post.CategoryId, post.Id);
            ViewBag.Popular = TenantStore.TopPosts(CompanyCode, 5, "popular", null, post.Id);
            SeoPost(post, ViewBag.Category as StoreCategory);
            ViewBag.ShareUrl = PageSeo.Canonical;
            return View(post);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Content)]
        public ActionResult AddComment(int postId, string authorName, string authorEmail, string body, string website)
        {
            var post = TenantStore.GetPost(CompanyCode, postId);
            if (post == null || !post.IsLive()) return HttpNotFound();
            var back = new { slug = post.Slug };
            if (!post.AllowComments || !string.IsNullOrEmpty(website)) return ToAction("Post", back);
            if (string.IsNullOrWhiteSpace(authorName) || string.IsNullOrWhiteSpace(body))
                return Fail("نام و متن نظر الزامی است.", "Post", back);

            var approved = !CurrentSettings.ModerateComments;
            var comment = TenantStore.AddComment(CompanyCode, new StorePostComment
            {
                PostId = post.Id,
                AuthorName = Clip(authorName, 80),
                AuthorEmail = Clip(authorEmail, 120),
                Body = Clip(body, 2000),
                IsApproved = approved
            });
            Notifications.StoreComment(CompanyCode, post, comment);
            return Ok(approved ? "نظر شما منتشر شد." : "نظر شما ثبت شد و پس از تأیید منتشر می‌شود.", "Post", back);
        }

        /// <summary>RSS 2.0 feed of the latest posts, for news readers and aggregators.</summary>
        [HttpGet]
        [ForKinds(SiteKinds.Content)]
        public ActionResult Rss()
        {
            var seo = PageSeo;
            var posts = TenantStore.TopPosts(CompanyCode, 30);
            var channel = new XElement("channel",
                new XElement("title", CurrentSettings.StoreName),
                new XElement("link", seo.Url(HomePath)),
                new XElement("description", CurrentSettings.Tagline ?? CurrentSettings.StoreName),
                new XElement("language", "fa-IR"),
                posts.Select(p => new XElement("item",
                    new XElement("title", p.Title),
                    new XElement("link", seo.Url(Url.RouteUrl("ShopPost", new { company = CompanyCode, slug = p.Slug }))),
                    new XElement("guid", seo.Url(Url.RouteUrl("ShopPost", new { company = CompanyCode, slug = p.Slug }))),
                    new XElement("description", p.Summary ?? ""),
                    new XElement("pubDate", p.PublishedAt.ToUniversalTime().ToString("r")),
                    string.IsNullOrWhiteSpace(p.Author) ? null : new XElement("author", p.Author))));
            var xml = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("rss", new XAttribute("version", "2.0"), channel));
            return Content(xml.Declaration + Environment.NewLine + xml.Root, "application/rss+xml", Encoding.UTF8);
        }

        // ---- Contact form, gallery and pages (every kind of site)

        [HttpGet]
        public ActionResult Contact()
        {
            var s = CurrentSettings;
            SeoSimple("راه‌های ارتباط با " + s.StoreName
                + (string.IsNullOrWhiteSpace(s.Phone) ? "" : "؛ تلفن " + s.Phone.Trim())
                + (string.IsNullOrWhiteSpace(s.Address) ? "" : "؛ نشانی: " + s.Address.Trim()), "Contact");
            return View(new StoreContactMessage());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Contact(StoreContactMessage model, string website)
        {
            if (!string.IsNullOrEmpty(website)) return ToAction("Contact");
            if (!ModelState.IsValid) return View(model);
            var message = TenantStore.AddMessage(CompanyCode, new StoreContactMessage
            {
                FullName = model.FullName.Trim(),
                Email = Clip(model.Email, 120),
                Mobile = Clip(model.Mobile, 20),
                Subject = Clip(model.Subject, 120),
                Message = model.Message.Trim()
            });
            Notifications.StoreMessage(CompanyCode, message);
            return Ok("پیام شما ارسال شد؛ به‌زودی پاسخ می‌دهیم.", "Contact");
        }

        [HttpGet]
        public ActionResult Gallery(string album = null)
        {
            var items = TenantStore.GetGallery(CompanyCode);
            ViewBag.Albums = items.Select(i => i.Album).Distinct().ToList();
            ViewBag.Album = album;
            if (!string.IsNullOrWhiteSpace(album))
                items = items.Where(i => string.Equals(i.Album, album, StringComparison.OrdinalIgnoreCase)).ToList();
            SeoSimple("گالری تصاویر " + (string.IsNullOrWhiteSpace(album) ? "" : album.Trim() + " ") + CurrentSettings.StoreName, "Gallery",
                string.IsNullOrWhiteSpace(album) ? null : "album=" + Uri.EscapeDataString(album.Trim()));
            var cover = items.FirstOrDefault();
            if (cover != null) PageSeo.Image = cover.LargeUrl ?? cover.ThumbUrl;
            return View(items);
        }

        [HttpGet]
        public ActionResult Page(string slug)
        {
            var page = TenantStore.GetPageBySlug(CompanyCode, slug);
            if (page == null) return HttpNotFound();
            SeoStorePage(page);
            return View(page);
        }

        private static StoreProduct ActiveProduct(StoreProduct product) => product != null && product.IsActive ? product : null;

        private static string Clip(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }
    }
}
