
# AuditLog.Core

## Overview

AuditLog.Core is a .NET Core library designed to track and log changes made to entities within an Entity Framework Core context. This library captures insert, update, and delete operations, logging the details of the changes to an audit log for future reference.

## Features

- **Entity Tracking:** Automatically tracks changes to entities in your `DbContext`.
- **Audit Logs:** Logs insert, update, and delete operations, including before and after values.
- **Customizable:** Easily extendable to support different audit log formats and data providers.

## Installation

### 1. Clone the Repository

To start using AuditLog.Core, first clone the repository:

```bash
git clone https://github.com/yourusername/AuditLog.Core.git
```

### 2. Build the Project

- Open the solution in Visual Studio or your preferred IDE.
- Restore the NuGet packages.
- Build the solution.

### 3. Configure Your Application

Make sure your application is configured to use dependency injection, and then add the `AuditLogService` to your services:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<IAuditLogService, AuditLogService>();
    services.AddDbContext<YourDbContext>(options =>
        options.UseSqlServer(Configuration.GetConnectionString("YourConnectionString")));
}
```

## Usage

### 1. Add the `AuditLogService` to Your `DbContext`

To integrate the audit logging into your application, modify your `DbContext` class to use the `AuditLogService`. This service will be responsible for capturing changes and creating audit entries.

```csharp
public class YourDbContext : DbContext
{
    private readonly IAuditLogService _auditLogService;

    public YourDbContext(DbContextOptions<YourDbContext> options, IAuditLogService auditLogService)
        : base(options)
    {
        _auditLogService = auditLogService;
    }

    public override int SaveChanges()
    {
        _auditLogService.AuditChanges(this);
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _auditLogService.AuditChanges(this);
        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 2. Implement `IAuditLogService`

Create a class that implements the `IAuditLogService` interface. This class will handle the logic for auditing the changes.

```csharp
public class AuditLogService : IAuditLogService
{
    public void AuditChanges(DbContext context)
    {
        var auditEntries = CreateAuditEntries(context);

        if (auditEntries.Any())
        {
            // Save the audit entries to the database or other storage
            SaveAuditEntries(context, auditEntries);
        }
    }

    private List<EventEntry> CreateAuditEntries(DbContext context)
    {
        var auditEntries = new List<EventEntry>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged || entry.Entity is TransactionLog)
                continue;

            var entityType = context.Model.FindEntityType(entry.Entity.GetType().BaseType ?? entry.Entity.GetType()) 
                            ?? context.Model.FindEntityType(entry.Entity.GetType());
            var tableName = entityType?.GetTableName() ?? GetTableNameFromType(entry.Entity.GetType());
            var schema = entityType?.GetSchema();

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

            if (entry.State == EntityState.Modified)
            {
                foreach (var property in entry.Properties)
                {
                    if (!Equals(property.OriginalValue, property.CurrentValue))
                    {
                        eventEntry.Changes.Add(new EventEntryChange
                        {
                            ColumnName = property.Metadata.Name,
                            OriginalValue = property.OriginalValue,
                            NewValue = property.CurrentValue
                        });
                    }
                }

                if (!eventEntry.Changes.Any())
                    continue;
            }
            else if (entry.State == EntityState.Added)
            {
                foreach (var property in entry.Properties)
                {
                    eventEntry.Changes.Add(new EventEntryChange
                    {
                        ColumnName = property.Metadata.Name,
                        OriginalValue = null,
                        NewValue = property.CurrentValue
                    });
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                foreach (var property in entry.Properties)
                {
                    eventEntry.Changes.Add(new EventEntryChange
                    {
                        ColumnName = property.Metadata.Name,
                        OriginalValue = property.OriginalValue,
                        NewValue = null
                    });
                }
            }

            auditEntries.Add(eventEntry);
        }

        return auditEntries;
    }

    private void SaveAuditEntries(DbContext context, List<EventEntry> auditEntries)
    {
        context.Set<TransactionLog>().AddRange(auditEntries.Select(entry => new TransactionLog
        {
            Action = entry.Action,
            ActionDateTime = DateTime.UtcNow,
            TableName = entry.Table,
            TablePk = entry.PrimaryKey.Values.FirstOrDefault()?.ToString(),
            OldValues = entry.Action == "Update" || entry.Action == "Delete" 
                        ? Newtonsoft.Json.JsonConvert.SerializeObject(entry.Changes.ToDictionary(c => c.ColumnName, c => c.OriginalValue))
                        : null,
            NewValues = entry.Action == "Insert" || entry.Action == "Update"
                        ? Newtonsoft.Json.JsonConvert.SerializeObject(entry.Changes.ToDictionary(c => c.ColumnName, c => c.NewValue))
                        : null,
            UserName = context.HttpContext?.User.Identity.Name ?? "system"
        }));

        context.SaveChanges();
    }

    private string GetTableNameFromType(Type entityType)
    {
        return entityType.GetCustomAttributes(typeof(TableAttribute), true)
               .Cast<TableAttribute>()
               .FirstOrDefault()?.Name ?? entityType.Name;
    }
}
```

### 3. Entity Classes

Make sure your entities are correctly set up with the necessary attributes, such as `[Table]` if you need specific table names.

```csharp
[Table("Employees")]
public class Employee
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string PhoneNumber { get; set; }
}
```

### 4. Example Usage

```csharp
using (var context = new YourDbContext())
{
    var employee = context.Employees.FirstOrDefault();
    if (employee != null)
    {
        employee.FirstName = "Updated Name";
        context.SaveChanges(); // This will trigger audit logging
    }
}
```

## Contributing

1. **Fork the Repository:**
   - Create your own fork of the repository.
   
2. **Create a Feature Branch:**
   - Create a new branch for your feature or bugfix.
   
   ```bash
   git checkout -b feature-branch
   ```

3. **Commit Your Changes:**

   ```bash
   git commit -m "Add feature or fix bug"
   ```

4. **Push to the Branch:**
   ```bash
   git push origin feature-branch
   ```

5. **Open a Pull Request:**
   - Open a pull request to merge your changes into the main repository.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contact

For any questions or issues, feel free to open an issue on GitHub or contact me directly at [molhamalnayyal@gmail.com].

