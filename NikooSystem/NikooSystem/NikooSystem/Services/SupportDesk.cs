using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using Newtonsoft.Json.Linq;
using NikooSystem.Helpers;
using NikooSystem.Models;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>
    /// Support tickets of store owners to Nikoo's support. A new ticket and every message are also sent in Bale to the
    /// other side (the main site's Bale accounts, or the store's); answering that Bale message with «پاسخ» (reply) adds
    /// the answer to the ticket, and "/ticket text" opens a ticket from Bale.
    /// </summary>
    public static class SupportDesk
    {
        public const int MaxBody = 4000;
        /// <summary>New tickets a store may open per hour, against floods.</summary>
        public const int MaxNewPerHour = 5;
        public const int MaxImages = 4;
        public const int MaxImageBytes = 10 * 1024 * 1024;
        /// <summary>Longer side of a stored image; enough to read a full-screen screenshot.</summary>
        private const int ImageSide = 1920;

        private const string Scope = BaleNotifier.SiteScope;
        private const string ReplyHint = "↩️ برای پاسخ، روی همین پیام «پاسخ» (Reply) بزنید و بنویسید؛ عکس هم می‌توانید بفرستید.";

        private static readonly Regex HashReply = new Regex(@"^[#＃]\s*(\d{1,9})\s*[:：\-]?\s*(.*)$", RegexOptions.Singleline);
        private static readonly Regex Command = new Regex(@"^/(ticket|new|tickets|list)(?:@\w+)?(?:\s+(.*))?$", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, string> Sorts = new Dictionary<string, string>
        {
            { "updated", "t.[UpdatedAt]" },
            { "created", "t.[CreatedAt]" },
            { "id", "t.[Id]" },
            { "subject", "t.[Subject]" },
            { "company", "t.[CompanyCode]" }
        };

        // ---- Reading

        public static SupportTicket Get(int id) => NikooDb.Get(NikooTables.SupportTickets, NikooDb.Key(id));

        /// <summary>The ticket when it belongs to <paramref name="companyCode"/>; otherwise null.</summary>
        public static SupportTicket GetForStore(string companyCode, int id)
        {
            var ticket = Get(id);
            return ticket != null && string.Equals(ticket.CompanyCode, companyCode, StringComparison.OrdinalIgnoreCase) ? ticket : null;
        }

        public static List<SupportMessage> Messages(int ticketId) =>
            NikooDb.Query(NikooTables.SupportMessages, new Row { ["TicketId"] = ticketId }, "[Id]");

        public static SupportMessage GetMessage(int id) => NikooDb.Get(NikooTables.SupportMessages, NikooDb.Key(id));

        /// <summary>The file of an image attached to <paramref name="message"/>, or null.</summary>
        public static string ImagePath(SupportMessage message, string name)
        {
            if (message == null || string.IsNullOrEmpty(name) || name != Path.GetFileName(name) || !message.ImageList.Contains(name)) return null;
            var path = Path.Combine(ImageFolder(message.CompanyCode), name);
            return File.Exists(path) ? path : null;
        }

        /// <param name="companyCode">Only this store's tickets (the store panel); null = every store (Nikoo's panel).</param>
        public static PagedList<SupportTicket> Grid(TicketGridQuery query, string companyCode = null)
        {
            var filter = new SqlFilter();
            var q = BaleNotifier.LatinDigits((query.Q ?? "").Trim()).TrimStart('#');
            int id;
            if (int.TryParse(q, NumberStyles.None, CultureInfo.InvariantCulture, out id)) filter.Eq("Id", id);
            else filter.Search(query.Q, "Subject", "CompanyCode");

            var company = companyCode ?? (string.IsNullOrWhiteSpace(query.Company) ? null : query.Company.Trim());
            if (company != null) filter.Eq("CompanyCode", company);

            switch (query.State)
            {
                case "open": filter.Eq("Status", TicketStatus.Open.ToString()); break;
                case "answered": filter.Eq("Status", TicketStatus.Answered.ToString()); break;
                case "closed": filter.Eq("Status", TicketStatus.Closed.ToString()); break;
                case "active": filter.Where("t.[Status] <> {0}", TicketStatus.Closed.ToString()); break;
            }
            return NikooDb.Page(NikooTables.SupportTickets, filter, query.OrderBy(Sorts, "updated", true), query.Page, query.PageSize);
        }

        /// <summary>Tickets waiting for Nikoo's answer (the badge of Nikoo's panel).</summary>
        public static int CountAwaitingSupport() =>
            NikooDb.Count(NikooTables.SupportTickets, new SqlFilter().Eq("Status", TicketStatus.Open.ToString()));

        /// <summary>Tickets of the store with an answer it has not seen yet (the badge of the store panel).</summary>
        public static int CountUnreadForStore(string companyCode) =>
            NikooDb.Count(NikooTables.SupportTickets, new SqlFilter().Eq("CompanyCode", companyCode).Eq("UnreadByCustomer", true));

        public static bool CanOpen(string companyCode) =>
            NikooDb.Count(NikooTables.SupportTickets, new SqlFilter().Eq("CompanyCode", companyCode).AtLeast("CreatedAt", DateTime.Now.AddHours(-1))) < MaxNewPerHour;

        // ---- Writing

        /// <summary>Saves the images uploaded with a message; when one is rejected none is kept and its error is thrown.</summary>
        public static List<string> SaveImages(string companyCode, IEnumerable<HttpPostedFileBase> files)
        {
            var uploads = (files ?? Enumerable.Empty<HttpPostedFileBase>()).Where(f => f != null && f.ContentLength > 0).ToList();
            if (uploads.Count > MaxImages)
                throw new InvalidOperationException("هر پیام حداکثر " + MaxImages.ToFa() + " تصویر می‌تواند داشته باشد.");
            var names = new List<string>();
            try
            {
                foreach (var file in uploads)
                    names.Add(ImageService.SavePrivate(file.InputStream, file.FileName, ImageFolder(companyCode), ImageSide, MaxImageBytes));
            }
            catch
            {
                DeleteImages(companyCode, names);
                throw;
            }
            return names;
        }

        /// <param name="images">File names returned by <see cref="SaveImages"/>.</param>
        public static SupportTicket Open(string companyCode, NewTicketForm form, string author, TicketVia via, List<string> images = null)
        {
            var now = DateTime.Now;
            var ticket = new SupportTicket
            {
                CompanyCode = companyCode,
                Subject = BaleNotifier.Clip(form.Subject, 120),
                Category = form.Category,
                Priority = form.Priority,
                Status = TicketStatus.Open,
                CreatedAt = now,
                UpdatedAt = now,
                MessageCount = 1,
                UnreadBySupport = true
            };
            var message = NewMessage(ticket, false, form.Body, author, via, now, images);
            using (var tx = NikooDb.Begin())
            {
                ticket.Id = NikooDb.NextId(NikooTables.SupportTickets, null, tx);
                message.TicketId = ticket.Id;
                message.Id = NikooDb.NextId(NikooTables.SupportMessages, null, tx);
                NikooDb.Insert(NikooTables.SupportTickets, ticket, tx: tx);
                NikooDb.Insert(NikooTables.SupportMessages, message, tx: tx);
                tx.Commit();
            }
            TellSupport(ticket, message, true);
            return ticket;
        }

        /// <summary>Adds a message; support's answer marks the ticket answered, the customer's (re)opens it.</summary>
        public static SupportMessage Reply(SupportTicket ticket, bool fromSupport, string body, string author, TicketVia via, List<string> images = null)
        {
            var now = DateTime.Now;
            var message = NewMessage(ticket, fromSupport, body, author, via, now, images);
            ticket.Status = fromSupport ? TicketStatus.Answered : TicketStatus.Open;
            ticket.UpdatedAt = now;
            ticket.ClosedAt = null;
            ticket.UnreadBySupport = !fromSupport;
            ticket.UnreadByCustomer = fromSupport;
            using (var tx = NikooDb.Begin())
            {
                message.Id = NikooDb.NextId(NikooTables.SupportMessages, null, tx);
                NikooDb.Insert(NikooTables.SupportMessages, message, tx: tx);
                NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.SupportTickets.Name)
                    + " SET [Status] = @Status, [UpdatedAt] = @Now, [ClosedAt] = NULL, [MessageCount] = [MessageCount] + 1,"
                    + " [UnreadBySupport] = @ForSupport, [UnreadByCustomer] = @ForCustomer WHERE [Id] = @Id",
                    new Row
                    {
                        ["Status"] = ticket.Status.ToString(),
                        ["Now"] = now,
                        ["ForSupport"] = ticket.UnreadBySupport,
                        ["ForCustomer"] = ticket.UnreadByCustomer,
                        ["Id"] = ticket.Id
                    }, tx);
                tx.Commit();
            }
            ticket.MessageCount++;
            if (fromSupport) TellCustomer(ticket, message);
            else TellSupport(ticket, message, false);
            return message;
        }

        /// <summary>Closes or reopens the ticket; when support closes it the store is told in Bale.</summary>
        public static void SetStatus(SupportTicket ticket, TicketStatus status, bool bySupport)
        {
            if (ticket.Status == status) return;
            ticket.Status = status;
            ticket.ClosedAt = status == TicketStatus.Closed ? DateTime.Now : (DateTime?)null;
            NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.SupportTickets.Name) + " SET [Status] = @Status, [ClosedAt] = @ClosedAt WHERE [Id] = @Id",
                new Row { ["Status"] = status.ToString(), ["ClosedAt"] = ticket.ClosedAt, ["Id"] = ticket.Id });
            if (status == TicketStatus.Closed && bySupport)
            {
                var text = "✅ *تیکت #" + ticket.Id + " بسته شد* — " + BaleNotifier.SiteName(ticket.CompanyCode) + "\n"
                    + "موضوع: " + BaleNotifier.Plain(ticket.Subject) + "\n\n"
                    + "اگر مشکل هنوز باقی است، روی همین پیام «پاسخ» بزنید تا تیکت دوباره باز شود.";
                BaleNotifier.Notify(ticket.CompanyCode, NotifyEvent.Ticket, text, StorePath(ticket), (chat, id) => Track(chat, id, ticket, false));
            }
        }

        public static void SetPriority(SupportTicket ticket, TicketPriority priority)
        {
            ticket.Priority = priority;
            NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.SupportTickets.Name) + " SET [Priority] = @Priority WHERE [Id] = @Id",
                new Row { ["Priority"] = priority.ToString(), ["Id"] = ticket.Id });
        }

        /// <param name="bySupport">Nikoo's panel read the ticket; otherwise the store's.</param>
        public static void MarkRead(SupportTicket ticket, bool bySupport)
        {
            var column = bySupport ? "UnreadBySupport" : "UnreadByCustomer";
            if (bySupport ? !ticket.UnreadBySupport : !ticket.UnreadByCustomer) return;
            NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.SupportTickets.Name) + " SET " + NikooDb.Name(column) + " = 0 WHERE [Id] = @Id",
                new Row { ["Id"] = ticket.Id });
        }

        public static void Delete(int id)
        {
            var messages = Messages(id);
            using (var tx = NikooDb.Begin())
            {
                var key = new Row { ["TicketId"] = id };
                NikooDb.Delete(NikooTables.SupportBaleLinks, key, tx);
                NikooDb.Delete(NikooTables.SupportMessages, key, tx);
                NikooDb.Delete(NikooTables.SupportTickets, NikooDb.Key(id), tx);
                tx.Commit();
            }
            foreach (var m in messages) DeleteImages(m.CompanyCode, m.ImageList);
        }

        private static SupportMessage NewMessage(SupportTicket ticket, bool fromSupport, string body, string author, TicketVia via, DateTime at,
            List<string> images)
        {
            body = (body ?? "").Replace("\r\n", "\n").Trim();
            if (body.Length > MaxBody) body = body.Substring(0, MaxBody);
            return new SupportMessage
            {
                TicketId = ticket.Id,
                CompanyCode = ticket.CompanyCode,
                FromSupport = fromSupport,
                AuthorName = BaleNotifier.Clip(author, 100),
                Body = body,
                Images = images != null && images.Count > 0 ? string.Join(",", images) : null,
                Via = via,
                CreatedAt = at
            };
        }

        private static string ImageFolder(string companyCode) =>
            HostingEnvironment.MapPath(DigitalFileService.Root + companyCode.ToUpperInvariant() + "/tickets");

        private static void DeleteImages(string companyCode, IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                var path = Path.Combine(ImageFolder(companyCode), Path.GetFileName(name));
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (IOException)
                {
                }
            }
        }

        // ---- Bale: messages to each side

        private static void TellSupport(SupportTicket ticket, SupportMessage message, bool isNew)
        {
            var store = TenantStore.GetTenant(ticket.CompanyCode);
            var text = new StringBuilder();
            text.Append(isNew ? "🎫 *تیکت جدید #" : "💬 *پاسخ مشتری — تیکت #").Append(ticket.Id).Append("* — ")
                .Append(BaleNotifier.SiteName(ticket.CompanyCode)).Append(" (").Append(ticket.CompanyCode).Append(")\n");
            text.Append("موضوع: ").Append(BaleNotifier.Plain(ticket.Subject)).Append('\n');
            if (isNew)
            {
                text.Append("بخش: ").Append(SupportLabels.Category(ticket.Category)).Append('\n');
                text.Append("اولویت: ").Append(SupportLabels.Priority(ticket.Priority)).Append(ticket.Priority == TicketPriority.High ? " 🔴" : "").Append('\n');
            }
            var from = BaleNotifier.Plain(message.AuthorName);
            if (!string.IsNullOrEmpty(store?.OwnerMobile)) from = (from.Length > 0 ? from + " — " : "") + store.OwnerMobile;
            if (from.Length > 0) text.Append("از: ").Append(from).Append(message.Via == TicketVia.Bale ? " (از بله)" : "").Append('\n');
            text.Append('\n').Append(Content(message)).Append("\n\n").Append(ReplyHint);
            BaleNotifier.Notify(Scope, NotifyEvent.Ticket, text.ToString(), "/Admin/Ticket/" + ticket.Id, (chat, id) => Track(chat, id, ticket, true),
                Photos(ticket, message));
        }

        private static void TellCustomer(SupportTicket ticket, SupportMessage message)
        {
            var text = "🎧 *پاسخ پشتیبانی نیکو — تیکت #" + ticket.Id + "*\n"
                + "موضوع: " + BaleNotifier.Plain(ticket.Subject) + "\n\n"
                + Content(message) + "\n\n" + ReplyHint;
            BaleNotifier.Notify(ticket.CompanyCode, NotifyEvent.Ticket, text, StorePath(ticket), (chat, id) => Track(chat, id, ticket, false),
                Photos(ticket, message));
        }

        /// <summary>The message text for Bale, with a note of its images (they follow as replies to it).</summary>
        private static string Content(SupportMessage message)
        {
            var body = BaleNotifier.Plain(BaleNotifier.Clip(message.Body, 1500));
            var count = message.ImageList.Length;
            if (count == 0) return body;
            var note = "🖼 " + count.ToFa() + " تصویر پیوست (در ادامه)";
            return body.Length == 0 ? note : body + "\n\n" + note;
        }

        private static List<BalePhoto> Photos(SupportTicket ticket, SupportMessage message)
        {
            var names = message.ImageList;
            return names.Select((name, i) => new BalePhoto
            {
                Path = Path.Combine(ImageFolder(message.CompanyCode), name),
                Caption = "🖼 تیکت #" + ticket.Id + (names.Length > 1 ? " — تصویر " + (i + 1).ToFa() + " از " + names.Length.ToFa() : "")
            }).ToList();
        }

        private static string StorePath(SupportTicket ticket) => "/ShopAdmin/" + ticket.CompanyCode + "/Ticket/" + ticket.Id;

        private static void Track(long chatId, long messageId, SupportTicket ticket, bool toSupport)
        {
            try
            {
                NikooDb.Insert(NikooTables.SupportBaleLinks, new SupportBaleLink
                {
                    ChatId = chatId,
                    MessageId = messageId,
                    TicketId = ticket.Id,
                    CompanyCode = ticket.CompanyCode,
                    ToSupport = toSupport,
                    SentAt = DateTime.Now
                });
            }
            catch (SqlException ex) when (NikooDb.IsDuplicateKey(ex))
            {
            }
        }

        /// <summary>Answers in the chat; a reply to this answer continues the same ticket.</summary>
        private static async Task SayAsync(string token, long chatId, string text, SupportTicket ticket = null, bool toSupport = false)
        {
            var sent = await BaleNotifier.ReplyAsync(token, chatId, text).ConfigureAwait(false);
            var id = sent?.Value<long?>("message_id");
            if (ticket != null && id.HasValue) Track(chatId, id.Value, ticket, toSupport);
        }

        // ---- Bale: messages from each side

        /// <summary>Handles a ticket reply or command sent to the bot; false when the message is not about tickets.</summary>
        public static async Task<bool> HandleBaleAsync(string token, BaleIncoming m)
        {
            var text = (m.Text ?? "").Trim();

            if (m.ReplyToId.HasValue)
            {
                var link = NikooDb.Get(NikooTables.SupportBaleLinks, new Row { ["ChatId"] = m.ChatId, ["MessageId"] = m.ReplyToId.Value });
                if (link != null)
                {
                    await ReplyFromBaleAsync(token, m, Get(link.TicketId), link.ToSupport, text).ConfigureAwait(false);
                    return true;
                }
            }

            // "#12 text" answers ticket 12 without replying to its message
            var hash = HashReply.Match(BaleNotifier.LatinDigits(text));
            if (hash.Success)
            {
                var ticket = Get(int.Parse(hash.Groups[1].Value, CultureInfo.InvariantCulture));
                var asSupport = BaleNotifier.IsRecipient(Scope, m.ChatId);
                if (ticket != null && !asSupport && !BaleNotifier.IsRecipient(ticket.CompanyCode, m.ChatId)) ticket = null;
                if (ticket == null)
                    await SayAsync(token, m.ChatId, "تیکت #" + hash.Groups[1].Value + " برای این حساب پیدا نشد. فهرست تیکت‌ها: /tickets").ConfigureAwait(false);
                else
                    await ReplyFromBaleAsync(token, m, ticket, asSupport, text.Substring(hash.Groups[2].Index)).ConfigureAwait(false);
                return true;
            }

            var command = Command.Match(text);
            if (!command.Success) return false;
            var name = command.Groups[1].Value.ToLowerInvariant();
            if (name == "tickets" || name == "list") await ListAsync(token, m).ConfigureAwait(false);
            else await OpenFromBaleAsync(token, m, command.Groups[2].Value.Trim()).ConfigureAwait(false);
            return true;
        }

        private static async Task ReplyFromBaleAsync(string token, BaleIncoming m, SupportTicket ticket, bool asSupport, string body)
        {
            if (ticket == null)
            {
                await SayAsync(token, m.ChatId, "این تیکت دیگر وجود ندارد.").ConfigureAwait(false);
                return;
            }
            // the account must still be linked to the side it answers for
            var allowed = asSupport
                ? BaleNotifier.IsRecipient(Scope, m.ChatId)
                : BaleNotifier.IsRecipient(ticket.CompanyCode, m.ChatId) && !TenantStore.IsDemo(ticket.CompanyCode);
            if (!allowed)
            {
                await SayAsync(token, m.ChatId, "این حساب بله دیگر به این سایت وصل نیست؛ پاسخ ثبت نشد.").ConfigureAwait(false);
                return;
            }
            body = (body ?? "").Trim();
            if (body.Length == 0 && m.ImageFileId == null)
            {
                await SayAsync(token, m.ChatId, m.HasMedia ? MediaNote : "متن پاسخ خالی است.", ticket, asSupport).ConfigureAwait(false);
                return;
            }
            var images = await SaveBaleImageAsync(token, m, ticket.CompanyCode, ticket, asSupport).ConfigureAwait(false);
            if (images == null) return;

            var author = !string.IsNullOrEmpty(m.Name) ? m.Name : asSupport ? "پشتیبانی" : OwnerName(ticket.CompanyCode);
            Reply(ticket, asSupport, body, author, TicketVia.Bale, images);
            await SayAsync(token, m.ChatId, "✅ " + (images.Count > 0 && body.Length == 0 ? "تصویر شما" : "پاسخ شما") + " در تیکت #" + ticket.Id + (asSupport
                ? " ثبت شد و برای «" + BaleNotifier.SiteName(ticket.CompanyCode) + "» فرستاده شد."
                : " ثبت شد و به پشتیبانی نیکو رسید. پاسخ را همین‌جا دریافت می‌کنید."), ticket, asSupport).ConfigureAwait(false);
        }

        private static async Task OpenFromBaleAsync(string token, BaleIncoming m, string body)
        {
            var stores = BaleNotifier.ScopesOf(m.ChatId)
                .Where(s => s != Scope && !TenantStore.IsDemo(s) && TenantStore.GetTenant(s) != null)
                .ToList();
            if (stores.Count == 0)
            {
                await SayAsync(token, m.ChatId, BaleNotifier.IsRecipient(Scope, m.ChatId)
                    ? "این دستور برای مشتریان است؛ شما پشتیبان نیکو هستید و تیکت‌ها را با «پاسخ» روی پیام هر تیکت جواب می‌دهید. فهرست: /tickets"
                    : "ابتدا این حساب بله را از پنل مدیریت سایت خود («اطلاع‌رسانی بله») وصل کنید، سپس تیکت بفرستید.").ConfigureAwait(false);
                return;
            }

            var company = stores.Count == 1 ? stores[0] : null;
            if (company == null)
            {
                var first = body.Split(new[] { ' ', '\n' }, 2);
                company = stores.FirstOrDefault(s => string.Equals(s, first[0], StringComparison.OrdinalIgnoreCase));
                if (company == null)
                {
                    await SayAsync(token, m.ChatId, "این حساب به چند سایت وصل است؛ کد سایت را اول بنویسید:\n/ticket کد_سایت متن درخواست\n\nکدهای شما: "
                        + string.Join("، ", stores)).ConfigureAwait(false);
                    return;
                }
                body = first.Length > 1 ? first[1].Trim() : "";
            }
            if (body.Length == 0)
            {
                await SayAsync(token, m.ChatId, "متن درخواست را بعد از /ticket بنویسید؛ مثلاً:\n/ticket پرداخت آنلاین سایت خطا می‌دهد").ConfigureAwait(false);
                return;
            }
            if (!CanOpen(company))
            {
                await SayAsync(token, m.ChatId, "در یک ساعت گذشته " + MaxNewPerHour + " تیکت ثبت کرده‌اید؛ لطفاً در همان تیکت‌ها ادامه دهید.").ConfigureAwait(false);
                return;
            }
            var images = await SaveBaleImageAsync(token, m, company, null, false).ConfigureAwait(false);
            if (images == null) return;

            var firstLine = body.Split('\n')[0].Trim();
            var ticket = Open(company, new NewTicketForm
            {
                Subject = firstLine.Length > 80 ? firstLine.Substring(0, 80) + "…" : firstLine,
                Category = TicketCategory.Other,
                Priority = TicketPriority.Normal,
                Body = body
            }, !string.IsNullOrEmpty(m.Name) ? m.Name : OwnerName(company), TicketVia.Bale, images);

            await SayAsync(token, m.ChatId, "🎫 تیکت #" + ticket.Id + " برای «" + BaleNotifier.SiteName(company) + "» ثبت شد و به پشتیبانی نیکو رسید.\n"
                + "پاسخ را همین‌جا دریافت می‌کنید؛ برای افزودن توضیح، روی همین پیام «پاسخ» بزنید.", ticket, false).ConfigureAwait(false);
        }

        private static async Task ListAsync(string token, BaleIncoming m)
        {
            var text = new StringBuilder();
            if (BaleNotifier.IsRecipient(Scope, m.ChatId))
            {
                var waiting = Grid(new TicketGridQuery { State = "open", Sort = "updated", Dir = "asc", Size = 10 });
                text.Append("📋 *تیکت‌های در انتظار پاسخ: ").Append(waiting.TotalCount).Append("*\n");
                foreach (var t in waiting)
                    text.Append("\n#").Append(t.Id).Append(" — ").Append(BaleNotifier.SiteName(t.CompanyCode)).Append(" — ")
                        .Append(BaleNotifier.Plain(t.Subject)).Append(t.Priority == TicketPriority.High ? " 🔴" : "");
                text.Append(waiting.TotalCount == 0 ? "\nهمه تیکت‌ها پاسخ داده شده‌اند 👌" : "\n\nبرای پاسخ: «#شماره متن پاسخ» بفرستید.");
            }
            else
            {
                var stores = BaleNotifier.ScopesOf(m.ChatId).Where(s => s != Scope && !TenantStore.IsDemo(s)).ToList();
                if (stores.Count == 0) return;
                var filter = new SqlFilter().In("CompanyCode", stores);
                var tickets = NikooDb.Page(NikooTables.SupportTickets, filter, "t.[UpdatedAt] DESC", 1, 8);
                text.Append("📋 *تیکت‌های شما*\n");
                foreach (var t in tickets)
                    text.Append("\n#").Append(t.Id).Append(" — ").Append(BaleNotifier.Plain(t.Subject)).Append(" — ")
                        .Append(SupportLabels.Status(t.Status, false));
                text.Append(tickets.Count == 0 ? "\nهنوز تیکتی ندارید." : "\n\nبرای پاسخ: «#شماره متن» بفرستید.");
                text.Append("\nتیکت تازه: /ticket متن درخواست");
            }
            await SayAsync(token, m.ChatId, text.ToString()).ConfigureAwait(false);
        }

        private const string MediaNote = "در تیکت متن و تصویر ثبت می‌شود؛ صدا، ویدیو و فایل‌های دیگر پذیرفته نمی‌شود. توضیح را بنویسید یا عکس بفرستید.";

        /// <summary>
        /// Keeps the photo of the message (none: an empty list). When it cannot be saved the sender is told why and null is returned.
        /// </summary>
        private static async Task<List<string>> SaveBaleImageAsync(string token, BaleIncoming m, string companyCode, SupportTicket ticket, bool toSupport)
        {
            var images = new List<string>();
            if (m.ImageFileId == null) return images;
            string error;
            try
            {
                var bytes = await BaleApi.DownloadAsync(token, m.ImageFileId, MaxImageBytes).ConfigureAwait(false);
                using (var stream = new MemoryStream(bytes))
                    images.Add(ImageService.SavePrivate(stream, m.ImageFileName, ImageFolder(companyCode), ImageSide, MaxImageBytes));
                return images;
            }
            catch (InvalidOperationException ex)
            {
                error = ex.Message;
            }
            catch (Exception ex) when (ex is BaleException || ex is HttpRequestException || ex is TaskCanceledException)
            {
                error = "دریافت تصویر از بله ممکن نشد؛ دوباره بفرستید.";
            }
            await SayAsync(token, m.ChatId, "⚠️ " + error + " پیام ثبت نشد.", ticket, toSupport).ConfigureAwait(false);
            return null;
        }

        private static string OwnerName(string companyCode)
        {
            var tenant = TenantStore.GetTenant(companyCode);
            return !string.IsNullOrWhiteSpace(tenant?.OwnerName) ? tenant.OwnerName : BaleNotifier.SiteName(companyCode);
        }
    }
}
