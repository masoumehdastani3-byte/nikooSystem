using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models.Store;

namespace NikooSystem.Models
{
    /// <summary>What an admin can be told about in Bale.</summary>
    public enum NotifyEvent
    {
        /// <summary>A paid or confirmed order (store orders; product and subscription purchases on the main site).</summary>
        Order,
        /// <summary>A new or cancelled appointment of a booking site.</summary>
        Appointment,
        /// <summary>A contact-form message.</summary>
        Message,
        /// <summary>A product review or a reader's comment.</summary>
        Review,
        /// <summary>Main site: a payment of an app connected to the payment hub.</summary>
        Payment,
        /// <summary>Main site: a new trial store.</summary>
        Signup,
        /// <summary>Support tickets: new tickets and customers' replies on the main site, support's answers in a store.</summary>
        Ticket,
        /// <summary>A store: reminders to renew its subscription (always sent; not offered in the settings).</summary>
        Subscription,
        /// <summary>Connecting a custom domain: support's part on the main site, progress for the store (always sent; not offered in the settings).</summary>
        Domain
    }

    /// <summary>
    /// Bale notification settings of one store, or of the main site (CompanyCode = <c>BaleNotifier.SiteScope</c>). The bot
    /// fields are used only on the main site's row: every store is notified through Nikoo's own bot.
    /// </summary>
    public class NotifySettings
    {
        public string CompanyCode { get; set; }

        /// <summary>Events switched off, comma separated; empty means every event is sent.</summary>
        public string MutedEvents { get; set; }

        /// <summary>One-time code that links a Bale account to this site (sent to the bot as /start CODE).</summary>
        public string LinkCode { get; set; }
        public DateTime? LinkCodeExpiresAt { get; set; }

        public string BotToken { get; set; }
        public string BotUsername { get; set; }
        public string BotName { get; set; }
        /// <summary>The next getUpdates offset: updates before it were already read.</summary>
        public long UpdateOffset { get; set; }

        public DateTime? LastSentAt { get; set; }
        public string LastError { get; set; }
        public DateTime? LastErrorAt { get; set; }

        public bool IsMuted(NotifyEvent e) => MutedList().Contains(e);

        public IEnumerable<NotifyEvent> MutedList()
        {
            foreach (var part in (MutedEvents ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                NotifyEvent e;
                if (Enum.TryParse(part.Trim(), out e)) yield return e;
            }
        }

        public void SetMuted(IEnumerable<NotifyEvent> muted)
        {
            var list = muted.Distinct().OrderBy(e => e).ToList();
            MutedEvents = list.Count == 0 ? null : string.Join(",", list);
        }

        public bool HasLiveLinkCode(DateTime now) => !string.IsNullOrEmpty(LinkCode) && LinkCodeExpiresAt > now;
    }

    /// <summary>A Bale account that receives one site's notifications.</summary>
    public class BaleRecipient : IStoreEntity
    {
        public int Id { get; set; }
        public long ChatId { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public DateTime LinkedAt { get; set; } = DateTime.Now;
    }

    /// <summary>An event a site can switch on or off, with its label in the admin.</summary>
    public class NotifyEventOption
    {
        public NotifyEvent Event { get; set; }
        public string Title { get; set; }
        public string Hint { get; set; }
        public bool Enabled { get; set; }
    }

    public class NotifyPageVm
    {
        public bool IsSite { get; set; }
        public bool BotReady { get; set; }
        public string BotUsername { get; set; }
        public string BotName { get; set; }
        /// <summary>Linking is off on the public demo stores.</summary>
        public bool IsDemo { get; set; }
        public NotifySettings Settings { get; set; }
        public List<BaleRecipient> Recipients { get; set; } = new List<BaleRecipient>();
        public List<NotifyEventOption> Events { get; set; } = new List<NotifyEventOption>();
        /// <summary>A link code waiting to be sent to the bot, with its deep link.</summary>
        public string LinkCode { get; set; }
        public string LinkUrl { get; set; }
        public DateTime? LinkCodeExpiresAt { get; set; }
    }
}
