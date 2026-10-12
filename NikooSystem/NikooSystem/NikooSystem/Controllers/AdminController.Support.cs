using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services;

namespace NikooSystem.Controllers
{
    /// <summary>
    /// Nikoo's support desk: the tickets of every store. Answers written here also go to the store owner's Bale accounts;
    /// the main site's own Bale accounts (اطلاع‌رسانی بله) get new tickets and can answer them from Bale.
    /// </summary>
    public partial class AdminController
    {
        public ActionResult Tickets(TicketGridQuery query)
        {
            if (string.IsNullOrEmpty(query.State)) query.State = "active";
            ViewBag.State = query.State;
            ViewBag.Awaiting = SupportDesk.CountAwaitingSupport();
            ViewBag.BaleLinked = BaleNotifier.Recipients(BaleNotifier.SiteScope).Count;
            ViewBag.BotReady = BaleNotifier.BotToken != null;
            return View(SupportDesk.Grid(query));
        }

        public ActionResult Ticket(int id)
        {
            var ticket = SupportDesk.Get(id);
            if (ticket == null) return HttpNotFound();
            SupportDesk.MarkRead(ticket, true);
            return View(new TicketPageVm
            {
                Ticket = ticket,
                Messages = SupportDesk.Messages(id),
                Store = TenantStore.GetTenantForAdmin(ticket.CompanyCode),
                StoreName = BaleNotifier.SiteName(ticket.CompanyCode),
                BaleRecipients = BaleNotifier.Recipients(ticket.CompanyCode).Count,
                BotReady = BaleNotifier.BotToken != null
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AnswerTicket(int id, string body, IEnumerable<HttpPostedFileBase> images, bool close = false)
        {
            var ticket = SupportDesk.Get(id);
            if (ticket == null) return HttpNotFound();
            List<string> saved;
            try
            {
                saved = SupportDesk.SaveImages(ticket.CompanyCode, images);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Ticket", new { id });
            }
            if (string.IsNullOrWhiteSpace(body) && saved.Count == 0)
            {
                TempData["Error"] = "متن پاسخ را بنویسید یا تصویری پیوست کنید.";
                return RedirectToAction("Ticket", new { id });
            }
            SupportDesk.Reply(ticket, true, body, Session["AdminUser"] as string, TicketVia.Panel, saved);
            if (close) SupportDesk.SetStatus(ticket, TicketStatus.Closed, true);
            var bale = BaleNotifier.Recipients(ticket.CompanyCode).Count > 0;
            TempData["Ok"] = "پاسخ ثبت شد" + (close ? " و تیکت بسته شد" : "")
                + (bale ? "؛ در بله هم برای مشتری فرستاده شد." : "؛ مشتری آن را در پنل خود می‌بیند (حساب بله‌ای وصل نکرده است).");
            return RedirectToAction("Ticket", new { id });
        }

        public ActionResult TicketImage(int id, string name) =>
            ShopAdminController.TicketImageFile(Response, SupportDesk.ImagePath(SupportDesk.GetMessage(id), name));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetTicketStatus(int id, TicketStatus status)
        {
            var ticket = SupportDesk.Get(id);
            if (ticket == null) return HttpNotFound();
            SupportDesk.SetStatus(ticket, status, true);
            TempData["Ok"] = "وضعیت تیکت: " + SupportLabels.Status(status, true) + ".";
            return RedirectToAction("Ticket", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetTicketPriority(int id, TicketPriority priority)
        {
            var ticket = SupportDesk.Get(id);
            if (ticket == null) return HttpNotFound();
            SupportDesk.SetPriority(ticket, priority);
            TempData["Ok"] = "اولویت تیکت: " + SupportLabels.Priority(priority) + ".";
            return RedirectToAction("Ticket", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteTicket(int id)
        {
            SupportDesk.Delete(id);
            TempData["Ok"] = "تیکت #" + id.ToFa() + " حذف شد.";
            return RedirectToAction("Tickets");
        }
    }
}
