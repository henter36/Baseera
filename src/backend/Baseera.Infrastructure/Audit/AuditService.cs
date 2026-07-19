namespace Baseera.Infrastructure.Audit;

using System.Text.Json;
using Baseera.Application.Abstractions;
using Baseera.Domain.Audit;
using Baseera.Infrastructure.Persistence;

public sealed class AuditService(BaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            Action = entry.Action,
            Module = entry.Module,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            OldValuesJson = SerializeSafe(entry.OldValues),
            NewValuesJson = SerializeSafe(entry.NewValues),
            Reason = entry.Reason,
            Outcome = entry.Outcome,
            IsSensitiveView = entry.IsSensitiveView,
            UserId = currentUser.UserId?.ToString() ?? currentUser.ExternalSubject,
            UserDisplayName = currentUser.DisplayName,
            IpAddress = currentUser.IpAddress,
            CorrelationId = currentUser.CorrelationId,
            UserScopeSummary = scope.SummarizeScopes(),
            OccurredAtUtc = DateTimeOffset.UtcNow
        };

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? SerializeSafe(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(value, JsonOptions);
        // Never persist obvious secrets.
        if (json.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("access_token", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("refresh_token", StringComparison.OrdinalIgnoreCase))
        {
            return "{\"redacted\":true}";
        }

        return json;
    }
}
