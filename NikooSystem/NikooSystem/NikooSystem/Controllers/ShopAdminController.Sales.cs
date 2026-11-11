using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>Orders, customers and product reviews.</summary>
    public partial class ShopAdminController
    {
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Orders(OrderGridQuery query)
        {
            ViewBag.Query = query;
            return View(TenantStore.OrderGrid(CompanyCode, query));
        }

        /// <summary>Manual digital access: completed order + cash/bank journal, no payment gateway.</summary>
        [HttpGet]
        [ForKinds(SiteKinds.Digital)]
        public ActionResult GrantAccess(int? productId, string name, string mobile, string amount)
        {
            BankBag(0);
            ViewBag.Products = TenantStore.GetProducts(CompanyCode).Where(p => p.IsActive && p.IsDigital()).OrderBy(p => p.Title).ToList();
            ViewBag.ProductId = productId;
            ViewBag.CustomerName = name;
            ViewBag.CustomerMobile = mobile;
            ViewBag.Amount = amount;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Digital)]
        public ActionResult GrantAccess(int productId, string customerName, string customerMobile, string amount, string payFrom, string note)
        {
            var back = new { productId, name = customerName, mobile = customerMobile, amount };
            long? priceOverride = null;
            var raw = (amount ?? "").Replace(",", "").Trim();
            if (raw.Length > 0)
            {
                long money;
                if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out money) || money < 0)
                    return Fail("مبلغ را به‌صورت عدد تومان بنویسید؛ خالی بگذارید تا قیمت محصول استفاده شود.", "GrantAccess", back);
                priceOverride = money;
            }

            var pay = ReadPay(payFrom, out var bankId);
            var error = TenantStore.GrantDigitalAccess(CompanyCode, productId, customerName, customerMobile, pay, bankId, note, priceOverride, out var order, out var temporaryPassword);
            if (error != null)
                return Fail(error, "GrantAccess", back);

            var message = "دسترسی «" + order.Items[0].Title + "» برای " + order.CustomerName + " فعال شد"
                + (string.IsNullOrEmpty(order.OrderNumber) ? "." : " (سفارش " + order.OrderNumber + ").");
            if (!string.IsNullOrEmpty(temporaryPassword))
                message += " حساب تازه ساخته شد؛ رمز موقت: " + temporaryPassword;
            return Ok(message, "OrderDetails", new { id = order.Id });
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult OrderDetails(int id)
        {
            var order = TenantStore.GetOrder(CompanyCode, id);
            if (order == null) return ToAction("Orders");
            ViewBag.Customer = TenantStore.GetCustomer(CompanyCode, order.CustomerId);
            ViewBag.Returns = TenantStore.ReturnsForOrder(CompanyCode, id);
            BankBag(0);
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult UpdateOrder(int id, StoreOrderStatus status, string trackingCode, string adminNote, string freightCost, string freightPayFrom)
        {
            if (status == StoreOrderStatus.Returned)
                return Fail("وضعیت «مرجوع شده» از طریق تأیید درخواست مرجوعی ثبت می‌شود.", "OrderDetails", new { id });

            var order = TenantStore.SetOrderStatus(CompanyCode, id, status, trackingCode, adminNote);
            if (order == null) return ToAction("Orders");

            long freight = 0;
            var raw = (freightCost ?? "").Replace(",", "").Trim();
            if (raw.Length > 0)
            {
                if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out freight) || freight < 0)
                    return Fail("هزینه حمل را به‌صورت عدد تومان بنویسید.", "OrderDetails", new { id });
            }
            if (freight > 0 && !order.FreightPosted)
            {
                var pay = ReadPay(freightPayFrom, out var bankId);
                if (pay == PayWith.Credit)
                    return Fail("برای ثبت هزینه حمل، صندوق یا بانک را انتخاب کنید.", "OrderDetails", new { id });
                TenantStore.PostOrderFreight(CompanyCode, id, freight, pay, bankId);
            }

            return Ok("سفارش " + order.OrderNumber + " به‌روز شد.", "OrderDetails", new { id });
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop)]
        public ActionResult Returns(ReturnGridQuery query)
        {
            ViewBag.PendingCount = TenantStore.CountPendingReturns(CompanyCode);
            return View(TenantStore.ReturnGrid(CompanyCode, query));
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop)]
        public ActionResult ReturnDetails(int id)
        {
            var request = TenantStore.GetReturn(CompanyCode, id);
            if (request == null) return ToAction("Returns");
            ViewBag.Order = TenantStore.GetOrder(CompanyCode, request.OrderId);
            BankBag(0);
            return View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop)]
        public ActionResult DecideReturn(int id, string decision, string adminNote, string payFrom, bool refundShipping = false)
        {
            if (string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase))
            {
                var error = TenantStore.RejectReturn(CompanyCode, id, adminNote);
                if (error != null) return Fail(error, "ReturnDetails", new { id });
                return Ok("درخواست مرجوعی رد شد.", "ReturnDetails", new { id });
            }

            var pay = ReadPay(payFrom, out var bankId);
            var approveError = TenantStore.ApproveReturn(CompanyCode, id, adminNote, pay, bankId, refundShipping);
            if (approveError != null) return Fail(approveError, "ReturnDetails", new { id });
            return Ok("مرجوعی تأیید شد؛ موجودی برگشت و سند حسابداری ثبت شد.", "ReturnDetails", new { id });
        }

        [HttpGet]
        [ForKinds(SiteKinds.Shop)]
        public ActionResult ShippingGuide()
        {
            return View(CurrentSettings);
        }

        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Customers(GridQuery query)
        {
            var page = TenantStore.CustomerGrid(CompanyCode, query);
            ViewBag.OrderCounts = TenantStore.OrderCounts(CompanyCode, page.Select(c => c.Id));
            return View(page);
        }

        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult Reviews(ReviewGridQuery query)
        {
            var page = TenantStore.ReviewGrid(CompanyCode, query);
            ViewBag.Products = TenantStore.GetProductsByIds(CompanyCode, page.Select(r => r.ProductId)).ToDictionary(p => p.Id);
            ViewBag.Query = query;
            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult ToggleReview(int id, bool approved)
        {
            var review = TenantStore.GetReview(CompanyCode, id);
            if (review != null)
            {
                review.IsApproved = approved;
                TenantStore.UpdateReview(CompanyCode, review);
            }
            return BackTo("Reviews");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ForKinds(SiteKinds.Shop, SiteKinds.Digital)]
        public ActionResult DeleteReview(int id)
        {
            TenantStore.DeleteReview(CompanyCode, id);
            return OkBack("نظر حذف شد.", "Reviews");
        }
    }
}
