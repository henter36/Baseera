namespace Baseera.UnitTests.SensitiveCustody;

using System.Security.Cryptography;
using Baseera.Application.Abstractions;
using Baseera.Application.SensitiveCustody;
using Baseera.Domain.Common;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

public sealed class SensitiveCustodySerialLoggingTests : IDisposable
{
    private readonly BaseeraDbContext db = NoteTestFixtures.CreateDb();
    private readonly CollectingLogger logger = new();

    public void Dispose() => db.Dispose();

    [Fact]
    public void TryUnprotectSerial_logs_cryptographic_exception_without_protected_value()
    {
        const string protectedValue = "cipher-SN-SHOULD-NOT-LEAK";
        var service = CreateService(new ThrowingProtector(new CryptographicException("unprotect failed")));

        var ok = service.TryUnprotectSerial(protectedValue, out var plaintext);

        Assert.False(ok);
        Assert.Equal(string.Empty, plaintext);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<CryptographicException>(entry.Exception);
        Assert.Contains("Failed to unprotect sensitive custody serial for authorized projection.", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(protectedValue, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(protectedValue, entry.StateText, StringComparison.Ordinal);
        Assert.DoesNotContain("SN-SHOULD-NOT-LEAK", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryUnprotectSerial_logs_format_exception_without_protected_value()
    {
        const string protectedValue = "bad-format-SERIAL-LEAK";
        var service = CreateService(new ThrowingProtector(new FormatException("bad format")));

        var ok = service.TryUnprotectSerial(protectedValue, out var plaintext);

        Assert.False(ok);
        Assert.Equal(string.Empty, plaintext);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.IsType<FormatException>(entry.Exception);
        Assert.Contains("invalid protected value format", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(protectedValue, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(protectedValue, entry.StateText, StringComparison.Ordinal);
    }

    private SensitiveCustodyService CreateService(ISensitiveValueProtector protector) =>
        new(
            db,
            new ScopeStub(),
            new UserStub(),
            new AuditStub(),
            protector,
            logger,
            TimeProvider.System);

    private sealed class ThrowingProtector(Exception exception) : ISensitiveValueProtector
    {
        public string Protect(string plaintext) => plaintext;

        public string Unprotect(string protectedValue) => throw exception;
    }

    private sealed class ScopeStub : IOrganizationalScopeService
    {
        public bool HasNationalAccess => true;
        public bool HasHeadquartersAccess => true;
        public bool CanAccessRegion(Guid regionId) => true;
        public bool CanAccessFacility(Guid facilityId) => true;
        public bool CanAccessFacilityUnit(Guid facilityUnitId) => true;
        public IQueryable<Region> FilterRegions(IQueryable<Region> query) => query;
        public IQueryable<Facility> FilterFacilities(IQueryable<Facility> query) => query;
        public bool CanAccess(IScopedEntity entity) => true;
        public string SummarizeScopes() => "test";
    }

    private sealed class UserStub : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => Guid.NewGuid();
        public string? ExternalSubject => "unit-test";
        public string? DisplayName => "Unit Test";
        public string? IpAddress => null;
        public string? CorrelationId => null;
        public IReadOnlyCollection<string> Permissions { get; } = [];
        public IReadOnlyCollection<UserScopeSnapshot> Scopes { get; } = [];
        public bool IsGlobalScope => true;
        public bool HasHeadquartersScope => true;
        public bool HasPermission(string permissionCode) => false;
    }

    private sealed class AuditStub : IAuditService
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CollectingLogger : ILogger<SensitiveCustodyService>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception), state?.ToString() ?? string.Empty));
        }
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message, string StateText);
}
