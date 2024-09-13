namespace AuditLog.Core.AuditEntities
{
    public class EventEntryChange
    {
        public string ColumnName { get; set; }
        public object OriginalValue { get; set; }
        public object NewValue { get; set; }
    }
}
