using AuditLog.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuditLog.Core.Models
{
    public class AuditLogContext : AuditLogDbContext
    {
        private readonly IConfiguration _configuration;
        private readonly IAuditLogService _auditLogService;

        public AuditLogContext(DbContextOptions<AuditLogContext> options, IConfiguration Configuration, IAuditLogService auditLogService)
        {
            _configuration = Configuration;
            _auditLogService = auditLogService;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = "AuditLogConnection";

                optionsBuilder.EnableSensitiveDataLogging();

                optionsBuilder.UseLazyLoadingProxies().UseSqlServer(
                    _configuration.GetConnectionString(connectionString),
                    options => options.CommandTimeout(70)
                );
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _auditLogService.AuditChanges(this); // Audit the changes before saving

            return await base.SaveChangesAsync(cancellationToken);
        }

    }
}
