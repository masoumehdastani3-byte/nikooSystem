using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Security;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>
    /// Bank accounts of a store. Each one posts to its own subsidiary account under «موجودی نقد و بانک» (11); the one marked as the site's
    /// gateway receives the online payments and its ZarinPal merchant code is the store's payment setting.
    /// </summary>
    public static partial class TenantStore
    {
        public static List<StoreBankAccount> GetBanks(string companyCode) => All(NikooTables.BankAccounts, companyCode, "[Title], [Id]");

        public static StoreBankAccount GetBank(string companyCode, int id) => id > 0 ? Find(NikooTables.BankAccounts, companyCode, id) : null;

        private static readonly Dictionary<string, string> BankSorts = new Dictionary<string, string>
        {
            { "title", "t.[Title]" },
            { "bank", "t.[BankName]" },
            { "gateway", "t.[Gateway]" },
            { "created", "t.[CreatedAt]" }
        };

        public static PagedList<StoreBankAccount> BankGrid(string companyCode, BankGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Title", "BankName", "Branch", "AccountNumber", "CardNumber", "Sheba", "Owner");
            if (query.State == "gateway") filter.Where("t.[Gateway] <> {0}", GatewayProvider.None.ToString());
            else if (query.State == "plain") filter.Eq("Gateway", GatewayProvider.None.ToString());
            return Grid(NikooTables.BankAccounts, companyCode, query, filter, BankSorts, "title", false);
        }

        /// <summary>Balance of each bank's ledger account. A reversed entry and its reversing entry both count, so they cancel out.</summary>
        public static Dictionary<int, long> BankBalances(string companyCode, IEnumerable<StoreBankAccount> banks)
        {
            var ledgerIds = banks.ToDictionary(b => b.Id, b => b.LedgerAccountId);
            var sums = new Dictionary<int, long>();
            foreach (var line in JournalLines(companyCode))
            {
                long sum;
                sums.TryGetValue(line.AccountId, out sum);
                sums[line.AccountId] = sum + line.Debit - line.Credit;
            }
            return ledgerIds.ToDictionary(p => p.Key, p => sums.ContainsKey(p.Value) ? sums[p.Value] : 0);
        }

        public static string SaveBank(string companyCode, StoreBankAccount bank, string newSecret)
        {
            bank.Title = (bank.Title ?? "").Trim();
            bank.BankName = Blank(bank.BankName);
            bank.Branch = Blank(bank.Branch);
            bank.Owner = Blank(bank.Owner);
            bank.Note = Blank(bank.Note);
            bank.AccountNumber = Blank(LatinDigits(bank.AccountNumber));
            bank.CardNumber = Blank(Regex.Replace(LatinDigits(bank.CardNumber) ?? "", @"[\s\-]", ""));
            bank.Sheba = Blank(Regex.Replace(LatinDigits(bank.Sheba) ?? "", @"[\s\-]", "").ToUpperInvariant());
            bank.GatewayMerchant = Blank(LatinDigits(bank.GatewayMerchant));
            bank.GatewayUsername = Blank(bank.GatewayUsername);
            if (bank.Title.Length == 0) return "یک نام کوتاه برای حساب بنویسید، مثلاً «ملت جاری».";
            if (bank.CardNumber != null && !Regex.IsMatch(bank.CardNumber, @"^\d{16}$")) return "شماره کارت ۱۶ رقم است.";
            if (bank.Sheba != null)
            {
                if (Regex.IsMatch(bank.Sheba, @"^\d{24}$")) bank.Sheba = "IR" + bank.Sheba;
                if (!Regex.IsMatch(bank.Sheba, @"^IR\d{24}$")) return "شماره شبا با IR و ۲۴ رقم بعد از آن است.";
            }

            var all = GetBanks(companyCode);
            if (all.Any(b => b.Id != bank.Id && string.Equals(b.Title, bank.Title, StringComparison.OrdinalIgnoreCase)))
                return "حساب بانکی دیگری با این نام هست.";
            var existing = bank.Id > 0 ? all.FirstOrDefault(b => b.Id == bank.Id) : null;
            if (bank.Id > 0 && existing == null) return "حساب بانکی پیدا نشد.";

            if (!bank.HasGateway)
            {
                bank.GatewayMerchant = null;
                bank.GatewayUsername = null;
                bank.GatewaySecret = null;
                bank.IsSiteGateway = false;
            }
            else
            {
                newSecret = (newSecret ?? "").Trim();
                bank.GatewaySecret = newSecret.Length > 0 ? Protect(newSecret)
                    : existing != null && existing.Gateway == bank.Gateway ? existing.GatewaySecret : null;
            }
            if (bank.IsSiteGateway)
            {
                Guid merchant;
                if (bank.Gateway != GatewayProvider.ZarinPal)
                    return "پرداخت آنلاین سایت فعلاً با زرین‌پال انجام می‌شود؛ درگاه سایت باید زرین‌پال باشد.";
                if (!Guid.TryParse(bank.GatewayMerchant ?? "", out merchant))
                    return "مرچنت کد زرین‌پال ۳۶ کاراکتر است؛ آن را دقیقاً از پنل زرین‌پال کپی کنید.";
                if (!bank.IsActive) return "حساب غیرفعال نمی‌تواند درگاه سایت باشد.";
            }

            EnsureAccounts(companyCode);
            var accounts = GetAccounts(companyCode);
            var ledger = existing == null ? null : accounts.FirstOrDefault(a => a.Id == existing.LedgerAccountId);
            if (ledger == null)
            {
                var parent = accounts.First(a => a.Code == AccountCodes.CashAndBank && a.Level == AccountLevel.General);
                var code = NextChildCode(parent.Code, 2, new HashSet<string>(accounts.Select(a => a.Code), StringComparer.OrdinalIgnoreCase));
                if (code == null) return "زیر «" + parent.Title + "» کد خالی برای حساب تازه نمانده است.";
                ledger = new StoreLedgerAccount
                {
                    Code = code,
                    Kind = parent.Kind,
                    Level = AccountLevel.Subsidiary,
                    ParentId = parent.Id,
                    IsSystem = true
                };
            }
            ledger.Title = "بانک " + bank.Title;
            ledger.IsActive = bank.IsActive;
            Save(NikooTables.LedgerAccounts, companyCode, ledger);
            bank.LedgerAccountId = ledger.Id;
            if (existing != null) bank.CreatedAt = existing.CreatedAt;
            Save(NikooTables.BankAccounts, companyCode, bank);

            if (bank.IsSiteGateway)
            {
                foreach (var other in all.Where(b => b.Id != bank.Id && b.IsSiteGateway))
                {
                    other.IsSiteGateway = false;
                    Save(NikooTables.BankAccounts, companyCode, other);
                }
                var settings = GetSettings(companyCode);
                if (settings != null && settings.ZarinPalMerchantId != bank.GatewayMerchant)
                {
                    settings.ZarinPalMerchantId = bank.GatewayMerchant;
                    settings.ZarinPalSandbox = false;
                    SaveSettings(settings);
                }
            }
            return null;
        }

        public static string DeleteBank(string companyCode, int id)
        {
            var bank = GetBank(companyCode, id);
            if (bank == null) return null;
            var used = JournalLines(companyCode).Any(l => l.AccountId == bank.LedgerAccountId)
                || NikooDb.Count(NikooTables.PurchaseInvoices, SqlFilter.ForStore(companyCode).Eq("BankId", id)) > 0
                || NikooDb.Count(NikooTables.SalesInvoices, SqlFilter.ForStore(companyCode).Eq("BankId", id)) > 0
                || NikooDb.Count(NikooTables.CashTransactions, SqlFilter.ForStore(companyCode).Eq("BankId", id)) > 0;
            if (used) return "حساب «" + bank.Title + "» گردش دارد و حذف نمی‌شود؛ غیرفعالش کنید.";
            Remove(NikooTables.BankAccounts, companyCode, id);
            if (bank.LedgerAccountId > 0) Remove(NikooTables.LedgerAccounts, companyCode, bank.LedgerAccountId);
            return null;
        }

        /// <summary>
        /// Keeps the gateway bank in step when the merchant code is changed from the settings or the setup wizard.
        /// Clearing the merchant code leaves the bank as it is, only no longer the site's gateway.
        /// </summary>
        public static void SyncGatewayBank(string companyCode, string merchantId)
        {
            var bank = GetBanks(companyCode).FirstOrDefault(b => b.IsSiteGateway);
            if (bank == null) return;
            merchantId = Blank(merchantId);
            if (merchantId == null) bank.IsSiteGateway = false;
            else bank.GatewayMerchant = merchantId;
            Save(NikooTables.BankAccounts, companyCode, bank);
        }

        /// <summary>Where the site's online payments settle: the gateway bank's ledger account, or the general «بانک».</summary>
        private static int OnlineBankAccountId(string companyCode)
        {
            var bank = GetBanks(companyCode).FirstOrDefault(b => b.IsSiteGateway && b.IsActive && b.LedgerAccountId > 0);
            return bank != null ? bank.LedgerAccountId : IdOf(companyCode, AccountCodes.Bank);
        }

        /// <summary>
        /// Checks the bank of a document paid by bank. With no bank chosen and exactly one active bank account, that one is used;
        /// with no bank accounts at all, the general «بانک» account is used.
        /// </summary>
        private static string ResolveBank(string companyCode, PayWith pay, ref int bankId)
        {
            if (pay != PayWith.Bank)
            {
                bankId = 0;
                return null;
            }
            var banks = GetBanks(companyCode);
            if (bankId > 0)
            {
                var id = bankId;
                var bank = banks.FirstOrDefault(b => b.Id == id);
                if (bank == null) return "حساب بانکی انتخاب‌شده پیدا نشد.";
                return null;
            }
            var active = banks.Where(b => b.IsActive).ToList();
            if (active.Count == 1) bankId = active[0].Id;
            else if (active.Count > 1) return "حساب بانکی را انتخاب کنید.";
            return null;
        }

        /// <summary>The ledger account of the cash box, a bank account, or receivable/payable for credit.</summary>
        private static int MoneyAccountId(string companyCode, PayWith pay, int bankId, bool incoming)
        {
            if (pay == PayWith.Bank && bankId > 0)
            {
                var bank = GetBank(companyCode, bankId);
                if (bank != null && bank.LedgerAccountId > 0) return bank.LedgerAccountId;
            }
            return IdOf(companyCode, PayAccount(pay, incoming));
        }

        private static string Protect(string secret) =>
            Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(secret), "NikooSystem.GatewaySecret"));

        private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string LatinDigits(string value)
        {
            if (value == null) return null;
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] >= '۰' && chars[i] <= '۹') chars[i] = (char)('0' + (chars[i] - '۰'));
                else if (chars[i] >= '٠' && chars[i] <= '٩') chars[i] = (char)('0' + (chars[i] - '٠'));
            }
            return new string(chars);
        }
    }
}
