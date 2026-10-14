using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NikooSystem.Models;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>
    /// The chart of accounts in three levels (group, general, subsidiary) and the floating detail accounts in two levels.
    /// Group codes are one digit, general codes two (the group's digit and one more) and subsidiary codes four.
    /// </summary>
    public static partial class TenantStore
    {
        private sealed class ChartRow
        {
            public string Code;
            public string Title;
            public AccountLevel Level;
            public AccountKind Kind;
            public bool System;
            public DetailUse Detail1;
            public DetailUse Detail2;
        }

        private static ChartRow ChartGroup(string code, string title, AccountKind kind) =>
            new ChartRow { Code = code, Title = title, Level = AccountLevel.Group, Kind = kind, System = true };

        private static ChartRow ChartGeneral(string code, string title) =>
            new ChartRow { Code = code, Title = title, Level = AccountLevel.General, System = true };

        private static ChartRow ChartSub(string code, string title, bool system = false, DetailUse detail1 = DetailUse.None, DetailUse detail2 = DetailUse.None) =>
            new ChartRow { Code = code, Title = title, Level = AccountLevel.Subsidiary, System = system, Detail1 = detail1, Detail2 = detail2 };

        /// <summary>Every parent comes before its children.</summary>
        private static readonly ChartRow[] DefaultChart =
        {
            ChartGroup("1", "دارایی‌های جاری", AccountKind.Asset),
            ChartGeneral("11", "موجودی نقد و بانک"),
            ChartSub(AccountCodes.Cash, "صندوق", true),
            ChartSub(AccountCodes.Bank, "بانک", true),
            ChartGeneral("12", "حساب‌ها و اسناد دریافتنی"),
            ChartSub(AccountCodes.Receivable, "حساب‌های دریافتنی تجاری", true, DetailUse.Required),
            ChartSub("1202", "اسناد دریافتنی (چک‌های دریافتی)", false, DetailUse.Optional),
            ChartGeneral("13", "موجودی کالا"),
            ChartSub(AccountCodes.Inventory, "موجودی کالا", true),
            ChartGeneral("14", "پیش‌پرداخت‌ها و سایر دارایی‌های جاری"),
            ChartSub("1401", "پیش‌پرداخت‌ها", false, DetailUse.Optional),
            ChartSub("1402", "تنخواه‌گردان", false, DetailUse.Optional),

            ChartGroup("2", "دارایی‌های غیرجاری", AccountKind.Asset),
            ChartGeneral("21", "دارایی‌های ثابت"),
            ChartSub(AccountCodes.FixedAssets, "اموال و دارایی ثابت", true, DetailUse.None, DetailUse.Optional),
            ChartSub("2102", "استهلاک انباشته"),

            ChartGroup("3", "بدهی‌های جاری", AccountKind.Liability),
            ChartGeneral("31", "حساب‌ها و اسناد پرداختنی"),
            ChartSub(AccountCodes.Payable, "حساب‌های پرداختنی تجاری", true, DetailUse.Required),
            ChartSub("3102", "اسناد پرداختنی (چک‌های صادره)", false, DetailUse.Optional),
            ChartGeneral("32", "پیش‌دریافت‌ها و سایر بدهی‌های جاری"),
            ChartSub("3201", "پیش‌دریافت از مشتریان", false, DetailUse.Optional),
            ChartSub("3202", "مالیات و عوارض ارزش افزوده"),
            ChartSub("3203", "حقوق و بیمه پرداختنی", false, DetailUse.Optional),

            ChartGroup("4", "بدهی‌های غیرجاری", AccountKind.Liability),
            ChartGeneral("41", "تسهیلات و وام‌های بلندمدت"),
            ChartSub("4101", "وام بانکی بلندمدت", false, DetailUse.Optional),

            ChartGroup("5", "حقوق صاحبان سرمایه", AccountKind.Equity),
            ChartGeneral("51", "سرمایه و اندوخته‌ها"),
            ChartSub(AccountCodes.Capital, "سرمایه", true, DetailUse.Optional),
            ChartSub("5102", "برداشت شرکا", false, DetailUse.Optional),
            ChartSub("5103", "سود و زیان انباشته"),

            ChartGroup("6", "درآمدها", AccountKind.Income),
            ChartGeneral("61", "درآمدهای عملیاتی"),
            ChartSub(AccountCodes.Sales, "فروش", true, DetailUse.None, DetailUse.Optional),
            ChartSub(AccountCodes.OnlineSales, "فروش آنلاین", true),
            ChartSub(AccountCodes.Tuition, "درآمد شهریه و رزرو", true, DetailUse.None, DetailUse.Optional),
            ChartGeneral("62", "سایر درآمدها"),
            ChartSub("6201", "سایر درآمدها", false, DetailUse.Optional),

            ChartGroup("7", "بهای تمام‌شده", AccountKind.Expense),
            ChartGeneral("71", "بهای تمام‌شده فروش"),
            ChartSub(AccountCodes.Cogs, "بهای تمام‌شده کالای فروش‌رفته", true),
            ChartSub(AccountCodes.StockAdjust, "تعدیل موجودی", true),

            ChartGroup("8", "هزینه‌ها", AccountKind.Expense),
            ChartGeneral("81", "هزینه‌های اداری و عمومی"),
            ChartSub(AccountCodes.Expense, "هزینه‌های عمومی", true, DetailUse.None, DetailUse.Optional),
            ChartSub("8102", "حقوق و دستمزد", false, DetailUse.Optional, DetailUse.Optional),
            ChartSub("8103", "اجاره", false, DetailUse.None, DetailUse.Optional),
            ChartSub("8104", "آب، برق، گاز و تلفن", false, DetailUse.None, DetailUse.Optional),
            ChartSub("8105", "هزینه استهلاک"),
            ChartGeneral("82", "هزینه‌های فروش و بازاریابی"),
            ChartSub("8201", "تبلیغات و بازاریابی", false, DetailUse.None, DetailUse.Optional),
            ChartSub("8202", "هزینه ارسال کالا"),
            ChartGeneral("83", "سایر هزینه‌ها"),
            ChartSub("8301", "کارمزد بانکی"),
            ChartSub("8302", "سایر هزینه‌ها", false, DetailUse.None, DetailUse.Optional)
        };

        /// <summary>
        /// Builds the chart the first time it is needed. A store that already has the first, single-level chart keeps its
        /// accounts (and so its documents): the seeded ones move to their new codes, bank accounts go under «موجودی نقد و بانک»
        /// and the owner's own accounts under the general account of their kind.
        /// </summary>
        private static void EnsureAccounts(string companyCode)
        {
            if (All(NikooTables.LedgerAccounts, companyCode).Any(a => a.Level == AccountLevel.Group)) return;
            lock (LockFor(companyCode))
            {
                using (var tx = NikooDb.Begin())
                {
                    var existing = NikooDb.List(NikooTables.LedgerAccounts, companyCode, tx: tx);
                    if (existing.Any(a => a.Level == AccountLevel.Group)) return;
                    BuildChart(companyCode, existing, tx);
                    tx.Commit();
                }
                EnsurePartyDetails(companyCode);
            }
        }

        private static void BuildChart(string companyCode, List<StoreLedgerAccount> existing, DbTx tx)
        {
            var groupKinds = DefaultChart.Where(r => r.Level == AccountLevel.Group).ToDictionary(r => r.Code, r => r.Kind);
            var generals = new HashSet<string>(DefaultChart.Where(r => r.Level == AccountLevel.General).Select(r => r.Code));
            var bankLedgers = new HashSet<int>(NikooDb.List(NikooTables.BankAccounts, companyCode, tx: tx).Select(b => b.LedgerAccountId));
            var byCode = new Dictionary<string, StoreLedgerAccount>(StringComparer.OrdinalIgnoreCase);
            var pending = new List<StoreLedgerAccount>();

            foreach (var account in existing.OrderBy(a => a.Id))
            {
                string code;
                if (account.IsSystem && account.Code != null && AccountCodes.Legacy.TryGetValue(account.Code, out code) && !byCode.ContainsKey(code))
                {
                    account.Code = code;
                    byCode[code] = account;
                }
                else pending.Add(account);
            }
            foreach (var account in pending.ToList())
            {
                var code = account.Code ?? "";
                var fits = !bankLedgers.Contains(account.Id) && Regex.IsMatch(code, @"^\d{4}$") && generals.Contains(code.Substring(0, 2))
                    && groupKinds[code.Substring(0, 1)] == account.Kind && !byCode.ContainsKey(code);
                if (!fits) continue;
                byCode[code] = account;
                pending.Remove(account);
            }

            foreach (var row in DefaultChart)
            {
                StoreLedgerAccount account;
                if (byCode.TryGetValue(row.Code, out account))
                {
                    if (account.IsSystem)
                    {
                        account.Detail1 = row.Detail1;
                        account.Detail2 = row.Detail2;
                    }
                    continue;
                }
                byCode[row.Code] = new StoreLedgerAccount
                {
                    Id = NikooDb.NextId(NikooTables.LedgerAccounts, companyCode, tx),
                    Code = row.Code,
                    Title = row.Title,
                    IsSystem = row.System,
                    IsActive = true,
                    Detail1 = row.Detail1,
                    Detail2 = row.Detail2
                };
                // NextId reads the table, so each new row is written before the next id is taken
                NikooDb.Upsert(NikooTables.LedgerAccounts, byCode[row.Code], companyCode, tx);
            }

            var taken = new HashSet<string>(byCode.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var account in pending)
            {
                var general = bankLedgers.Contains(account.Id) ? AccountCodes.CashAndBank : GeneralFor(account.Kind);
                account.Code = NextChildCode(general, 2, taken);
                byCode[account.Code] = account;
            }

            foreach (var account in byCode.Values)
            {
                account.Level = account.Code.Length == 1 ? AccountLevel.Group : account.Code.Length == 2 ? AccountLevel.General : AccountLevel.Subsidiary;
                account.Kind = groupKinds[account.Code.Substring(0, 1)];
                account.ParentId = account.Level == AccountLevel.Group ? 0
                    : byCode[account.Code.Substring(0, account.Level == AccountLevel.General ? 1 : 2)].Id;
                if (account.Level != AccountLevel.Subsidiary)
                {
                    account.Detail1 = DetailUse.None;
                    account.Detail2 = DetailUse.None;
                }
                NikooDb.Upsert(NikooTables.LedgerAccounts, account, companyCode, tx);
            }
        }

        /// <summary>Where an account of the old chart that has no fixed place goes.</summary>
        private static string GeneralFor(AccountKind kind)
        {
            switch (kind)
            {
                case AccountKind.Liability: return "32";
                case AccountKind.Equity: return "51";
                case AccountKind.Income: return "62";
                case AccountKind.Expense: return "83";
                default: return "14";
            }
        }

        /// <summary>The first free code made of <paramref name="prefix"/> and <paramref name="digits"/> more digits; null when all are used.</summary>
        private static string NextChildCode(string prefix, int digits, HashSet<string> taken)
        {
            var format = new string('0', digits);
            var last = (int)Math.Pow(10, digits) - 1;
            for (var n = 1; n <= last; n++)
            {
                var code = prefix + n.ToString(format, CultureInfo.InvariantCulture);
                if (taken.Add(code)) return code;
            }
            return null;
        }

        public static string SaveAccount(string companyCode, StoreLedgerAccount account)
        {
            account.Code = LatinDigits((account.Code ?? "").Trim());
            account.Title = (account.Title ?? "").Trim();
            if (account.Title.Length == 0) return "عنوان حساب را بنویسید.";
            var all = GetAccounts(companyCode);
            var existing = account.Id > 0 ? all.FirstOrDefault(a => a.Id == account.Id) : null;
            if (account.Id > 0 && existing == null) return "حساب پیدا نشد.";
            if (existing != null)
            {
                if (existing.IsSystem || all.Any(a => a.ParentId == existing.Id))
                {
                    account.Code = existing.Code;
                    account.Level = existing.Level;
                    account.ParentId = existing.ParentId;
                }
                if (existing.IsSystem) account.Kind = existing.Kind;
                account.IsSystem = existing.IsSystem;
                if (account.Level != existing.Level && JournalLines(companyCode).Any(l => l.AccountId == existing.Id))
                    return "این حساب در سندها آمده و سطحش عوض نمی‌شود.";
            }
            else account.IsSystem = false;

            StoreLedgerAccount parent = null;
            var digits = 1;
            if (account.Level == AccountLevel.Group) account.ParentId = 0;
            else
            {
                parent = all.FirstOrDefault(a => a.Id == account.ParentId);
                var parentLevel = account.Level == AccountLevel.General ? AccountLevel.Group : AccountLevel.General;
                if (parent == null || parent.Level != parentLevel)
                    return account.Level == AccountLevel.General ? "گروه این حساب کل را انتخاب کنید." : "حساب کل این معین را انتخاب کنید.";
                digits = account.Level == AccountLevel.General ? 1 : 2;
            }
            var prefix = parent == null ? "" : parent.Code;
            if (account.Code.Length == 0)
            {
                account.Code = NextChildCode(prefix, digits, new HashSet<string>(all.Select(a => a.Code), StringComparer.OrdinalIgnoreCase));
                if (account.Code == null) return "زیر این حساب کد خالی نمانده است.";
            }
            if (!Regex.IsMatch(account.Code, "^" + Regex.Escape(prefix) + @"\d{" + digits.ToString(CultureInfo.InvariantCulture) + "}$"))
            {
                if (account.Level == AccountLevel.Group) return "کد گروه یک رقم است.";
                if (account.Level == AccountLevel.General) return "کد حساب کل دو رقم است و با کد گروهش («" + prefix + "») شروع می‌شود.";
                return "کد معین چهار رقم است و با کد حساب کلش («" + prefix + "») شروع می‌شود.";
            }
            if (all.Any(a => a.Id != account.Id && string.Equals(a.Code, account.Code, StringComparison.OrdinalIgnoreCase)))
                return "این کد قبلاً استفاده شده است.";

            if (parent != null)
            {
                var group = Ancestor(parent, AccountLevel.Group, all.ToDictionary(a => a.Id));
                if (group != null) account.Kind = group.Kind;
            }
            if (account.Level != AccountLevel.Subsidiary)
            {
                account.Detail1 = DetailUse.None;
                account.Detail2 = DetailUse.None;
            }
            Save(NikooTables.LedgerAccounts, companyCode, account);

            if (account.Level == AccountLevel.Group && existing != null && existing.Kind != account.Kind)
            {
                var byId = all.ToDictionary(a => a.Id);
                foreach (var child in all.Where(a => a.Id != account.Id && Under(a, account.Id, byId)))
                {
                    child.Kind = account.Kind;
                    Save(NikooTables.LedgerAccounts, companyCode, child);
                }
            }
            return null;
        }

        public static string DeleteAccount(string companyCode, int id)
        {
            var account = GetAccount(companyCode, id);
            if (account == null) return null;
            if (account.IsSystem) return "حساب‌های پیش‌فرض حذف نمی‌شوند؛ غیرفعالشان کنید.";
            if (GetAccounts(companyCode).Any(a => a.ParentId == id)) return "اول حساب‌های زیرمجموعه را حذف یا جابه‌جا کنید.";
            if (JournalLines(companyCode).Any(l => l.AccountId == id)) return "این حساب در سندها آمده و حذف نمی‌شود.";
            Remove(NikooTables.LedgerAccounts, companyCode, id);
            return null;
        }

        /// <summary>The account itself or the parent above it at <paramref name="level"/>; null when the chain has none.</summary>
        private static StoreLedgerAccount Ancestor(StoreLedgerAccount account, AccountLevel level, Dictionary<int, StoreLedgerAccount> byId)
        {
            var current = account;
            for (var depth = 0; current != null && depth < 4; depth++)
            {
                if (current.Level == level) return current;
                StoreLedgerAccount parent;
                current = current.ParentId > 0 && byId.TryGetValue(current.ParentId, out parent) ? parent : null;
            }
            return null;
        }

        /// <summary>Whether <paramref name="account"/> is <paramref name="ancestorId"/> or sits somewhere below it.</summary>
        private static bool Under(StoreLedgerAccount account, int ancestorId, Dictionary<int, StoreLedgerAccount> byId)
        {
            var current = account;
            for (var depth = 0; current != null && depth < 4; depth++)
            {
                if (current.Id == ancestorId) return true;
                StoreLedgerAccount parent;
                current = current.ParentId > 0 && byId.TryGetValue(current.ParentId, out parent) ? parent : null;
            }
            return false;
        }

        // ---- Detail accounts

        public static List<StoreDetailAccount> GetDetails(string companyCode) =>
            All(NikooTables.DetailAccounts, companyCode, "[Level], [Code], [Id]");

        public static StoreDetailAccount GetDetail(string companyCode, int id) => id > 0 ? Find(NikooTables.DetailAccounts, companyCode, id) : null;

        public static string SaveDetail(string companyCode, StoreDetailAccount detail)
        {
            detail.Title = (detail.Title ?? "").Trim();
            detail.Code = Blank(LatinDigits(detail.Code));
            detail.Note = Blank(detail.Note);
            if (detail.Level != 2) detail.Level = 1;
            var all = GetDetails(companyCode);
            var existing = detail.Id > 0 ? all.FirstOrDefault(d => d.Id == detail.Id) : null;
            if (detail.Id > 0 && existing == null) return "حساب تفصیلی پیدا نشد.";
            if (existing != null)
            {
                detail.PartyId = existing.PartyId;
                detail.CreatedAt = existing.CreatedAt;
                if (existing.PartyId > 0)
                {
                    detail.Title = existing.Title;
                    detail.Level = 1;
                    detail.Kind = DetailKind.Person;
                    detail.IsActive = existing.IsActive;
                }
                if (detail.Level != existing.Level && DetailUsed(companyCode, existing.Id))
                    return "این تفصیلی در سندها آمده و سطحش عوض نمی‌شود.";
            }
            else detail.PartyId = 0;
            if (detail.Title.Length == 0) return "عنوان حساب تفصیلی را بنویسید.";
            if (all.Any(d => d.Id != detail.Id && d.Level == detail.Level && string.Equals(d.Title, detail.Title, StringComparison.OrdinalIgnoreCase)))
                return "تفصیلی دیگری با این عنوان در همین سطح هست.";
            if (detail.Code == null) detail.Code = NextNumber(all.Select(d => d.Code), detail.Level.ToString(CultureInfo.InvariantCulture));
            if (!Regex.IsMatch(detail.Code, @"^\d{2,10}$")) return "کد تفصیلی فقط رقم است، بین ۲ تا ۱۰ رقم.";
            if (all.Any(d => d.Id != detail.Id && d.Code == detail.Code)) return "این کد تفصیلی قبلاً استفاده شده است.";
            Save(NikooTables.DetailAccounts, companyCode, detail);
            return null;
        }

        public static string DeleteDetail(string companyCode, int id)
        {
            var detail = GetDetail(companyCode, id);
            if (detail == null) return null;
            if (detail.PartyId > 0) return "این تفصیلی مال یکی از تامین‌کنندگان و مشتریان است و با حذف همان طرف حساب حذف می‌شود.";
            if (DetailUsed(companyCode, id)) return "این تفصیلی در سندها آمده و حذف نمی‌شود؛ غیرفعالش کنید.";
            Remove(NikooTables.DetailAccounts, companyCode, id);
            return null;
        }

        private static bool DetailUsed(string companyCode, int id) =>
            JournalLines(companyCode).Any(l => l.Detail1Id == id || l.Detail2Id == id)
            || NikooDb.Count(NikooTables.CashTransactions, SqlFilter.ForStore(companyCode).Eq("Detail2Id", id)) > 0;

        private static readonly Dictionary<string, string> DetailSorts = new Dictionary<string, string>
        {
            { "code", "t.[Code]" },
            { "title", "t.[Title]" },
            { "level", "t.[Level], t.[Code]" },
            { "kind", "t.[Kind], t.[Code]" }
        };

        public static PagedList<StoreDetailAccount> DetailGrid(string companyCode, DetailGridQuery query)
        {
            var filter = SqlFilter.ForStore(companyCode).Search(query.Q, "Code", "Title", "Note");
            if (query.Level.HasValue) filter.Eq("Level", query.Level.Value);
            if (query.Kind.HasValue) filter.Eq("Kind", query.Kind.Value.ToString());
            return Grid(NikooTables.DetailAccounts, companyCode, query, filter, DetailSorts, "level", false);
        }

        /// <summary>Debit minus credit of each detail account, over all documents.</summary>
        public static Dictionary<int, long> DetailBalances(string companyCode)
        {
            var sums = new Dictionary<int, long>();
            foreach (var line in JournalLines(companyCode))
            {
                foreach (var id in new[] { line.Detail1Id, line.Detail2Id })
                {
                    if (id <= 0) continue;
                    long sum;
                    sums.TryGetValue(id, out sum);
                    sums[id] = sum + line.Debit - line.Credit;
                }
            }
            return sums;
        }

        /// <summary>The level-1 detail account of a supplier or customer, made the first time and kept in step with its name.</summary>
        private static StoreDetailAccount PartyDetail(string companyCode, StoreParty party, List<StoreDetailAccount> details = null)
        {
            details = details ?? GetDetails(companyCode);
            var detail = details.FirstOrDefault(d => d.PartyId == party.Id);
            if (detail == null)
            {
                detail = new StoreDetailAccount
                {
                    Code = NextNumber(details.Select(d => d.Code), "1"),
                    Title = party.Name,
                    Level = 1,
                    Kind = DetailKind.Person,
                    PartyId = party.Id,
                    IsActive = party.IsActive
                };
                Save(NikooTables.DetailAccounts, companyCode, detail);
                details.Add(detail);
            }
            else if (detail.Title != party.Name || detail.IsActive != party.IsActive)
            {
                detail.Title = party.Name;
                detail.IsActive = party.IsActive;
                Save(NikooTables.DetailAccounts, companyCode, detail);
            }
            return detail;
        }

        private static void EnsurePartyDetails(string companyCode)
        {
            var details = GetDetails(companyCode);
            foreach (var party in GetParties(companyCode))
                PartyDetail(companyCode, party, details);
        }

        /// <summary>Names the party's detail account on the lines whose subsidiary account takes a level-1 detail.</summary>
        private static void TagParty(string companyCode, List<StoreJournalLine> lines, int partyId)
        {
            if (partyId <= 0) return;
            var detail = GetDetails(companyCode).FirstOrDefault(d => d.PartyId == partyId);
            if (detail == null) return;
            var accounts = GetAccounts(companyCode).ToDictionary(a => a.Id);
            foreach (var line in lines)
            {
                StoreLedgerAccount account;
                if (line.Detail1Id == 0 && accounts.TryGetValue(line.AccountId, out account) && account.Detail1 != DetailUse.None)
                    line.Detail1Id = detail.Id;
            }
        }
    }
}
