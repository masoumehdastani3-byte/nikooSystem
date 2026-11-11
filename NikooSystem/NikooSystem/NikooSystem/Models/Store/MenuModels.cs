using System;

namespace NikooSystem.Models.Store
{
    /// <summary>A guest's request for the waiter, from the QR menu of one table.</summary>
    public class WaiterCall : IStoreEntity
    {
        public int Id { get; set; }
        public string TableLabel { get; set; }
        public string Note { get; set; }
        public bool IsDone { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? DoneAt { get; set; }
    }
}
