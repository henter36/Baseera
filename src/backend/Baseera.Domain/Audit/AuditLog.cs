namespace Baseera.Domain.Audit;

using Baseera.Domain.Common;

/// <summary>
/// Append-only audit record. Must never be updated or deleted by application code.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? Reason { get; set; }
    public string? UserScopeSummary { get; set; }
    public string Outcome { get; set; } = "Success";
    public bool IsSensitiveView { get; set; }
}
