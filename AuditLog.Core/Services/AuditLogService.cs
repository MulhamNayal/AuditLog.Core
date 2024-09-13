using AuditLog.Core.AuditEntities;
using AuditLog.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditLog.Core.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private bool _bypassAudit = false;

        public AuditLogService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void AuditChanges(DbContext context)
        {
            if (_bypassAudit) return;

            var auditEntries = CreateAuditEntries(context);

            // Save the main entities to the database
            context.SaveChanges();

            // Update the primary keys for entries that were inserted
            foreach (var entry in auditEntries.Where(e => e.Action == "Insert"))
            {
                var dbEntry = context.Entry(entry.Entity);
                entry.PrimaryKey = dbEntry.Properties
                                         .Where(p => p.Metadata.IsPrimaryKey())
                                         .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
            }

            if (auditEntries.Any())
            {
                SaveAuditEntries(context, auditEntries);
            }
        }

        private List<EventEntry> CreateAuditEntries(DbContext context)
        {
            return context.ChangeTracker.Entries()
                .Where(entry => entry.State != EntityState.Detached
                             && entry.State != EntityState.Unchanged
                             && !(entry.Entity is TransactionLog))
                .Select(entry =>
                {
                    // Determine entity type, table name, and schema
                    var entityType = context.Model.FindEntityType(entry.Entity.GetType().BaseType ?? entry.Entity.GetType())
                                    ?? context.Model.FindEntityType(entry.Entity.GetType());
                    var tableName = entityType?.GetTableName() ?? GetTableNameFromType(entry.Entity.GetType());
                    var schema = entityType?.GetSchema();

                    // Initialize the event entry
                    var eventEntry = new EventEntry
                    {
                        Schema = schema,
                        Table = tableName,
                        Action = entry.State switch
                        {
                            EntityState.Added => "Insert",
                            EntityState.Modified => "Update",
                            EntityState.Deleted => "Delete",
                            _ => null
                        },
                        PrimaryKey = entry.Properties
                                         .Where(p => p.Metadata.IsPrimaryKey())
                                         .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue),
                        ColumnValues = entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue),
                        Entity = entry.Entity,
                        Changes = new List<EventEntryChange>()
                    };

                    // Handle property changes based on entity state
                    if (entry.State == EntityState.Added)
                    {
                        eventEntry.Changes.AddRange(entry.Properties.Select(property => new EventEntryChange
                        {
                            ColumnName = property.Metadata.Name,
                            OriginalValue = null,
                            NewValue = property.CurrentValue
                        }));
                    }
                    else if (entry.State == EntityState.Deleted)
                    {
                        eventEntry.Changes.AddRange(entry.Properties.Select(property => new EventEntryChange
                        {
                            ColumnName = property.Metadata.Name,
                            OriginalValue = property.OriginalValue,
                            NewValue = null
                        }));
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        eventEntry.Changes.AddRange(entry.Properties
                            .Where(property => !Equals(property.OriginalValue, property.CurrentValue))
                            .Select(property => new EventEntryChange
                            {
                                ColumnName = property.Metadata.Name,
                                OriginalValue = property.OriginalValue,
                                NewValue = property.CurrentValue
                            }));

                        // Skip if no real changes
                        if (!eventEntry.Changes.Any())
                            return null;
                    }

                    return eventEntry;
                })
                .Where(entry => entry != null)
                .ToList();
        }

        private string GetTableNameFromType(Type entityType)
        {
            // Use reflection to get the table name or default to the CLR type name
            return entityType.GetCustomAttributes(typeof(TableAttribute), true)
                       .Cast<TableAttribute>()
                       .FirstOrDefault()?.Name ?? entityType.Name;
        }

        private void SaveAuditEntries(DbContext context, List<EventEntry> auditEntries)
        {
            _bypassAudit = true; // Temporarily bypass audit to prevent recursion

            var transactionLogs = auditEntries.Select(entry => new TransactionLog
            {
                Action = entry.Action,
                ActionDateTime = DateTime.UtcNow,
                TableName = entry.Table,
                TablePk = entry.PrimaryKey.Values.FirstOrDefault()?.ToString(),
                OldValues = entry.Action == "Update" || entry.Action == "Delete"
                            ? GetSerializedValues(entry.Changes, true)
                            : null,
                NewValues = entry.Action == "Insert" || entry.Action == "Update"
                            ? GetSerializedValues(entry.Changes, false)
                            : null,
                UserName = _httpContextAccessor.HttpContext?.User.FindFirst("Claim")?.Value ?? "system"
            }).ToList();

            context.Set<TransactionLog>().AddRange(transactionLogs);
            context.SaveChanges();

            _bypassAudit = false; // Reset bypass after saving
        }

        private string GetSerializedValues(IEnumerable<EventEntryChange> changes, bool isOldValues)
        {
            var values = changes.ToDictionary(
                change => change.ColumnName,
                change => isOldValues ? change.OriginalValue : change.NewValue);

            return Newtonsoft.Json.JsonConvert.SerializeObject(values);
        }
    }


}
