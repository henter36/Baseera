namespace Baseera.Infrastructure.Audit;

using System.Text.Json;
using System.Text.RegularExpressions;
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

    private static readonly Regex SecretPattern = new(
        "(password|secret|clientsecret|token|authorization|connectionstring|apikey|access_token|refresh_token)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Stages an append-only audit row in the same DbContext. Caller must SaveChanges
    /// so operational change + audit commit atomically.
    /// </summary>
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
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
        return Task.CompletedTask;
    }

    private static string? SerializeSafe(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(value, JsonOptions);
        if (SecretPattern.IsMatch(json))
        {
            return "{\"redacted\":true}";
        }

        return json;
    }
}
