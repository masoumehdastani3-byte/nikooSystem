using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    /// <summary>
    /// Trial balance at the group, general, subsidiary or detail level, the general, subsidiary and detail ledgers, and the
    /// voucher of one entry. A reversed entry and the entry that reverses it are both counted, so together they come to zero.
    /// </summary>
    public static partial class TenantStore
    {
        private sealed class Turn
        {
            public int AccountId;
            public int Detail1Id;
            public int Detail2Id;
            public long OpenDebit;
            public long OpenCredit;
            public long Debit;
            public long Credit;
        }

        private sealed class Move
        {
            public int EntryId;
            public DateTime Date;
            public string Number;
            public string Description;
            public int AccountId;
            public int Detail1Id;
            public int Detail2Id;
            public long Debit;
            public long Credit;
            public string Memo;
        }

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
            foreach (var turn in Turns(companyCode, from, to))
            {
                StoreLedgerAccount account;
                byId.TryGetValue(turn.AccountId, out account);
                if (parentId.HasValue && !Under(account, parentId.Value, byId)) continue;
                if (level == "detail")
                {
                    foreach (var id in new[] { turn.Detail1Id, turn.Detail2Id })
                    {
                        StoreDetailAccount detail;
                        if (id > 0 && details.TryGetValue(id, out detail))
                            AddTurn(rows, "d" + id, () => new AccountBalance { Detail = detail }, turn);
                    }
                    continue;
                }
                var target = level == "group" ? Ancestor(account, AccountLevel.Group, byId)
                    : level == "general" ? Ancestor(account, AccountLevel.General, byId)
                    : account;
                if (target != null) AddTurn(rows, "a" + target.Id, () => new AccountBalance { Account = target }, turn);
            }
            return rows.Values.Where(r => r.Debit != 0 || r.Credit != 0 || r.Opening != 0).ToList();
        }

        private static void AddTurn(Dictionary<string, AccountBalance> rows, string key, Func<AccountBalance> create, Turn turn)
        {
            if (turn.OpenDebit == turn.OpenCredit && turn.Debit == 0 && turn.Credit == 0) return;
            AccountBalance row;
            if (!rows.TryGetValue(key, out row)) rows[key] = row = create();
            row.Opening += turn.OpenDebit - turn.OpenCredit;
            row.Debit += turn.Debit;
            row.Credit += turn.Credit;
        }

        /// <summary>
        /// Movements of <see cref="BooksPage.Account"/> (with everything below it) and/or <see cref="BooksPage.Detail"/>
        /// between the page's dates, oldest first with a running balance. Fills the page's opening balance and totals.
        /// </summary>
        public static List<LedgerMovement> Ledger(string companyCode, BooksPage page)
        {
            var byId = GetAccounts(companyCode).ToDictionary(a => a.Id);
            var details = GetDetails(companyCode).ToDictionary(d => d.Id);
            var account = page.Account;
            var detail = page.Detail;
            page.PerEntry = account != null && !account.IsPosting && detail == null;
            var accountIds = PostingIds(account, byId);
            var detailId = detail == null ? (int?)null : detail.Id;

            if (account != null && account.IsPosting)
            {
                var ids = new HashSet<int>(DetailIdsOnAccount(companyCode, account.Id, page.To));
                page.DetailChoices = details.Values.Where(d => ids.Contains(d.Id)).OrderBy(d => d.Level).ThenBy(d => d.Code).ToList();
            }

            page.Opening = page.From.HasValue ? OpeningBalance(companyCode, accountIds, detailId, page.From.Value, page.To) : 0;
            var rows = new List<LedgerMovement>();
            var running = page.Opening;
            foreach (var entry in Movements(companyCode, accountIds, detailId, page.From, page.To).GroupBy(m => m.EntryId))
            {
                var head = entry.First();
                if (page.PerEntry)
                {
                    var debit = entry.Sum(m => m.Debit);
                    var credit = entry.Sum(m => m.Credit);
                    running += debit - credit;
                    rows.Add(new LedgerMovement
                    {
                        EntryId = head.EntryId,
                        Date = head.Date,
                        Number = head.Number,
                        Description = head.Description,
                        Account = string.Join("، ", entry.Select(m => AccountLabel(m.AccountId, byId)).Distinct()),
                        Debit = debit,
                        Credit = credit,
                        Balance = running
                    });
                    continue;
                }
                foreach (var move in entry)
                {
                    running += move.Debit - move.Credit;
                    rows.Add(new LedgerMovement
                    {
                        EntryId = head.EntryId,
                        Date = head.Date,
                        Number = head.Number,
                        Description = head.Description,
                        Memo = move.Memo,
                        Account = AccountLabel(move.AccountId, byId),
                        Detail1 = DetailLabel(move.Detail1Id, details),
                        Detail2 = DetailLabel(move.Detail2Id, details),
                        Debit = move.Debit,
                        Credit = move.Credit,
                        Balance = running
                    });
                }
            }
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

        private static string JournalFrom() =>
            " FROM " + NikooDb.Table("JournalLines") + " l INNER JOIN " + NikooDb.Table(NikooTables.JournalEntries.Name)
            + " e ON e.[CompanyCode] = l.[CompanyCode] AND e.[Id] = l.[EntryId]";

        /// <summary>Null means every posting account. An empty list matches nothing.</summary>
        private static List<int> PostingIds(StoreLedgerAccount account, Dictionary<int, StoreLedgerAccount> byId)
        {
            if (account == null) return null;
            if (account.IsPosting) return new List<int> { account.Id };
            return byId.Values.Where(a => a.IsPosting && Under(a, account.Id, byId)).Select(a => a.Id).ToList();
        }

        private static void LedgerWhere(string companyCode, List<int> accountIds, int? detailId, DateTime? to, Row parameters, List<string> clauses)
        {
            parameters["CompanyCode"] = companyCode;
            clauses.Add("e.[CompanyCode] = @CompanyCode");
            if (to.HasValue)
            {
                parameters["To"] = to.Value;
                clauses.Add("e.[EntryDate] <= @To");
            }
            if (accountIds != null)
            {
                if (accountIds.Count == 0) clauses.Add("1 = 0");
                else
                {
                    var names = new List<string>();
                    for (var i = 0; i < accountIds.Count; i++)
                    {
                        parameters["a" + i] = accountIds[i];
                        names.Add("@a" + i);
                    }
                    clauses.Add("l.[AccountId] IN (" + string.Join(", ", names) + ")");
                }
            }
            if (detailId.HasValue)
            {
                parameters["DetailId"] = detailId.Value;
                clauses.Add("(l.[Detail1Id] = @DetailId OR l.[Detail2Id] = @DetailId)");
            }
        }

        private static List<Turn> Turns(string companyCode, DateTime? from, DateTime? to)
        {
            var sql = "SELECT l.[AccountId], l.[Detail1Id], l.[Detail2Id], "
                + "SUM(CASE WHEN @HasFrom = 1 AND e.[EntryDate] < @From THEN l.[Debit] ELSE 0 END), "
                + "SUM(CASE WHEN @HasFrom = 1 AND e.[EntryDate] < @From THEN l.[Credit] ELSE 0 END), "
                + "SUM(CASE WHEN @HasFrom = 0 OR e.[EntryDate] >= @From THEN l.[Debit] ELSE 0 END), "
                + "SUM(CASE WHEN @HasFrom = 0 OR e.[EntryDate] >= @From THEN l.[Credit] ELSE 0 END)"
                + JournalFrom() + " WHERE e.[CompanyCode] = @CompanyCode"
                + (to.HasValue ? " AND e.[EntryDate] <= @To" : "")
                + " GROUP BY l.[AccountId], l.[Detail1Id], l.[Detail2Id]";
            var parameters = new Row
            {
                ["CompanyCode"] = companyCode,
                ["HasFrom"] = from.HasValue ? 1 : 0,
                ["From"] = from ?? DateTime.MinValue
            };
            if (to.HasValue) parameters["To"] = to.Value;
            return NikooDb.Select(sql, parameters, r => new Turn
            {
                AccountId = r.GetInt32(0),
                Detail1Id = r.GetInt32(1),
                Detail2Id = r.GetInt32(2),
                OpenDebit = Convert.ToInt64(r.GetValue(3)),
                OpenCredit = Convert.ToInt64(r.GetValue(4)),
                Debit = Convert.ToInt64(r.GetValue(5)),
                Credit = Convert.ToInt64(r.GetValue(6))
            });
        }

        private static long OpeningBalance(string companyCode, List<int> accountIds, int? detailId, DateTime from, DateTime? to)
        {
            var clauses = new List<string>();
            var parameters = new Row();
            LedgerWhere(companyCode, accountIds, detailId, to, parameters, clauses);
            parameters["From"] = from;
            clauses.Add("e.[EntryDate] < @From");
            var values = NikooDb.Column<long>("SELECT ISNULL(SUM(l.[Debit] - l.[Credit]), 0)" + JournalFrom()
                + " WHERE " + string.Join(" AND ", clauses), parameters);
            return values.Count == 0 ? 0 : values[0];
        }

        private static List<Move> Movements(string companyCode, List<int> accountIds, int? detailId, DateTime? from, DateTime? to)
        {
            var clauses = new List<string>();
            var parameters = new Row();
            LedgerWhere(companyCode, accountIds, detailId, to, parameters, clauses);
            if (from.HasValue)
            {
                parameters["From"] = from.Value;
                clauses.Add("e.[EntryDate] >= @From");
            }
            return NikooDb.Select(
                "SELECT e.[Id], e.[EntryDate], e.[Number], e.[Description], l.[AccountId], l.[Detail1Id], l.[Detail2Id], l.[Debit], l.[Credit], l.[Memo]"
                + JournalFrom() + " WHERE " + string.Join(" AND ", clauses)
                + " ORDER BY e.[EntryDate], e.[Id], l.[Position]",
                parameters, ReadMove);
        }

        private static List<int> DetailIdsOnAccount(string companyCode, int accountId, DateTime? to)
        {
            var clauses = new List<string>();
            var parameters = new Row();
            LedgerWhere(companyCode, new List<int> { accountId }, null, to, parameters, clauses);
            var where = string.Join(" AND ", clauses);
            return NikooDb.Column<int>(
                "SELECT [Detail1Id]" + JournalFrom() + " WHERE " + where + " AND l.[Detail1Id] > 0"
                + " UNION SELECT [Detail2Id]" + JournalFrom() + " WHERE " + where + " AND l.[Detail2Id] > 0",
                parameters);
        }

        private static Move ReadMove(System.Data.IDataRecord r) => new Move
        {
            EntryId = r.GetInt32(0),
            Date = r.GetDateTime(1),
            Number = r.IsDBNull(2) ? null : r.GetString(2),
            Description = r.IsDBNull(3) ? null : r.GetString(3),
            AccountId = r.GetInt32(4),
            Detail1Id = r.GetInt32(5),
            Detail2Id = r.GetInt32(6),
            Debit = Convert.ToInt64(r.GetValue(7)),
            Credit = Convert.ToInt64(r.GetValue(8)),
            Memo = r.IsDBNull(9) ? null : r.GetString(9)
        };
    }
}
