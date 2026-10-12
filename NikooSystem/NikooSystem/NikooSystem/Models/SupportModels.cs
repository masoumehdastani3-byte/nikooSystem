using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NikooSystem.Models.Store;

namespace NikooSystem.Models
{
    public enum TicketStatus
    {
        /// <summary>Waiting for Nikoo's support: new, or the customer wrote last.</summary>
        Open,
        /// <summary>Support wrote last; waiting for the customer.</summary>
        Answered,
        Closed
    }

    public enum TicketPriority
    {
        Low,
        Normal,
        High
    }

    public enum TicketCategory
    {
        Technical,
        Billing,
        Payment,
        Domain,
        Design,
        Training,
        Other
    }

    /// <summary>Where a ticket message was written.</summary>
    public enum TicketVia
    {
        Panel,
        Bale
    }

    /// <summary>A support request of a store owner to Nikoo's support; its messages are <see cref="SupportMessage"/> rows.</summary>
    public class SupportTicket
    {
        public int Id { get; set; }
        public string CompanyCode { get; set; }
        public string Subject { get; set; }
        public TicketCategory Category { get; set; } = TicketCategory.Technical;
        public TicketPriority Priority { get; set; } = TicketPriority.Normal;
        public TicketStatus Status { get; set; } = TicketStatus.Open;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        /// <summary>Time of the last message.</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? ClosedAt { get; set; }
        public int MessageCount { get; set; }
        public bool UnreadBySupport { get; set; }
        public bool UnreadByCustomer { get; set; }
    }

    public class SupportMessage
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        /// <summary>The ticket's store, so the rows go with the store when it is deleted.</summary>
        public string CompanyCode { get; set; }
        public bool FromSupport { get; set; }
        public string AuthorName { get; set; }
        /// <summary>May be empty when the message is only images.</summary>
        public string Body { get; set; }
        /// <summary>File names of the attached images, comma separated; the files are private (see <c>SupportDesk.ImagePath</c>).</summary>
        public string Images { get; set; }
        public TicketVia Via { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string[] ImageList => string.IsNullOrEmpty(Images) ? new string[0] : Images.Split(',');
    }

    /// <summary>A ticket message the bot sent to a Bale chat; a reply to it in Bale is added to the ticket.</summary>
    public class SupportBaleLink
    {
        public long ChatId { get; set; }
        public long MessageId { get; set; }
        public int TicketId { get; set; }
        public string CompanyCode { get; set; }
        /// <summary>Sent to Nikoo's support (a reply is support's answer); otherwise sent to the store owner.</summary>
        public bool ToSupport { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
    }

    public class NewTicketForm
    {
        [Required(ErrorMessage = "موضوع را بنویسید.")]
        [StringLength(120, ErrorMessage = "موضوع حداکثر ۱۲۰ حرف است.")]
        public string Subject { get; set; }

        public TicketCategory Category { get; set; } = TicketCategory.Technical;
        public TicketPriority Priority { get; set; } = TicketPriority.Normal;

        [Required(ErrorMessage = "متن پیام را بنویسید.")]
        [StringLength(4000, ErrorMessage = "متن پیام حداکثر ۴۰۰۰ حرف است.")]
        public string Body { get; set; }
    }

    public class TicketGridQuery : GridQuery
    {
        /// <summary>open | answered | closed | active (open + answered); anything else shows every ticket</summary>
        public string State { get; set; }
        public string Company { get; set; }
    }

    public class TicketPageVm
    {
        public SupportTicket Ticket { get; set; }
        public List<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
        public TenantInfo Store { get; set; }
        public string StoreName { get; set; }
        /// <summary>Bale accounts of the other side, which get the replies written on this page.</summary>
        public int BaleRecipients { get; set; }
        public bool BotReady { get; set; }
        public bool IsDemo { get; set; }
    }

    /// <summary>Persian labels and badge colours of the ticket enums.</summary>
    public static class SupportLabels
    {
        public static readonly IReadOnlyDictionary<TicketCategory, string> Categories = new Dictionary<TicketCategory, string>
        {
            [TicketCategory.Technical] = "فنی و خطای سایت",
            [TicketCategory.Billing] = "مالی و اشتراک",
            [TicketCategory.Payment] = "درگاه پرداخت",
            [TicketCategory.Domain] = "دامنه و آدرس سایت",
            [TicketCategory.Design] = "قالب و طراحی",
            [TicketCategory.Training] = "آموزش و راهنمایی",
            [TicketCategory.Other] = "سایر"
        };

        public static readonly IReadOnlyDictionary<TicketPriority, string> Priorities = new Dictionary<TicketPriority, string>
        {
            [TicketPriority.Low] = "کم",
            [TicketPriority.Normal] = "عادی",
            [TicketPriority.High] = "فوری"
        };

        public static string Category(TicketCategory c) => Categories[c];

        public static string Priority(TicketPriority p) => Priorities[p];

        /// <param name="forSupport">The wording of Nikoo's panel; otherwise the store owner's.</param>
        public static string Status(TicketStatus s, bool forSupport)
        {
            switch (s)
            {
                case TicketStatus.Open: return forSupport ? "در انتظار پاسخ" : "در انتظار پاسخ پشتیبانی";
                case TicketStatus.Answered: return "پاسخ داده شد";
                default: return "بسته شده";
            }
        }

        public static string StatusTone(TicketStatus s) =>
            s == TicketStatus.Open ? "is-open" : s == TicketStatus.Answered ? "is-answered" : "is-closed";

        public static string PriorityTone(TicketPriority p) =>
            p == TicketPriority.High ? "is-high" : p == TicketPriority.Low ? "is-low" : "is-normal";
    }
}
