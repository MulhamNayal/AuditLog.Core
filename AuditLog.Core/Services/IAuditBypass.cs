namespace AuditLog.Core.Services
{
    public interface IAuditBypass
    {
        void SetBypassAudit(bool bypass);
    }
}
