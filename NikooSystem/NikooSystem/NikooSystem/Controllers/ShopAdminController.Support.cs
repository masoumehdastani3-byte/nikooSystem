using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// Tickets of the store owner to Nikoo's support (see <see cref="SupportDesk"/>). Support's answers also arrive in the
    /// owner's linked Bale accounts, where a «پاسخ» to them is added to the ticket.
    /// </summary>
    public partial class ShopAdminController
    {
        private const string DemoTicketNote = "این یک سایت نمایشی است و از آن تیکت فرستاده نمی‌شود؛ پس از ساخت سایت خودتان از همین صفحه با پشتیبانی در ارتباط باشید.";

        public ActionResult Support(TicketGridQuery query)
        {
            ViewBag.IsDemo = TenantStore.IsDemo(CompanyCode);
            ViewBag.BaleLinked = BaleNotifier.Recipients(CompanyCode).Count;
            ViewBag.BotReady = BaleNotifier.BotToken != null;
            return View(SupportDesk.Grid(query, CompanyCode));
        }

        [HttpGet]
        public ActionResult NewTicket(TicketCategory category = TicketCategory.Technical, string subject = null)
        {
            if (TenantStore.IsDemo(CompanyCode)) return Fail(DemoTicketNote, "Support");
            ModelState.Clear();
            return View(new NewTicketForm { Category = category, Subject = Clip(subject, 120) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult NewTicket(NewTicketForm form, IEnumerable<HttpPostedFileBase> images)
        {
            if (TenantStore.IsDemo(CompanyCode)) return Fail(DemoTicketNote, "Support");
            if (!ModelState.IsValid) return View(form);
            if (!SupportDesk.CanOpen(CompanyCode))
            {
                ModelState.AddModelError("", "در یک ساعت گذشته " + SupportDesk.MaxNewPerHour + " تیکت ثبت کرده‌اید؛ لطفاً در همان تیکت‌ها ادامه دهید.");
                return View(form);
            }
            List<string> saved;
            try
            {
                saved = SupportDesk.SaveImages(CompanyCode, images);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message + " تصویرها را دوباره انتخاب کنید.");
                return View(form);
            }
            var ticket = SupportDesk.Open(CompanyCode, form, OwnerName, TicketVia.Panel, saved);
            return Ok("تیکت #" + ticket.Id + " ثبت شد و به پشتیبانی نیکو رسید؛ پاسخ را همین‌جا و در بله می‌بینید.", "Ticket", new { id = ticket.Id });
        }

        public ActionResult Ticket(int id)
        {
            var ticket = SupportDesk.GetForStore(CompanyCode, id);
            if (ticket == null) return ToAction("Support");
            SupportDesk.MarkRead(ticket, false);
            return View(new TicketPageVm
            {
                Ticket = ticket,
                Messages = SupportDesk.Messages(id),
                BaleRecipients = BaleNotifier.Recipients(CompanyCode).Count,
                BotReady = BaleNotifier.BotToken != null,
                IsDemo = TenantStore.IsDemo(CompanyCode)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReplyTicket(int id, string body, IEnumerable<HttpPostedFileBase> images)
        {
            var ticket = SupportDesk.GetForStore(CompanyCode, id);
            if (ticket == null) return ToAction("Support");
            if (TenantStore.IsDemo(CompanyCode)) return Fail(DemoTicketNote, "Ticket", new { id });
            List<string> saved;
            try
            {
                saved = SupportDesk.SaveImages(CompanyCode, images);
            }
            catch (InvalidOperationException ex)
            {
                return Fail(ex.Message, "Ticket", new { id });
            }
            if (string.IsNullOrWhiteSpace(body) && saved.Count == 0) return Fail("متن پاسخ را بنویسید یا تصویری پیوست کنید.", "Ticket", new { id });
            SupportDesk.Reply(ticket, false, body, OwnerName, TicketVia.Panel, saved);
            return Ok("پاسخ شما ثبت شد و به پشتیبانی نیکو رسید.", "Ticket", new { id });
        }

        /// <summary>An image attached to a message of this store's tickets.</summary>
        public ActionResult TicketImage(int id, string name)
        {
            var message = SupportDesk.GetMessage(id);
            if (message == null || !string.Equals(message.CompanyCode, CompanyCode, StringComparison.OrdinalIgnoreCase)) return HttpNotFound();
            return TicketImageFile(Response, SupportDesk.ImagePath(message, name));
        }

        /// <summary>A private image: cached by the browser only, never by proxies.</summary>
        internal static ActionResult TicketImageFile(HttpResponseBase response, string path)
        {
            if (path == null) return new HttpNotFoundResult();
            response.Cache.SetCacheability(HttpCacheability.Private);
            response.Cache.SetMaxAge(TimeSpan.FromDays(30));
            response.AddHeader("X-Content-Type-Options", "nosniff");
            return new FilePathResult(path, MimeMapping.GetMimeMapping(path));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CloseTicket(int id)
        {
            var ticket = SupportDesk.GetForStore(CompanyCode, id);
            if (ticket == null) return ToAction("Support");
            SupportDesk.SetStatus(ticket, TicketStatus.Closed, false);
            return Ok("تیکت بسته شد. اگر دوباره سؤالی داشتید، در همین تیکت بنویسید تا باز شود.", "Ticket", new { id });
        }

        private string OwnerName =>
            !string.IsNullOrWhiteSpace(Tenant?.OwnerName) ? Tenant.OwnerName : CurrentSettings?.StoreName ?? CompanyCode;
    }
}
