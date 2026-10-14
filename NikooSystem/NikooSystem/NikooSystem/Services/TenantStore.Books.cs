using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models.Store;

namespace NikooSystem.Services
{
    /// <summary>
    /// Trial balance at the group, general, subsidiary or detail level, the general, subsidiary and detail ledgers, and the
    /// voucher of one entry. A reversed entry and the entry that reverses it are both counted, so together they come to zero.
    /// </summary>
    public static partial class TenantStore
    {
        private sealed class Posting
        {
            public StoreJournalEntry Entry;
            public StoreJournalLine Line;
        }

        private static List<Posting> Postings(string companyCode, DateTime? to) =>
            GetJournal(companyCode)
                .Where(e => !to.HasValue || e.EntryDate <= to.Value)
                .OrderBy(e => e.EntryDate).ThenBy(e => e.Id)
                .SelectMany(e => (e.Lines ?? new List<StoreJournalLine>()).Select(l => new Posting { Entry = e, Line = l }))
                .ToList();

        /// <summary>
        /// Turnover of each account at <paramref name="level"/> (group, general, subsidiary or detail) between the dates,
        /// with the balance brought forward from before <paramref name="from"/>, optionally only below
        /// <paramref name="parentId"/>. Rows with neither turnover nor an opening balance are left out.
        /// </summary>
        public static List<AccountBalance> TrialBalance(string companyCode, string level, int? parentId, DateTime? from, DateTime? to)
        {
            var byId = GetAccounts(companyCode).ToDictionary(a => a.Id);
            var details = GetDetails(companyCode).ToDictionary(d => d.Id);
            var rows = new Dictionary<string, AccountBalance>();
            foreach (var posting in Postings(companyCode, to))
            {
                var before = from.HasValue && posting.Entry.EntryDate < from.Value;
                StoreLedgerAccount account;
                byId.TryGetValue(posting.Line.AccountId, out account);
                if (parentId.HasValue && !Under(account, parentId.Value, byId)) continue;
                if (level == "detail")
                {
                    foreach (var id in new[] { posting.Line.Detail1Id, posting.Line.Detail2Id })
                    {
                        StoreDetailAccount detail;
                        if (id > 0 && details.TryGetValue(id, out detail))
                            AddTurnover(rows, "d" + id, () => new AccountBalance { Detail = detail }, posting.Line, before);
                    }
                    continue;
                }
                var target = level == "group" ? Ancestor(account, AccountLevel.Group, byId)
                    : level == "general" ? Ancestor(account, AccountLevel.General, byId)
                    : account;
                if (target != null) AddTurnover(rows, "a" + target.Id, () => new AccountBalance { Account = target }, posting.Line, before);
            }
            return rows.Values.Where(r => r.Debit != 0 || r.Credit != 0 || r.Opening != 0).ToList();
        }

        private static void AddTurnover(Dictionary<string, AccountBalance> rows, string key, Func<AccountBalance> create, StoreJournalLine line, bool opening)
        {
            AccountBalance row;
            if (!rows.TryGetValue(key, out row)) rows[key] = row = create();
            if (opening)
            {
                row.Opening += line.Debit - line.Credit;
                return;
            }
            row.Debit += line.Debit;
            row.Credit += line.Credit;
        }

