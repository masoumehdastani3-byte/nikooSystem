using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NikooSystem.Models;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>An error answered by the Bale bot API.</summary>
    public class BaleException : Exception
    {
        public BaleException(string message, int code) : base(message)
        {
            Code = code;
        }

        public int Code { get; }
    }

    /// <summary>The Bale bot API (https://docs.bale.ai): Telegram-style methods posted as JSON to tapi.bale.ai.</summary>
    internal static class BaleApi
    {
        private const string Base = "https://tapi.bale.ai/bot";
        private const string FileBase = "https://tapi.bale.ai/file/bot";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        public static Task<JToken> CallAsync(string token, string method, object payload) =>
            PostAsync(token, method, new StringContent(JsonConvert.SerializeObject(payload ?? new object()), Encoding.UTF8, "application/json"));

        /// <summary>Uploads an image file with sendPhoto; returns the sent message.</summary>
        public static async Task<JToken> SendPhotoAsync(string token, long chatId, string path, string caption, long? replyTo)
        {
            using (var form = new MultipartFormDataContent())
            using (var stream = File.OpenRead(path))
            {
                form.Add(Field(chatId.ToString(CultureInfo.InvariantCulture)), "chat_id");
                if (!string.IsNullOrEmpty(caption)) form.Add(Field(caption), "caption");
                if (replyTo.HasValue) form.Add(Field(replyTo.Value.ToString(CultureInfo.InvariantCulture)), "reply_to_message_id");
                var photo = new StreamContent(stream);
                photo.Headers.ContentType = new MediaTypeHeaderValue(MimeMapping.GetMimeMapping(path));
                form.Add(photo, "photo", Path.GetFileName(path));
                return await PostAsync(token, "sendPhoto", form).ConfigureAwait(false);
            }
        }

        /// <summary>Downloads a file sent to the bot (getFile, then the file address).</summary>
        public static async Task<byte[]> DownloadAsync(string token, string fileId, long maxBytes)
        {
            var file = await CallAsync(token, "getFile", new { file_id = fileId }).ConfigureAwait(false);
            var path = file?.Value<string>("file_path");
            if (string.IsNullOrEmpty(path)) throw new BaleException("فایل در بله پیدا نشد.", 404);
            if (file.Value<long?>("file_size") > maxBytes) throw new InvalidOperationException(TooLarge(maxBytes));
            using (var response = await Http.GetAsync(FileBase + token + "/" + path, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) throw new BaleException("دریافت فایل از بله ممکن نشد.", (int)response.StatusCode);
                if (response.Content.Headers.ContentLength > maxBytes) throw new InvalidOperationException(TooLarge(maxBytes));
                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (bytes.Length > maxBytes) throw new InvalidOperationException(TooLarge(maxBytes));
                return bytes;
            }
        }

        private static string TooLarge(long maxBytes) => "حجم تصویر باید کمتر از " + maxBytes / (1024 * 1024) + " مگابایت باشد.";

        /// <summary>A plain form field (no content-type header, as the bot API expects).</summary>
        private static StringContent Field(string value)
        {
            var content = new StringContent(value, Encoding.UTF8);
            content.Headers.ContentType = null;
            return content;
        }

        private static async Task<JToken> PostAsync(string token, string method, HttpContent content)
        {
            using (var response = await Http.PostAsync(Base + token + "/" + method, content).ConfigureAwait(false))
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JObject json;
                try
                {
                    json = JObject.Parse(body);
                }
                catch (JsonException)
                {
                    throw new BaleException("پاسخ نامعتبر از سرور بله (HTTP " + (int)response.StatusCode + ")", (int)response.StatusCode);
                }
                if (json.Value<bool?>("ok") == true) return json["result"];
                throw new BaleException(json.Value<string>("description") ?? "HTTP " + (int)response.StatusCode,
                    json.Value<int?>("error_code") ?? (int)response.StatusCode);
            }
        }
    }

    /// <summary>
    /// Sends admins a Bale message when something happens on their site (an order, a payment, a booking, a message…).
    /// Every store is served by Nikoo's single bot, whose token the main site's admin enters; an owner links a Bale account
    /// by opening the bot with a one-time code (ble.ir/BOT?start=CODE). Messages are sent in the background so a slow or
    /// unreachable Bale never delays the visitor's request.
    /// </summary>
    public static class BaleNotifier
    {
        /// <summary>The CompanyCode under which the main site's own settings and recipients are kept.</summary>
        public const string SiteScope = "_NIKOO";
        public const int LinkCodeMinutes = 15;
        public const int MaxRecipients = 5;

        private static readonly SemaphoreSlim PollGate = new SemaphoreSlim(1, 1);
        private static readonly Regex CodePattern = new Regex(@"^(?:/start\s*)?(\d{8})$");

        // ---- Settings and recipients

        public static NotifySettings Settings(string scope) =>
            NikooDb.Get(NikooTables.NotifySettings, NikooDb.Key(scope)) ?? new NotifySettings { CompanyCode = scope };

        public static List<BaleRecipient> Recipients(string scope) =>
            NikooDb.List(NikooTables.BaleRecipients, scope, "[Id]");

        public static bool RemoveRecipient(string scope, int id) =>
            NikooDb.Delete(NikooTables.BaleRecipients, NikooDb.Key(scope, id));

        /// <summary>The admin page of a site; <paramref name="events"/> lists the events this kind of site has.</summary>
        public static NotifyPageVm Page(string scope, Func<NotifySettings, List<NotifyEventOption>> events, bool isDemo)
        {
            var settings = Settings(scope);
            var bot = scope == SiteScope ? settings : Settings(SiteScope);
            var live = settings.HasLiveLinkCode(DateTime.Now);
            return new NotifyPageVm
            {
                IsSite = scope == SiteScope,
                BotReady = BotToken != null,
                BotUsername = bot.BotUsername,
                BotName = bot.BotName,
                IsDemo = isDemo,
                Settings = settings,
                Recipients = Recipients(scope),
                Events = events(settings),
                LinkCode = live ? settings.LinkCode : null,
                LinkUrl = live ? LinkUrl(settings.LinkCode) : null,
                LinkCodeExpiresAt = live ? settings.LinkCodeExpiresAt : null
            };
        }

        /// <summary>Switches off the events of <paramref name="offered"/> that are not in <paramref name="enabled"/>.</summary>
        public static void SaveEnabled(string scope, IEnumerable<NotifyEventOption> offered, IEnumerable<string> enabled)
        {
            var on = new HashSet<string>(enabled ?? new string[0]);
            SaveMuted(scope, offered.Where(o => !on.Contains(o.Event.ToString())).Select(o => o.Event));
        }

        public static void SaveMuted(string scope, IEnumerable<NotifyEvent> muted)
        {
            var s = new NotifySettings();
            s.SetMuted(muted);
            Patch(scope, new Row { ["MutedEvents"] = s.MutedEvents });
        }

        /// <summary>The bot token from the main site's settings, or from Web.config (Bale.BotToken).</summary>
        public static string BotToken
        {
            get
            {
                var token = Settings(SiteScope).BotToken;
                if (string.IsNullOrWhiteSpace(token)) token = ConfigurationManager.AppSettings["Bale.BotToken"];
                return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
            }
        }

        /// <summary>Checks the token with getMe and keeps it with the bot's name; an empty token turns notifications off.</summary>
        public static async Task<string> SaveBotAsync(string token)
        {
            token = (token ?? "").Trim();
            if (token.Length == 0)
            {
                Patch(SiteScope, new Row { ["BotToken"] = null, ["BotUsername"] = null, ["BotName"] = null, ["UpdateOffset"] = 0L });
                return null;
            }
            if (!Regex.IsMatch(token, @"^\d+:[A-Za-z0-9_\-]{10,}$"))
                return "توکن بازو باید به شکل 123456789:AbC... باشد؛ آن را از «بازوفاده» (@botfather) در بله بگیرید.";
            try
            {
                var me = await BaleApi.CallAsync(token, "getMe", null).ConfigureAwait(false);
                Patch(SiteScope, new Row
                {
                    ["BotToken"] = token,
                    ["BotUsername"] = me.Value<string>("username"),
                    ["BotName"] = me.Value<string>("first_name"),
                    ["UpdateOffset"] = 0L
                });
                return null;
            }
            catch (Exception ex)
            {
                return "بله این توکن را نپذیرفت: " + Describe(ex);
            }
        }

        // ---- Linking a Bale account

        /// <summary>A fresh one-time code for this site, valid for <see cref="LinkCodeMinutes"/> minutes.</summary>
        public static string NewLinkCode(string scope)
        {
            var bytes = new byte[4];
            string code;
            do
            {
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
                code = (10000000 + BitConverter.ToUInt32(bytes, 0) % 90000000).ToString(CultureInfo.InvariantCulture);
            }
            while (NikooDb.Exists(NikooTables.NotifySettings, new Row { ["LinkCode"] = code }));

            Patch(scope, new Row { ["LinkCode"] = code, ["LinkCodeExpiresAt"] = DateTime.Now.AddMinutes(LinkCodeMinutes) });
            return code;
        }

        public static string LinkUrl(string code)
        {
            var bot = Settings(SiteScope).BotUsername;
            return string.IsNullOrEmpty(bot) || string.IsNullOrEmpty(code) ? null : "https://ble.ir/" + bot + "?start=" + code;
        }

        /// <summary>
        /// Reads the messages sent to the bot since the last call. A "/start CODE" (or the bare code) links its sender to
        /// the site that issued the code; support-ticket messages go to <see cref="SupportDesk"/>; anything else gets a
        /// short help. Returns an error text, or null.
        /// </summary>
        public static async Task<string> PollAsync()
        {
            var token = BotToken;
            if (token == null) return "بازوی بله هنوز تنظیم نشده است.";
            if (!await PollGate.WaitAsync(0).ConfigureAwait(false)) return null;
            try
            {
                EnsureRow(SiteScope);
                var offset = Settings(SiteScope).UpdateOffset;
                var updates = await BaleApi.CallAsync(token, "getUpdates", new { offset, limit = 100, timeout = 0 }).ConfigureAwait(false) as JArray;
                foreach (var update in updates ?? new JArray())
                {
                    if (!Claim(update.Value<long>("update_id"))) continue;
                    var message = update["message"];
                    if (message == null || message["chat"]?.Value<string>("type") != "private") continue;
                    var from = message["from"];
                    var name = (from?.Value<string>("first_name") + " " + from?.Value<string>("last_name")).Trim();
                    // a photo comes in several sizes; an image may also be sent as a file
                    var photo = (message["photo"] as JArray)?.OrderBy(p => p.Value<long?>("file_size") ?? p.Value<long?>("width") ?? 0).LastOrDefault();
                    var document = message["document"];
                    var imageFile = document != null && (document.Value<string>("mime_type") ?? "").StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? document : null;
                    await HandleMessageAsync(token, new BaleIncoming
                    {
                        ChatId = message["chat"].Value<long>("id"),
                        Name = Clip(name, 100),
                        Username = Clip(from?.Value<string>("username"), 100),
                        Text = message.Value<string>("text") ?? message.Value<string>("caption"),
                        ReplyToId = message["reply_to_message"]?.Value<long?>("message_id"),
                        ImageFileId = photo?.Value<string>("file_id") ?? imageFile?.Value<string>("file_id"),
                        ImageFileName = imageFile?.Value<string>("file_name") ?? "photo.jpg",
                        HasMedia = photo != null || document != null || message["voice"] != null || message["video"] != null
                            || message["audio"] != null || message["sticker"] != null
                    }).ConfigureAwait(false);
                }
                return null;
            }
            catch (Exception ex)
            {
                return Describe(ex);
            }
            finally
            {
                PollGate.Release();
            }
        }

        /// <summary>Called every few seconds by the application, so replies written in Bale reach the panels without a page open.</summary>
        public static void PollInBackground()
        {
            try
            {
                if (BotToken != null) PollAsync().Wait();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Bale poll: " + ex);
            }
        }

        /// <summary>
        /// Marks an update as handled; false when it already was (by an earlier poll or by another server process
        /// sharing the database), so every message is handled exactly once.
        /// </summary>
        private static bool Claim(long updateId)
        {
            return NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.NotifySettings.Name)
                + " SET [UpdateOffset] = @Next WHERE [CompanyCode] = @Scope AND [UpdateOffset] <= @Id",
                new Row { ["Next"] = updateId + 1, ["Scope"] = SiteScope, ["Id"] = updateId }) > 0;
        }

        private static async Task HandleMessageAsync(string token, BaleIncoming m)
        {
            var chatId = m.ChatId;
            var name = m.Name;
            var match = CodePattern.Match(LatinDigits((m.Text ?? "").Trim()));
            if (!match.Success)
            {
                if (await SupportDesk.HandleBaleAsync(token, m).ConfigureAwait(false)) return;
                await ReplyAsync(token, chatId,
                    "سلام! من بازوی نیکو سیستم هستم. سفارش‌ها، پرداخت‌ها، نوبت‌ها و پیام‌های سایت شما را همین‌جا خبر می‌دهم "
                    + "و پیام‌های پشتیبانی نیکو هم از همین‌جا رد و بدل می‌شود.\n\n"
                    + "🔗 اتصال: در پنل مدیریت سایت خود به «اطلاع‌رسانی بله» بروید و دکمه «اتصال حساب بله» را بزنید.\n"
                    + "🎫 تیکت تازه: /ticket و بعد متن درخواست؛ مثلاً «/ticket درگاه پرداخت خطا می‌دهد»\n"
                    + "↩️ پاسخ به تیکت: روی پیام تیکت در همین گفتگو «پاسخ» (Reply) بزنید و بنویسید؛ عکس و اسکرین‌شات هم می‌توانید بفرستید.\n"
                    + "📋 فهرست تیکت‌ها: /tickets").ConfigureAwait(false);
                return;
            }

            var target = NikooDb.Get(NikooTables.NotifySettings, new Row { ["LinkCode"] = match.Groups[1].Value });
            if (target == null || !target.HasLiveLinkCode(DateTime.Now))
            {
                await ReplyAsync(token, chatId, "این کد اتصال معتبر نیست یا منقضی شده است؛ از پنل مدیریت یک کد تازه بگیرید.").ConfigureAwait(false);
                return;
            }

            var scope = target.CompanyCode;
            var site = SiteName(scope);
            var existing = Recipients(scope);
            Patch(scope, new Row { ["LinkCode"] = null, ["LinkCodeExpiresAt"] = null });
            if (existing.Any(r => r.ChatId == chatId))
            {
                await ReplyAsync(token, chatId, "این حساب از قبل پیام‌های «" + site + "» را دریافت می‌کند.").ConfigureAwait(false);
                return;
            }
            if (existing.Count >= MaxRecipients)
            {
                await ReplyAsync(token, chatId, "به «" + site + "» حداکثر " + MaxRecipients + " حساب می‌تواند وصل شود؛ ابتدا یکی را از پنل حذف کنید.").ConfigureAwait(false);
                return;
            }

            AddRecipient(scope, new BaleRecipient { ChatId = chatId, Name = name, Username = m.Username });
            await ReplyAsync(token, chatId, "✅ این حساب به اطلاع‌رسانی «" + site + "» وصل شد.\n"
                + "از این پس سفارش‌ها، پرداخت‌ها و پیام‌های تازه را همین‌جا دریافت می‌کنید. فهرست رویدادها را از پنل مدیریت تغییر دهید.").ConfigureAwait(false);

            foreach (var other in existing)
                await ReplyAsync(token, other.ChatId, "🔗 حساب «" + (string.IsNullOrEmpty(name) ? "بدون نام" : Plain(name))
                    + "» هم به اطلاع‌رسانی «" + site + "» وصل شد. اگر شما این کار را نکرده‌اید، آن را از پنل مدیریت حذف کنید.").ConfigureAwait(false);
        }

        private static void AddRecipient(string scope, BaleRecipient recipient)
        {
            using (var tx = NikooDb.Begin())
            {
                recipient.Id = NikooDb.NextId(NikooTables.BaleRecipients, scope, tx);
                recipient.LinkedAt = DateTime.Now;
                NikooDb.Insert(NikooTables.BaleRecipients, recipient, scope, tx);
                tx.Commit();
            }
        }

        // ---- Sending

        /// <summary>
        /// Queues a message for the site's recipients unless the event is switched off. <paramref name="path"/> is a page of
        /// the admin panel (e.g. "/ShopAdmin/CODE/OrderDetails/5"); it is turned into a link on the address of this request.
        /// </summary>
        /// <param name="sent">Called with the chat and message id of every message delivered, so replies to it can be traced.</param>
        /// <param name="photos">Images sent after the message, as replies to it.</param>
        public static void Notify(string scope, NotifyEvent e, string text, string path = null, Action<long, long> sent = null,
            IReadOnlyList<BalePhoto> photos = null)
        {
            if (string.IsNullOrEmpty(scope) || (scope != SiteScope && TenantStore.IsDemo(scope))) return;
            var link = AbsoluteUrl(path);
            Func<CancellationToken, Task> work = ct => SendEventAsync(scope, e, text, link, sent, photos);
            try
            {
                HostingEnvironment.QueueBackgroundWorkItem(work);
            }
            catch (InvalidOperationException)
            {
                Task.Run(() => work(CancellationToken.None));
            }
        }

        /// <summary>Sends a test message to every recipient now; returns an error text, or null.</summary>
        public static async Task<string> SendTestAsync(string scope)
        {
            var token = BotToken;
            if (token == null) return "بازوی بله هنوز تنظیم نشده است.";
            var recipients = Recipients(scope);
            if (recipients.Count == 0) return "هنوز هیچ حساب بله‌ای وصل نشده است.";
            var text = "🔔 *پیام آزمایشی* \nاطلاع‌رسانی «" + SiteName(scope) + "» در بله درست کار می‌کند.\n"
                + DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
            return await SendToAllAsync(scope, token, recipients, text, null).ConfigureAwait(false);
        }

        private static async Task SendEventAsync(string scope, NotifyEvent e, string text, string link, Action<long, long> sent,
            IReadOnlyList<BalePhoto> photos)
        {
            try
            {
                var token = BotToken;
                if (token == null || Settings(scope).IsMuted(e)) return;
                var recipients = Recipients(scope);
                if (recipients.Count > 0) await SendToAllAsync(scope, token, recipients, text, link, sent, photos).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Bale notify " + scope + ": " + ex);
            }
        }

        private static async Task<string> SendToAllAsync(string scope, string token, List<BaleRecipient> recipients, string text, string link,
            Action<long, long> sent = null, IReadOnlyList<BalePhoto> photos = null)
        {
            string error = null;
            foreach (var r in recipients)
            {
                try
                {
                    var messageId = await SendAsync(token, r.ChatId, text, link).ConfigureAwait(false);
                    if (messageId.HasValue) sent?.Invoke(r.ChatId, messageId.Value);
                    foreach (var photo in photos ?? new BalePhoto[0])
                    {
                        if (!File.Exists(photo.Path)) continue;
                        var result = await BaleApi.SendPhotoAsync(token, r.ChatId, photo.Path, photo.Caption, messageId).ConfigureAwait(false);
                        var photoId = result?.Value<long?>("message_id");
                        if (photoId.HasValue) sent?.Invoke(r.ChatId, photoId.Value);
                    }
                }
                catch (Exception ex)
                {
                    error = (string.IsNullOrEmpty(r.Name) ? r.ChatId.ToString(CultureInfo.InvariantCulture) : r.Name) + ": " + Describe(ex);
                }
            }
            if (error == null) Patch(scope, new Row { ["LastSentAt"] = DateTime.Now, ["LastError"] = null, ["LastErrorAt"] = null });
            else Patch(scope, new Row { ["LastError"] = Clip(error, 500), ["LastErrorAt"] = DateTime.Now });
            return error;
        }

        /// <summary>
        /// A public https link becomes a button under the message; other links (e.g. localhost) are written in the text.
        /// Returns the id of the sent message.
        /// </summary>
        private static async Task<long?> SendAsync(string token, long chatId, string text, string link)
        {
            Uri uri;
            var button = link != null && Uri.TryCreate(link, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps && !uri.IsLoopback;
            if (button)
            {
                try
                {
                    var result = await BaleApi.CallAsync(token, "sendMessage", new
                    {
                        chat_id = chatId,
                        text,
                        reply_markup = new { inline_keyboard = new[] { new[] { new { text = "مشاهده در پنل مدیریت", url = link } } } }
                    }).ConfigureAwait(false);
                    return result?.Value<long?>("message_id");
                }
                catch (BaleException ex) when (ex.Code == 400)
                {
                }
            }
            var body = link == null ? text : text + "\n\n🔗 " + link;
            var plain = await BaleApi.CallAsync(token, "sendMessage", new { chat_id = chatId, text = body }).ConfigureAwait(false);
            return plain?.Value<long?>("message_id");
        }

        internal static Task<JToken> ReplyAsync(string token, long chatId, string text) =>
            BaleApi.CallAsync(token, "sendMessage", new { chat_id = chatId, text });

        /// <summary>The sites whose notifications this Bale chat receives (the main site is <see cref="SiteScope"/>).</summary>
        internal static List<string> ScopesOf(long chatId) =>
            NikooDb.Column<string>("SELECT DISTINCT [CompanyCode] FROM " + NikooDb.Table(NikooTables.BaleRecipients.Name)
                + " WHERE [ChatId] = @ChatId", new Row { ["ChatId"] = chatId });

        internal static bool IsRecipient(string scope, long chatId) =>
            NikooDb.Exists(NikooTables.BaleRecipients, new Row { ["CompanyCode"] = scope, ["ChatId"] = chatId });

        // ---- Helpers

        public static string SiteName(string scope)
        {
            if (scope == SiteScope) return Plain(SiteContentStore.Settings.SiteName);
            return Plain(TenantStore.GetSettings(scope)?.StoreName ?? scope);
        }

        /// <summary>User text made safe for Bale's always-on Markdown: no stars, link brackets or code fences.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return text.Trim().Replace('*', '٭').Replace('[', '(').Replace(']', ')').Replace('`', '\'');
        }

        public static string Clip(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            return text.Length > max ? text.Substring(0, max) + "…" : text;
        }

        private static string AbsoluteUrl(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var request = HttpContext.Current?.Request;
            var root = request != null ? request.Url.GetLeftPart(UriPartial.Authority) : ConfigurationManager.AppSettings["PaymentHub.BaseUrl"];
            return string.IsNullOrEmpty(root) ? null : root.TrimEnd('/') + path;
        }

        internal static string LatinDigits(string text)
        {
            var chars = text.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] >= '۰' && chars[i] <= '۹') chars[i] = (char)('0' + (chars[i] - '۰'));
                else if (chars[i] >= '٠' && chars[i] <= '٩') chars[i] = (char)('0' + (chars[i] - '٠'));
            }
            return new string(chars);
        }

        private static string Describe(Exception ex)
        {
            var bale = ex as BaleException;
            if (bale != null)
            {
                // Bale answers an unknown token with 403 "Token not found", the code Telegram uses for a blocked bot.
                if (bale.Code == 401 || bale.Code == 404 || bale.Message.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "توکن بازو نامعتبر است.";
                if (bale.Code == 403) return "کاربر بازو را مسدود کرده یا هنوز آن را شروع نکرده است.";
                return bale.Message;
            }
            if (ex is TaskCanceledException || ex is HttpRequestException) return "اتصال به سرور بله برقرار نشد.";
            return ex.Message;
        }

        /// <summary>Writes some columns of a site's settings row, creating the row first if needed (columns are named in code).</summary>
        private static void Patch(string scope, Row values)
        {
            EnsureRow(scope);
            var parameters = new Row(values) { ["Scope"] = scope };
            var sets = string.Join(", ", values.Keys.Select(k => "[" + k + "] = @" + k));
            NikooDb.Execute("UPDATE " + NikooDb.Table(NikooTables.NotifySettings.Name) + " SET " + sets + " WHERE [CompanyCode] = @Scope", parameters);
        }

        private static void EnsureRow(string scope)
        {
            if (NikooDb.Exists(NikooTables.NotifySettings, NikooDb.Key(scope))) return;
            try
            {
                NikooDb.Insert(NikooTables.NotifySettings, new NotifySettings { CompanyCode = scope });
            }
            catch (SqlException ex) when (NikooDb.IsDuplicateKey(ex))
            {
            }
        }
    }

    /// <summary>A private message sent to the bot.</summary>
    public sealed class BaleIncoming
    {
        public long ChatId { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        /// <summary>The text, or the caption of a photo or file.</summary>
        public string Text { get; set; }
        /// <summary>The bot's message this one answers (Bale's «پاسخ»).</summary>
        public long? ReplyToId { get; set; }
        /// <summary>The photo, or the image sent as a file; null when the message has none.</summary>
        public string ImageFileId { get; set; }
        public string ImageFileName { get; set; }
        /// <summary>Any attachment: a photo, file, voice, video, audio or sticker.</summary>
        public bool HasMedia { get; set; }
    }

    /// <summary>An image file sent after a Bale message, as a reply to it.</summary>
    public sealed class BalePhoto
    {
        public string Path { get; set; }
        public string Caption { get; set; }
    }
}
