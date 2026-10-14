using System;
using System.Collections.Generic;

namespace NikooSystem.Models.Store
{
    /// <summary>One heading in a store's chart of accounts.</summary>
    public class StoreLedgerAccount : IStoreEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public AccountKind Kind { get; set; } = AccountKind.Asset;
        public int ParentId { get; set; }
        /// <summary>Seeded headings that documents post to; the code and kind stay fixed.</summary>
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; } = true;

        public bool DebitNormal => Kind == AccountKind.Asset || Kind == AccountKind.Expense;
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
        Adjust
    }

    public class StoreJournalEntry : IStoreEntity
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public DateTime EntryDate { get; set; } = DateTime.Today;
        public string Description { get; set; }
        public JournalSource Source { get; set; } = JournalSource.Manual;
        public int SourceId { get; set; }
        public bool Reversed { get; set; }
        public List<StoreJournalLine> Lines { get; set; } = new List<StoreJournalLine>();
    }

    public class StoreJournalLine
    {
        public int AccountId { get; set; }
        public long Debit { get; set; }
        public long Credit { get; set; }
        public string Memo { get; set; }
    }

    public class StorePurchaseInvoice : IStoreEntity
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public string PartyName { get; set; }
        public PayWith PayWith { get; set; } = PayWith.Cash;
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
        public string PartyName { get; set; }
        public PayWith PayWith { get; set; } = PayWith.Cash;
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

    public static class AccountCodes
    {
        public const string Cash = "1010";
        public const string Bank = "1020";
        public const string Receivable = "1030";
        public const string Inventory = "1040";
        public const string FixedAssets = "1050";
        public const string Payable = "2010";
        public const string Capital = "3010";
        public const string Sales = "4010";
        public const string OnlineSales = "4020";
        public const string Tuition = "4030";
        public const string Cogs = "5010";
        public const string Expense = "5020";
        public const string StockAdjust = "5030";
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
                default: return "سند دستی";
            }
        }
    }

    public class BooksPage
    {
        public List<AccountBalance> Totals { get; set; } = new List<AccountBalance>();
        public StoreLedgerAccount Account { get; set; }
        public List<LedgerMovement> Movements { get; set; } = new List<LedgerMovement>();
        public long Debit { get; set; }
        public long Credit { get; set; }
    }

    public class AccountBalance
    {
        public StoreLedgerAccount Account { get; set; }
        public long Debit { get; set; }
        public long Credit { get; set; }
    }

    public class LedgerMovement
    {
        public DateTime Date { get; set; }
        public string Number { get; set; }
        public string Description { get; set; }
        public string Memo { get; set; }
        public long Debit { get; set; }
        public long Credit { get; set; }
    }
}
