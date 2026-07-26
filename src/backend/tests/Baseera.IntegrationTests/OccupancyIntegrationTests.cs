using System.Net.Http.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Occupancy;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

[Collection(OperationsIntegrationCollection.Name)]
public sealed class OccupancyIntegrationTests(OperationsIntegrationFixture fixture)
    : IntegrationTestBase<OperationsIntegrationFixture>(fixture)
{
    private BaseeraApiFactory factory => Factory;

    [IntegrationConnectionFact]
    public async Task Concurrent_movement_import_is_idempotent_under_unique_constraint()
    {
        const string subject = "occupancy-import-race";
        var externalEventId = $"race-{Guid.NewGuid():N}";
        await factory.SeedUserAsync(
            subject,
            "مدير إشغال",
            [RoleCodes.SystemAdministrator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var firstClient = factory.CreateAuthenticatedClient(subject);
        var secondClient = factory.CreateAuthenticatedClient(subject);

        var body = new
        {
            sourceSystem = "race-system",
            importReference = $"batch-{Guid.NewGuid():N}",
            rows = new[]
            {
                new
                {
                    inmateReferenceHash = $"hash-{externalEventId}",
                    movementType = MovementType.Admission,
                    toFacilityId = SeedIds.FacilityA1,
                    occurredAtUtc = DateTimeOffset.UtcNow,
                    externalEventId
                }
            }
        };

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/occupancy/movements/import", body),
            secondClient.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/occupancy/movements/import", body));

        Assert.All(responses, response => response.EnsureSuccessStatusCode());
        var results = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<ImportResult>()));

        Assert.Equal(1, results.Sum(result => result?.AcceptedRows ?? 0));
        Assert.Equal(1, results.Sum(result => result?.DuplicateRows ?? 0));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var stored = await db.InmateMovementEvents.CountAsync(movement =>
            movement.SourceReference == "race-system" &&
            movement.ExternalEventId == externalEventId);
        Assert.Equal(1, stored);
    }

    private sealed record ImportResult(int AcceptedRows, int DuplicateRows, IReadOnlyList<string> RejectedRows);
}
