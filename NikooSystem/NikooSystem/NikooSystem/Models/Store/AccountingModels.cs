using System;
using System.Collections.Generic;
using System.Linq;

namespace NikooSystem.Models.Store
{
    /// <summary>
    /// One heading in a store's chart of accounts: a group (one digit), a general account under a group (two digits)
    /// or a subsidiary account under a general one (four digits). Documents post to subsidiary accounts only.
    /// </summary>
    public class StoreLedgerAccount : IStoreEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        /// <summary>Chosen on the group; general and subsidiary accounts take the kind of their group.</summary>
        public AccountKind Kind { get; set; } = AccountKind.Asset;
        /// <summary>Rows saved before the levels existed read as Subsidiary (the zero value).</summary>
        public AccountLevel Level { get; set; } = AccountLevel.Subsidiary;
        public int ParentId { get; set; }
        /// <summary>Seeded headings that documents post to; the code, level, parent and kind stay fixed.</summary>
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>Whether lines of this subsidiary account name a level-1 detail account (a person).</summary>
        public DetailUse Detail1 { get; set; } = DetailUse.None;
        /// <summary>Whether lines of this subsidiary account name a level-2 detail account (a cost center or project).</summary>
        public DetailUse Detail2 { get; set; } = DetailUse.None;

        public bool DebitNormal => Kind == AccountKind.Asset || Kind == AccountKind.Expense;
        public bool IsPosting => Level == AccountLevel.Subsidiary;
    }

    public enum AccountLevel
    {
        Subsidiary = 0,
        General = 1,
        Group = 2
    }

    public enum DetailUse
    {
        None,
        Optional,
        Required
    }

    public enum DetailKind
    {
        Person,
        CostCenter,
        Project,
        Other
    }

    /// <summary>
    /// A floating detail account (حساب تفصیلی) used beside subsidiary accounts in journal lines. Level 1 is for people
    /// (each supplier or customer gets one), level 2 for cost centers, projects and the like.
    /// </summary>
    public class StoreDetailAccount : IStoreEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        /// <summary>1 or 2.</summary>
        public int Level { get; set; } = 1;
        public DetailKind Kind { get; set; } = DetailKind.Person;
        /// <summary>The supplier or customer this detail account was made for; its title follows the party's name.</summary>
        public int PartyId { get; set; }
        public string Note { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class DetailGridQuery : GridQuery
    {
        public int? Level { get; set; }
        public DetailKind? Kind { get; set; }
    }

    public enum AccountKind
    {
        Asset,
        Liability,
        Equity,
        Income,
        Expense
    }

    /// <summary>How a purchase is paid or a sale is collected.</summary>
    public enum PayWith
    {
        Cash,
        Bank,
        Credit
    }

    public enum PurchaseLineKind
    {
        Goods,
        Expense,
        Asset
    }

    public enum JournalSource
    {
        Manual,
        Purchase,
        Sale,
        Online,
        Tuition,
        Facility,
        Adjust,
        Receipt,
        Payment
    }

    public class StoreJournalEntry : IStoreEntity
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public DateTime EntryDate { get; set; } = DateTime.Today;
        public string Description { get; set; }
        public JournalSource Source { get; set; } = JournalSource.Manual;
        public int SourceId { get; set; }
        public int DocumentTypeId { get; set; }
        public bool Reversed { get; set; }
        public List<StoreJournalLine> Lines { get; set; } = new List<StoreJournalLine>();
    }

    public class StoreJournalLine
    {
        /// <summary>A subsidiary account.</summary>
        public int AccountId { get; set; }
        public int Detail1Id { get; set; }
        public int Detail2Id { get; set; }
        public long Debit { get; set; }
        public long Credit { get; set; }
        public string Memo { get; set; }
    }

    public class StorePurchaseInvoice : IStoreEntity
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public int PartyId { get; set; }
        /// <summary>The supplier's name when the invoice was saved.</summary>
        public string PartyName { get; set; }
        public PayWith PayWith { get; set; } = PayWith.Cash;
        /// <summary>The bank account when <see cref="PayWith"/> is Bank; 0 posts to the general «بانک» account.</summary>
        public int BankId { get; set; }
        public string Note { get; set; }
        public long Total { get; set; }
        public int EntryId { get; set; }
        public List<StorePurchaseLine> Lines { get; set; } = new List<StorePurchaseLine>();
    }

    public class StorePurchaseLine
    {
        public int ProductId { get; set; }
        /// <summary>Warehouse item when the line is not a shop product.</summary>
        public int GoodsId { get; set; }
        public string Title { get; set; }
        public int Qty { get; set; }
        public long UnitCost { get; set; }
        public PurchaseLineKind Kind { get; set; } = PurchaseLineKind.Goods;
        public long Amount => (long)Qty * UnitCost;
    }

    public class StoreSalesInvoice : IStoreEntity
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public int PartyId { get; set; }
        /// <summary>The customer's name when the invoice was saved.</summary>
        public string PartyName { get; set; }
        public PayWith PayWith { get; set; } = PayWith.Cash;
        public int BankId { get; set; }
        public string Note { get; set; }
        public long Total { get; set; }
        public int EntryId { get; set; }
        public List<StoreSaleLine> Lines { get; set; } = new List<StoreSaleLine>();
    }

    public class StoreSaleLine
    {
        public int ProductId { get; set; }
        public int GoodsId { get; set; }
        public string Title { get; set; }
        public int Qty { get; set; }
        public long UnitPrice { get; set; }
        public long Amount => (long)Qty * UnitPrice;
    }

    /// <summary>Quantity in or out of a product. Product.Stock stays the quantity on hand.</summary>
    public class StoreStockMove : IStoreEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int GoodsId { get; set; }
        public DateTime MoveDate { get; set; } = DateTime.Today;
        /// <summary>Positive receives goods; negative issues them.</summary>
        public int Qty { get; set; }
        public long UnitCost { get; set; }
        public string Kind { get; set; }
        public int RefId { get; set; }
        public string Note { get; set; }
    }

    /// <summary>A quantity the store keeps that is not a product on the shop (balls, kits, supplies).</summary>
    public class StoreGoodsItem : IStoreEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public string Unit { get; set; } = "عدد";
        public int Qty { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class StoreFixedAsset : IStoreEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public DateTime BoughtOn { get; set; } = DateTime.Today;
        public long Cost { get; set; }
        public string Location { get; set; }
        public string Note { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>Subsidiary accounts that documents post to by themselves, and the general account bank accounts sit under.</summary>
    public static class AccountCodes
    {
        public const string CashAndBank = "11";
        public const string Cash = "1101";
        public const string Bank = "1102";
        public const string Receivable = "1201";
        public const string Inventory = "1301";
        public const string FixedAssets = "2101";
        public const string Payable = "3101";
        public const string Capital = "5101";
        public const string Sales = "6101";
        public const string OnlineSales = "6102";
        public const string Tuition = "6103";
        public const string Cogs = "7101";
        public const string StockAdjust = "7102";
        public const string Expense = "8101";

        /// <summary>Codes of the first, single-level chart and the subsidiary account each became.</summary>
        public static readonly Dictionary<string, string> Legacy = new Dictionary<string, string>
        {
            { "1010", Cash }, { "1020", Bank }, { "1030", Receivable }, { "1040", Inventory }, { "1050", FixedAssets },
            { "2010", Payable }, { "3010", Capital }, { "4010", Sales }, { "4020", OnlineSales }, { "4030", Tuition },
            { "5010", Cogs }, { "5020", Expense }, { "5030", StockAdjust }
        };
    }

    public static class DocumentTypeCodes
    {
        public const string Manual = "SN";
        public const string Purchase = "KH";
        public const string Sale = "FR";
        public const string Online = "ON";
        public const string Tuition = "SH";
        public const string Facility = "RZ";
        public const string Adjust = "TA";
        public const string Receipt = "DR";
        public const string Payment = "PD";
    }

    public enum CashDirection
    {
        In,
        Out
    }

    /// <summary>Money in or out of the cash box or the bank, with its own journal entry.</summary>
    public class StoreCashTransaction : IStoreEntity
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public DateTime TxDate { get; set; } = DateTime.Today;
        public CashDirection Direction { get; set; } = CashDirection.In;
        public long Amount { get; set; }
        public PayWith PayWith { get; set; } = PayWith.Cash;
        public int BankId { get; set; }
        /// <summary>Optional; when set, <see cref="PartyName"/> is copied from the party.</summary>
        public int PartyId { get; set; }
        public string PartyName { get; set; }
        public int DocumentTypeId { get; set; }
        public int AccountId { get; set; }
        /// <summary>Optional cost center or project of the opposite account.</summary>
        public int Detail2Id { get; set; }
        public string Note { get; set; }
        public int EntryId { get; set; }
    }

    public enum GatewayProvider
    {
        None,
        ZarinPal,
        Mellat,
        Saman,
        Parsian,
        Melli,
        Pasargad,
        IranKish,
        IdPay,
        PayPing,
        Other
    }

    /// <summary>A bank account of the store, with its own ledger account under «بانک» and an optional payment gateway.</summary>
    public class StoreBankAccount : IStoreEntity
    {
        public int Id { get; set; }
        /// <summary>A short name shown in invoices, such as «ملت جاری».</summary>
        public string Title { get; set; }
        public string BankName { get; set; }
        public string Branch { get; set; }
        public string AccountNumber { get; set; }
        public string CardNumber { get; set; }
        public string Sheba { get; set; }
        public string Owner { get; set; }
        public int LedgerAccountId { get; set; }
        public bool IsActive { get; set; } = true;
        public string Note { get; set; }

        public GatewayProvider Gateway { get; set; } = GatewayProvider.None;
        /// <summary>Merchant code of ZarinPal, or the terminal number of a bank gateway.</summary>
        public string GatewayMerchant { get; set; }
        public string GatewayUsername { get; set; }
        /// <summary>Password or API key of the gateway; never shown back in the form.</summary>
        public string GatewaySecret { get; set; }
        /// <summary>The site's online payments go through this gateway and settle in this account.</summary>
        public bool IsSiteGateway { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool HasGateway => Gateway != GatewayProvider.None;
    }

    public class BankGridQuery : GridQuery
    {
        /// <summary>gateway | plain</summary>
        public string State { get; set; }
    }

    public enum PartyKind
    {
        Supplier,
        Customer,
        Both
    }

    /// <summary>A supplier or customer that purchase and sales invoices are issued to.</summary>
    public class StoreParty : IStoreEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public PartyKind Kind { get; set; } = PartyKind.Supplier;
        public string Mobile { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Note { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool Supplies => Kind != PartyKind.Customer;
        public bool Buys => Kind != PartyKind.Supplier;
    }

    public class PartyGridQuery : GridQuery
    {
        public PartyKind? Kind { get; set; }
    }

    public class AccountGridQuery : GridQuery
    {
        public AccountKind? Kind { get; set; }
        public AccountLevel? Level { get; set; }
    }

    public class JournalGridQuery : GridQuery
    {
        public int? Type { get; set; }
    }

    public class InvoiceGridQuery : GridQuery
    {
        public PayWith? Pay { get; set; }
    }

    public class CashGridQuery : GridQuery
    {
        public CashDirection? Flow { get; set; }
    }

    public class InventoryGridQuery : GridQuery
    {
        /// <summary>goods | products | moves</summary>
        public string Tab { get; set; }
    }

    public class BooksGridQuery : GridQuery
    {
        /// <summary>Trial balance level: group | general | subsidiary | detail.</summary>
        public string Level { get; set; }
        /// <summary>Shows only the accounts under this group or general account.</summary>
        public int? Parent { get; set; }
        public int? AccountId { get; set; }
        public int? DetailId { get; set; }
        /// <summary>yyyy-MM-dd</summary>
        public string From { get; set; }
        public string To { get; set; }
    }

    /// <summary>A label the owner can add, such as «هزینه اجاره» or «دریافت از مشتری».</summary>
    public class StoreDocumentType : IStoreEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public static class AccountingText
    {
        public static string Kind(AccountKind kind)
        {
            switch (kind)
            {
                case AccountKind.Asset: return "دارایی";
                case AccountKind.Liability: return "بدهی";
                case AccountKind.Equity: return "سرمایه";
                case AccountKind.Income: return "درآمد";
                default: return "هزینه";
            }
        }

        public static string Pay(PayWith pay)
        {
            switch (pay)
            {
                case PayWith.Bank: return "بانک";
                case PayWith.Credit: return "نسیه";
                default: return "صندوق";
            }
        }

        public static string Line(PurchaseLineKind kind)
        {
            switch (kind)
            {
                case PurchaseLineKind.Expense: return "هزینه";
                case PurchaseLineKind.Asset: return "اموال";
                default: return "کالا";
            }
        }

        public static string Source(JournalSource source)
        {
            switch (source)
            {
                case JournalSource.Purchase: return "فاکتور خرید";
                case JournalSource.Sale: return "فاکتور فروش";
                case JournalSource.Online: return "فروش آنلاین";
                case JournalSource.Tuition: return "شهریه و ثبت‌نام";
                case JournalSource.Facility: return "رزرو سالن";
                case JournalSource.Adjust: return "تعدیل انبار";
                case JournalSource.Receipt: return "دریافت";
                case JournalSource.Payment: return "پرداخت";
                default: return "سند دستی";
            }
        }

        public static string Direction(CashDirection direction)
        {
            return direction == CashDirection.Out ? "پرداخت" : "دریافت";
        }

        public static string Gateway(GatewayProvider gateway)
        {
            switch (gateway)
            {
                case GatewayProvider.ZarinPal: return "زرین‌پال";
                case GatewayProvider.Mellat: return "به‌پرداخت ملت";
                case GatewayProvider.Saman: return "سامان کیش (سپ)";
                case GatewayProvider.Parsian: return "تاپ پرداخت پارسیان";
                case GatewayProvider.Melli: return "سداد بانک ملی";
                case GatewayProvider.Pasargad: return "پاسارگاد";
                case GatewayProvider.IranKish: return "ایران کیش";
                case GatewayProvider.IdPay: return "آیدی‌پی";
                case GatewayProvider.PayPing: return "پی‌پینگ";
                case GatewayProvider.Other: return "درگاه دیگر";
                default: return "بدون درگاه";
            }
        }

        /// <summary>The value of a «paid from» dropdown: Cash, Credit, Bank (no account chosen) or bank-12.</summary>
        public static string PayToken(PayWith pay, int bankId)
        {
            if (pay == PayWith.Bank) return bankId > 0 ? "bank-" + bankId.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Bank";
            return pay.ToString();
        }

        /// <summary>Options of a «paid from» dropdown: the cash box, each bank account, and credit when allowed.</summary>
        public static List<KeyValuePair<string, string>> PayOptions(IEnumerable<StoreBankAccount> banks, bool credit, string current)
        {
            var list = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Cash", "صندوق") };
            var bankList = banks.ToList();
            foreach (var bank in bankList)
                list.Add(new KeyValuePair<string, string>("bank-" + bank.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "بانک " + bank.Title + (bank.IsActive ? "" : " (غیرفعال)")));
            if (bankList.Count == 0 || current == "Bank")
                list.Add(new KeyValuePair<string, string>("Bank", bankList.Count == 0 ? "بانک" : "بانک (حساب مشخص نشده)"));
            if (credit) list.Add(new KeyValuePair<string, string>("Credit", "نسیه"));
            return list;
        }

        public static string Party(PartyKind kind)
        {
            switch (kind)
            {
                case PartyKind.Customer: return "مشتری";
                case PartyKind.Both: return "تامین‌کننده و مشتری";
                default: return "تامین‌کننده";
            }
        }

        public static string Level(AccountLevel level)
        {
            switch (level)
            {
                case AccountLevel.Group: return "گروه";
                case AccountLevel.General: return "کل";
                default: return "معین";
            }
        }

        public static string Use(DetailUse use)
        {
            switch (use)
            {
                case DetailUse.Optional: return "اختیاری";
                case DetailUse.Required: return "اجباری";
                default: return "ندارد";
            }
        }

        public static string Detail(DetailKind kind)
        {
            switch (kind)
            {
                case DetailKind.CostCenter: return "مرکز هزینه";
                case DetailKind.Project: return "پروژه";
                case DetailKind.Other: return "سایر";
                default: return "شخص";
            }
        }

        public static string DetailLevel(int level) => level == 2 ? "تفصیلی سطح ۲" : "تفصیلی سطح ۱";

        /// <summary>«بد» or «بس» after a balance; a dash for zero.</summary>
        public static string Side(long balance) => balance == 0 ? "—" : balance > 0 ? "بد" : "بس";
    }

    public class BooksPage
    {
        /// <summary>group | general | subsidiary | detail, for the trial balance.</summary>
        public string Level { get; set; }
        /// <summary>The group or general account the trial balance is narrowed to.</summary>
        public StoreLedgerAccount Parent { get; set; }
        /// <summary>The trial balance; null while a ledger is shown.</summary>
        public PagedList<AccountBalance> Totals { get; set; }
        /// <summary>The account of a general or subsidiary ledger.</summary>
        public StoreLedgerAccount Account { get; set; }
        /// <summary>The detail account of a detail ledger, or the detail a subsidiary ledger is narrowed to.</summary>
        public StoreDetailAccount Detail { get; set; }
        public PagedList<LedgerMovement> Movements { get; set; }
        /// <summary>Lines before <see cref="From"/>, as debit minus credit.</summary>
        public long Opening { get; set; }
        public long Debit { get; set; }
        public long Credit { get; set; }
        /// <summary>Trial balance: the sum of the debit balances and of the credit balances of all rows.</summary>
        public long BalanceDebit { get; set; }
        public long BalanceCredit { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        /// <summary>A group or general ledger shows one row per document; a subsidiary or detail ledger one row per line.</summary>
        public bool PerEntry { get; set; }
        /// <summary>Detail accounts that appear in the subsidiary ledger, for its filter.</summary>
        public List<StoreDetailAccount> DetailChoices { get; set; } = new List<StoreDetailAccount>();
    }

    /// <summary>One row of the trial balance: an account at the chosen level, or a detail account.</summary>
    public class AccountBalance
    {
        public StoreLedgerAccount Account { get; set; }
        public StoreDetailAccount Detail { get; set; }
        /// <summary>Debit minus credit of the entries before the start of the period.</summary>
        public long Opening { get; set; }
        public long Debit { get; set; }
        public long Credit { get; set; }

        public string Code => Account != null ? Account.Code : Detail?.Code;
        public string Title => Account != null ? Account.Title : Detail?.Title;
        public long Balance => Opening + Debit - Credit;
    }

    public class LedgerMovement
    {
        public int EntryId { get; set; }
        public DateTime Date { get; set; }
        public string Number { get; set; }
        public string Description { get; set; }
        public string Memo { get; set; }
        /// <summary>Code and title of the subsidiary account, in detail and general ledgers.</summary>
        public string Account { get; set; }
        public string Detail1 { get; set; }
        public string Detail2 { get; set; }
        public long Debit { get; set; }
        public long Credit { get; set; }
        /// <summary>Running balance, debit minus credit, after this row.</summary>
        public long Balance { get; set; }
    }

    /// <summary>A journal entry with the names of its accounts, for the voucher page.</summary>
    public class VoucherPage
    {
        public StoreJournalEntry Entry { get; set; }
        public string TypeTitle { get; set; }
        public List<VoucherLine> Lines { get; set; } = new List<VoucherLine>();
        public long Debit { get; set; }
        public long Credit { get; set; }
    }

    public class VoucherLine
    {
        public StoreJournalLine Line { get; set; }
        public StoreLedgerAccount Account { get; set; }
        public StoreLedgerAccount General { get; set; }
        public StoreLedgerAccount Group { get; set; }
        public StoreDetailAccount Detail1 { get; set; }
        public StoreDetailAccount Detail2 { get; set; }
    }
}
