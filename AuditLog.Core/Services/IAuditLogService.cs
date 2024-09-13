using Microsoft.EntityFrameworkCore;

namespace AuditLog.Core.Services
{
    public interface IAuditLogService
    {
        void AuditChanges(DbContext context);
    }
}