        /// <summary>
        /// Movements of <see cref="BooksPage.Account"/> (with everything below it) and/or <see cref="BooksPage.Detail"/>
        /// between the page's dates, oldest first with a running balance. Fills the page's opening balance and totals.
        /// </summary>
        public static List<LedgerMovement> Ledger(string companyCode, BooksPage page)
        {
            var byId = GetAccounts(companyCode).ToDictionary(a => a.Id);
            var details = GetDetails(companyCode).ToDictionary(d => d.Id);
            var postings = Postings(companyCode, page.To);
            var account = page.Account;
            var detail = page.Detail;
            page.PerEntry = account != null && !account.IsPosting && detail == null;

            if (account != null && account.IsPosting)
            {
                var ids = new HashSet<int>(postings.Where(p => p.Line.AccountId == account.Id)
                    .SelectMany(p => new[] { p.Line.Detail1Id, p.Line.Detail2Id }).Where(id => id > 0));
                page.DetailChoices = details.Values.Where(d => ids.Contains(d.Id)).OrderBy(d => d.Level).ThenBy(d => d.Code).ToList();
            }

            Func<Posting, bool> matches = p =>
            {
                if (account != null)
                {
                    StoreLedgerAccount lineAccount;
                    if (!byId.TryGetValue(p.Line.AccountId, out lineAccount) || !Under(lineAccount, account.Id, byId)) return false;
                }
                return detail == null || p.Line.Detail1Id == detail.Id || p.Line.Detail2Id == detail.Id;
            };

            var rows = new List<LedgerMovement>();
            long running = 0, opening = 0;
            foreach (var entry in postings.Where(matches).GroupBy(p => p.Entry.Id))
            {
                var head = entry.First().Entry;
                if (page.From.HasValue && head.EntryDate < page.From.Value)
                {
                    opening += entry.Sum(p => p.Line.Debit - p.Line.Credit);
                    running = opening;
                    continue;
                }
                if (page.PerEntry)
                {
                    var debit = entry.Sum(p => p.Line.Debit);
                    var credit = entry.Sum(p => p.Line.Credit);
                    running += debit - credit;
                    rows.Add(new LedgerMovement
                    {
                        EntryId = head.Id,
                        Date = head.EntryDate,
                        Number = head.Number,
                        Description = head.Description,
                        Account = string.Join("، ", entry.Select(p => AccountLabel(p.Line.AccountId, byId)).Distinct()),
                        Debit = debit,
                        Credit = credit,
                        Balance = running
                    });
                    continue;
                }
                foreach (var posting in entry)
                {
                    running += posting.Line.Debit - posting.Line.Credit;
                    rows.Add(new LedgerMovement
                    {
                        EntryId = head.Id,
                        Date = head.EntryDate,
                        Number = head.Number,
                        Description = head.Description,
                        Memo = posting.Line.Memo,
                        Account = AccountLabel(posting.Line.AccountId, byId),
                        Detail1 = DetailLabel(posting.Line.Detail1Id, details),
                        Detail2 = DetailLabel(posting.Line.Detail2Id, details),
                        Debit = posting.Line.Debit,
                        Credit = posting.Line.Credit,
                        Balance = running
                    });
                }
            }
            page.Opening = opening;
            page.Debit = rows.Sum(r => r.Debit);
            page.Credit = rows.Sum(r => r.Credit);
            return rows;
        }

        public static VoucherPage Voucher(string companyCode, int entryId)
        {
            var entry = GetEntry(companyCode, entryId);
            if (entry == null) return null;
            var byId = GetAccounts(companyCode).ToDictionary(a => a.Id);
            var details = GetDetails(companyCode).ToDictionary(d => d.Id);
            var type = GetDocumentTypes(companyCode).FirstOrDefault(t => t.Id == entry.DocumentTypeId);
            var page = new VoucherPage { Entry = entry, TypeTitle = type != null ? type.Title : AccountingText.Source(entry.Source) };
            foreach (var line in (entry.Lines ?? new List<StoreJournalLine>()).OrderBy(l => l.Credit > 0 ? 1 : 0))
            {
                StoreLedgerAccount account;
                StoreDetailAccount detail1, detail2;
                byId.TryGetValue(line.AccountId, out account);
                details.TryGetValue(line.Detail1Id, out detail1);
                details.TryGetValue(line.Detail2Id, out detail2);
                page.Lines.Add(new VoucherLine
                {
                    Line = line,
                    Account = account,
                    General = Ancestor(account, AccountLevel.General, byId),
                    Group = Ancestor(account, AccountLevel.Group, byId),
                    Detail1 = detail1,
                    Detail2 = detail2
                });
                page.Debit += line.Debit;
                page.Credit += line.Credit;
            }
            return page;
        }

        private static string AccountLabel(int accountId, Dictionary<int, StoreLedgerAccount> byId)
        {
            StoreLedgerAccount account;
            return byId.TryGetValue(accountId, out account) ? account.Code + " — " + account.Title : "حساب حذف‌شده";
        }

        private static string DetailLabel(int detailId, Dictionary<int, StoreDetailAccount> details)
        {
            StoreDetailAccount detail;
            return detailId > 0 && details.TryGetValue(detailId, out detail) ? detail.Code + " — " + detail.Title : null;
        }
    }
}
