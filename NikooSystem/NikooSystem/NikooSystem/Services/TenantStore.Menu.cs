using System;
using System.Collections.Generic;
using System.Linq;
using NikooSystem.Models.Store;
using NikooSystem.Services.Data;

namespace NikooSystem.Services
{
    public static partial class TenantStore
    {
        /// <summary>The demo of the cafe QR menu.</summary>
        internal static void SeedCafeDemo()
        {
            if (CompanyCodeExists("CAFE")) return;
            AddDemo("CAFE", "کافه و رستوران نارون", "cafe");
        }

        public static List<WaiterCall> GetWaiterCalls(string companyCode)
        {
            return All(NikooTables.WaiterCalls, companyCode, "[CreatedAt] DESC, [Id] DESC");
        }

        public static int OpenWaiterCallCount(string companyCode) =>
            NikooDb.Count(NikooTables.WaiterCalls, SqlFilter.ForStore(companyCode).Eq("IsDone", false));

        public static WaiterCall AddWaiterCall(string companyCode, WaiterCall call)
        {
            call.Id = 0;
            call.IsDone = false;
            return Save(NikooTables.WaiterCalls, companyCode, call, (item, stored) => item.CreatedAt = DateTime.Now);
        }

        public static bool CloseWaiterCall(string companyCode, int id)
        {
            var call = Find(NikooTables.WaiterCalls, companyCode, id);
            if (call == null || call.IsDone) return call != null;
            call.IsDone = true;
            call.DoneAt = DateTime.Now;
            return Update(NikooTables.WaiterCalls, companyCode, call);
        }
    }
}
