namespace AuditLog.Core.AuditEntities
{
    using System.Collections.Generic;

    public class EventEntry
    {
        public string Schema { get; set; }
        public string Table { get; set; }
        public string Action { get; set; }
        public IDictionary<string, object> PrimaryKey { get; set; }
        public List<EventEntryChange> Changes { get; set; }
        public IDictionary<string, object> ColumnValues { get; set; }
        public object Entity { get; set; }
    }

}
