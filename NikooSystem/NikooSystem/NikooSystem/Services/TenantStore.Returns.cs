using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>Customer return requests for physical shop orders, with stock and journal side-effects.</summary>
    public static partial class TenantStore
    {
        public static StoreReturnRequest GetReturn(string companyCode, int id) => Find(NikooTables.ReturnRequests, companyCode, id);

        public static List<StoreReturnRequest> ReturnsForOrder(string companyCode, int orderId) =>
            Every(NikooTables.ReturnRequests, companyCode, SqlFilter.ForStore(companyCode).Eq("OrderId", orderId), "t.[Id] DESC");

        private static readonly Dictionary<string, string> ReturnSorts = new Dictionary<string, string>
        {
            { "date", "t.[CreatedAt], t.[Id]" },
            { "order", "t.[OrderNumber]" },
            { "customer", "t.[CustomerName]" },
            { "amount", "t.[RefundAmount]" },
            { "status", "t.[Status]" }
        };

        public static PagedList<StoreReturnRequest> ReturnGrid(string companyCode, ReturnGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "OrderNumber", "CustomerName", "CustomerMobile", "Reason")
                .DateRange("CreatedAt", query.From, query.To);
            if (query.Status.HasValue) filter.Eq("Status", query.Status.Value.ToString());
            return Grid(NikooTables.ReturnRequests, companyCode, query, filter, ReturnSorts, "date", true);
        }

        public static int CountPendingReturns(string companyCode) =>
            NikooDb.Count(NikooTables.ReturnRequests, SqlFilter.ForStore(companyCode).Eq("Status", ReturnRequestStatus.Pending.ToString()));

        /// <summary>How many units of each product are already in approved returns for this order.</summary>
        public static Dictionary<int, int> ReturnedQuantities(string companyCode, int orderId)
        {
            var totals = new Dictionary<int, int>();
            foreach (var request in ReturnsForOrder(companyCode, orderId).Where(r => r.Status == ReturnRequestStatus.Approved))
            {
                foreach (var line in request.Lines ?? new List<StoreReturnLine>())
                {
                    int qty;
                    totals.TryGetValue(line.ProductId, out qty);
                    totals[line.ProductId] = qty + line.Quantity;
                }
            }
            return totals;
        }

        public static bool ShippingAlreadyRefunded(string companyCode, int orderId) =>
            ReturnsForOrder(companyCode, orderId).Any(r => r.Status == ReturnRequestStatus.Approved && r.RefundShipping);

        /// <summary>Customer opens a return request for physical lines of a fulfilled order.</summary>
        public static string RequestReturn(string companyCode, int orderId, int customerId, string reason,
            IEnumerable<StoreReturnLine> lines, out StoreReturnRequest request)
        {
            request = null;
            var settings = GetSettings(companyCode);
            if (settings == null || !settings.EnableReturns) return "مرجوعی در این فروشگاه فعال نیست.";

            var order = GetOrder(companyCode, orderId);
            if (order == null || order.CustomerId != customerId) return "سفارش پیدا نشد.";
            if (!StoreOrder.Fulfilled.Contains(order.Status)) return "فقط سفارش‌های پرداخت‌شده قابل مرجوعی هستند.";
            if (order.IsDigitalOnly()) return "محصولات دیجیتال مرجوع نمی‌شوند.";
            if (ReturnsForOrder(companyCode, orderId).Any(r => r.Status == ReturnRequestStatus.Pending))
                return "یک درخواست مرجوعی در انتظار بررسی دارید؛ تا پاسخ فروشنده صبر کنید.";

            if (settings.ReturnDays > 0)
            {
                var start = order.PaidAt ?? order.CreatedAt;
                if (DateTime.Now.Date > start.Date.AddDays(settings.ReturnDays))
                    return "مهلت مرجوعی این سفارش (" + settings.ReturnDays + " روز) تمام شده است.";
            }

            reason = (reason ?? "").Trim();
            if (reason.Length == 0) return "دلیل مرجوعی را بنویسید.";
            if (reason.Length > 500) reason = reason.Substring(0, 500);

            var already = ReturnedQuantities(companyCode, orderId);
            var wanted = (lines ?? Enumerable.Empty<StoreReturnLine>())
                .Where(l => l != null && l.ProductId > 0 && l.Quantity > 0)
                .GroupBy(l => l.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToList();
            if (wanted.Count == 0) return "حداقل یک کالا را برای مرجوعی انتخاب کنید.";

            var built = new List<StoreReturnLine>();
            foreach (var row in wanted)
            {
                var item = (order.Items ?? new List<StoreOrderItem>()).FirstOrDefault(i => i.ProductId == row.ProductId && !i.IsDigital);
                if (item == null) return "فقط کالاهای فیزیکی همین سفارش را می‌توان مرجوع کرد.";
                int prior;
                already.TryGetValue(row.ProductId, out prior);
                var left = item.Quantity - prior;
                if (row.Quantity > left)
                    return "برای «" + item.Title + "» حداکثر " + left + " عدد قابل مرجوعی است.";
                built.Add(new StoreReturnLine
                {
                    ProductId = item.ProductId,
                    Title = item.Title,
                    Quantity = row.Quantity,
                    UnitPrice = item.UnitPrice
                });
            }

            request = new StoreReturnRequest
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerId = customerId,
                CustomerName = order.CustomerName,
                CustomerMobile = order.CustomerMobile,
                Reason = reason,
                Status = ReturnRequestStatus.Pending,
                Lines = built,
                CreatedAt = DateTime.Now
            };
            Save(NikooTables.ReturnRequests, companyCode, request);
            return null;
        }

        /// <summary>Owner rejects a pending request; stock and money stay as they were.</summary>
        public static string RejectReturn(string companyCode, int id, string adminNote)
        {
            lock (LockFor(companyCode))
            {
                var request = GetReturn(companyCode, id);
                if (request == null) return "درخواست پیدا نشد.";
                if (request.Status != ReturnRequestStatus.Pending) return "این درخواست قبلاً رسیدگی شده است.";
                request.Status = ReturnRequestStatus.Rejected;
                request.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
                if (request.AdminNote != null && request.AdminNote.Length > 500)
                    request.AdminNote = request.AdminNote.Substring(0, 500);
                request.ResolvedAt = DateTime.Now;
                Update(NikooTables.ReturnRequests, companyCode, request);
            }
            return null;
        }

        /// <summary>
        /// Approves a return: restocks goods, posts a return journal (and optional shipping refund), and marks the order
        /// Returned when every physical line is back.
        /// </summary>
        public static string ApproveReturn(string companyCode, int id, string adminNote, PayWith refundVia, int bankId, bool refundShipping)
        {
            StoreReturnRequest request;
            StoreOrder order;
            lock (LockFor(companyCode))
            {
                request = GetReturn(companyCode, id);
                if (request == null) return "درخواست پیدا نشد.";
                if (request.Status != ReturnRequestStatus.Pending) return "این درخواست قبلاً رسیدگی شده است.";
                order = GetOrder(companyCode, request.OrderId);
                if (order == null) return "سفارش پیدا نشد.";
                if (!StoreOrder.Fulfilled.Contains(order.Status) && order.Status != StoreOrderStatus.Returned)
                    return "وضعیت سفارش برای مرجوعی مناسب نیست.";

                var already = ReturnedQuantities(companyCode, order.Id);
                foreach (var line in request.Lines ?? new List<StoreReturnLine>())
                {
                    var item = (order.Items ?? new List<StoreOrderItem>()).FirstOrDefault(i => i.ProductId == line.ProductId && !i.IsDigital);
                    if (item == null) return "کالای مرجوعی در سفارش نیست.";
                    int prior;
                    already.TryGetValue(line.ProductId, out prior);
                    if (line.Quantity <= 0 || prior + line.Quantity > item.Quantity)
                        return "تعداد مرجوعی «" + line.Title + "» از سفارش بیشتر است.";
                }

                if (refundShipping)
                {
                    if (order.ShippingCost <= 0) refundShipping = false;
                    else if (ShippingAlreadyRefunded(companyCode, order.Id)) return "هزینه ارسال این سفارش قبلاً برگشت خورده است.";
                }

                if (refundVia == PayWith.Credit) bankId = 0;
                else
                {
                    var bankError = ResolveBank(companyCode, refundVia, ref bankId);
                    if (bankError != null) return bankError;
                }

                var goodsRefund = (request.Lines ?? new List<StoreReturnLine>()).Sum(l => l.Amount);
                var shippingRefund = refundShipping ? order.ShippingCost : 0;
                var refund = goodsRefund + shippingRefund;
                if (refund <= 0) return "مبلغ مرجوعی صفر است.";

                using (var tx = NikooDb.Begin())
                {
                    foreach (var line in request.Lines)
                    {
                        if (line.Quantity <= 0) continue;
                        NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.Products.Name)
                            + " SET [Stock] = [Stock] + @Amount, [SoldCount] = CASE WHEN [SoldCount] - @Amount < 0 THEN 0 ELSE [SoldCount] - @Amount END"
                            + " WHERE [CompanyCode] = @CompanyCode AND [Id] = @Id",
                            new Row { ["CompanyCode"] = companyCode, ["Id"] = line.ProductId, ["Amount"] = line.Quantity }, tx);
                    }

                    request.Status = ReturnRequestStatus.Approved;
                    request.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
                    if (request.AdminNote != null && request.AdminNote.Length > 500)
                        request.AdminNote = request.AdminNote.Substring(0, 500);
                    request.RefundAmount = refund;
                    request.RefundShipping = refundShipping;
                    request.ResolvedAt = DateTime.Now;
                    NikooDb.Update(NikooTables.ReturnRequests, request, companyCode, tx);

                    // After this approval, are all physical units back?
                    var next = new Dictionary<int, int>(already);
                    foreach (var line in request.Lines)
                    {
                        int qty;
                        next.TryGetValue(line.ProductId, out qty);
                        next[line.ProductId] = qty + line.Quantity;
                    }
                    var physical = (order.Items ?? new List<StoreOrderItem>()).Where(i => !i.IsDigital).ToList();
                    var allBack = physical.Count > 0 && physical.All(i =>
                    {
                        int qty;
                        next.TryGetValue(i.ProductId, out qty);
                        return qty >= i.Quantity;
                    });
                    if (allBack && !order.Items.Any(i => i.IsDigital))
                    {
                        order.Status = StoreOrderStatus.Returned;
                        NikooDb.Update(NikooTables.Orders, order, companyCode, tx);
                    }

                    tx.Commit();
                }

                foreach (var line in request.Lines.Where(l => l.Quantity > 0))
                    AddMove(companyCode, line.ProductId, line.Quantity, AverageCost(companyCode, line.ProductId), "return", request.Id, "مرجوعی " + order.OrderNumber);
            }

            PostOrderReturn(companyCode, request, order, refundVia, bankId);
            return null;
        }

        private static void PostOrderReturn(string companyCode, StoreReturnRequest request, StoreOrder order, PayWith refundVia, int bankId)
        {
            if (request.RefundAmount <= 0) return;
            if (FindEntry(companyCode, JournalSource.Return, request.Id) != null) return;
            EnsureAccounts(companyCode);
            EnsureDocumentTypes(companyCode);

            var goods = request.RefundAmount - (request.RefundShipping ? order.ShippingCost : 0);
            var shipping = request.RefundShipping ? order.ShippingCost : 0;
            var creditAccount = refundVia == PayWith.Credit
                ? IdOf(companyCode, AccountCodes.Payable)
                : MoneyAccountId(companyCode, refundVia == PayWith.Bank ? PayWith.Bank : PayWith.Cash, bankId, false);

            var lines = new List<StoreJournalLine>();
            if (goods > 0)
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.SalesReturns), Debit = goods, Memo = order.OrderNumber });
            if (shipping > 0)
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.ShippingIncome), Debit = shipping, Memo = "برگشت ارسال " + order.OrderNumber });
            lines.Add(new StoreJournalLine { AccountId = creditAccount, Credit = request.RefundAmount, Memo = "مرجوعی " + order.OrderNumber });

            long cogs = 0;
            foreach (var line in request.Lines ?? new List<StoreReturnLine>())
            {
                var cost = AverageCost(companyCode, line.ProductId) * line.Quantity;
                cogs += cost;
            }
            if (cogs > 0)
            {
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Inventory), Debit = cogs, Memo = "برگشت موجودی" });
                lines.Add(new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.Cogs), Credit = cogs, Memo = "برگشت بهای فروش" });
            }

            if (refundVia == PayWith.Credit)
            {
                var party = PartyByName(companyCode, string.IsNullOrWhiteSpace(order.CustomerName) ? "مشتری" : order.CustomerName, PartyKind.Customer);
                var detail = party == null ? null : GetDetails(companyCode).FirstOrDefault(d => d.PartyId == party.Id);
                if (detail != null)
                {
                    foreach (var line in lines.Where(l => l.AccountId == creditAccount))
                        line.Detail1Id = detail.Id;
                }
            }

            Save(NikooTables.JournalEntries, companyCode, new StoreJournalEntry
            {
                Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-"),
                EntryDate = DateTime.Today,
                Description = "مرجوعی سفارش " + order.OrderNumber,
                Source = JournalSource.Return,
                SourceId = request.Id,
                DocumentTypeId = TypeId(companyCode, DocumentTypeCodes.Return),
                Lines = lines
            });
        }

        /// <summary>Posts the shop's courier/freight cost once when the owner records it on the order.</summary>
        public static void PostOrderFreight(string companyCode, int orderId, long freightCost, PayWith payWith, int bankId)
        {
            if (freightCost <= 0) return;
            var order = GetOrder(companyCode, orderId);
            if (order == null || order.FreightPosted) return;
            if (FindEntry(companyCode, JournalSource.Freight, orderId) != null)
            {
                order.FreightPosted = true;
                order.FreightCost = freightCost;
                UpdateOrder(companyCode, order);
                return;
            }
            EnsureAccounts(companyCode);
            EnsureDocumentTypes(companyCode);
            if (payWith == PayWith.Credit) return;
            var bankError = ResolveBank(companyCode, payWith, ref bankId);
            if (bankError != null) return;

            Save(NikooTables.JournalEntries, companyCode, new StoreJournalEntry
            {
                Number = NextSerial(NikooTables.JournalEntries, companyCode, "Number", "SN-"),
                EntryDate = DateTime.Today,
                Description = "هزینه حمل سفارش " + order.OrderNumber,
                Source = JournalSource.Freight,
                SourceId = orderId,
                DocumentTypeId = TypeId(companyCode, DocumentTypeCodes.Freight),
                Lines = new List<StoreJournalLine>
                {
                    new StoreJournalLine { AccountId = IdOf(companyCode, AccountCodes.ShippingExpense), Debit = freightCost, Memo = order.OrderNumber },
                    new StoreJournalLine { AccountId = MoneyAccountId(companyCode, payWith, bankId, false), Credit = freightCost, Memo = order.OrderNumber }
                }
            });
            order.FreightCost = freightCost;
            order.FreightPosted = true;
            UpdateOrder(companyCode, order);
        }
    }
}
