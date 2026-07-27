namespace Baseera.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using Baseera.Application.RiskManagement;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Covers the UX_RiskAssessments_RiskRecordId_AssessmentType_InProgress filtered unique index: exactly one
/// RiskAssessment may exist per (RiskRecordId, AssessmentType) while Status is Draft/PendingReview/Reviewed.
/// The status-matrix cases insert directly against the DbContext (they exercise the raw constraint); the race
/// case goes through the full HTTP -> RiskAssessmentService.CreateAsync -> SaveChangesAsync path with a
/// SaveChangesInterceptor barrier forcing both concurrent requests past the application-level guard before
/// either commits, so the database index — not request ordering — is what actually arbitrates the winner.
/// </summary>
[Collection(RiskManagementIntegrationCollection.Name)]
public sealed class RiskAssessmentConcurrencyIntegrationTests(RiskManagementIntegrationFixture fixture)
    : IntegrationTestBase<RiskManagementIntegrationFixture>(fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private BaseeraApiFactory factory => Factory;

    [IntegrationConnectionFact]
    public async Task Draft_blocks_another_draft_of_the_same_type()
    {
        await AssertStatusBlocksNewInProgressAsync(AssessmentStatus.Draft, shouldBlock: true);
    }

    [IntegrationConnectionFact]
    public async Task PendingReview_blocks_another_of_the_same_type()
    {
        await AssertStatusBlocksNewInProgressAsync(AssessmentStatus.PendingReview, shouldBlock: true);
    }

    [IntegrationConnectionFact]
    public async Task Reviewed_blocks_another_of_the_same_type()
    {
        await AssertStatusBlocksNewInProgressAsync(AssessmentStatus.Reviewed, shouldBlock: true);
    }

    [IntegrationConnectionFact]
    public async Task Approved_allows_a_new_assessment_of_the_same_type()
    {
        await AssertStatusBlocksNewInProgressAsync(AssessmentStatus.Approved, shouldBlock: false);
    }

    [IntegrationConnectionFact]
    public async Task Superseded_allows_a_new_assessment_of_the_same_type()
    {
        await AssertStatusBlocksNewInProgressAsync(AssessmentStatus.Superseded, shouldBlock: false);
    }

    [IntegrationConnectionFact]
    public async Task Rejected_allows_a_new_assessment_of_the_same_type()
    {
        await AssertStatusBlocksNewInProgressAsync(AssessmentStatus.Rejected, shouldBlock: false);
    }

    [IntegrationConnectionFact]
    public async Task Different_assessment_type_is_allowed_even_while_one_is_in_progress()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var (matrixId, likelihoodId, bandId) = await SeedMinimalMatrixAsync(db);
        var riskId = await SeedRiskAsync(db);

        db.Add(NewAssessment(riskId, AssessmentType.Inherent, matrixId, likelihoodId, bandId, AssessmentStatus.Draft));
        await db.SaveChangesAsync();

        db.Add(NewAssessment(riskId, AssessmentType.Current, matrixId, likelihoodId, bandId, AssessmentStatus.Draft));
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.RiskAssessments.CountAsync(a => a.RiskRecordId == riskId));
    }

    [IntegrationConnectionFact]
    public async Task Different_risk_record_is_allowed_for_the_same_assessment_type()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var (matrixId, likelihoodId, bandId) = await SeedMinimalMatrixAsync(db);
        var riskOneId = await SeedRiskAsync(db);
        var riskTwoId = await SeedRiskAsync(db);

        db.Add(NewAssessment(riskOneId, AssessmentType.Current, matrixId, likelihoodId, bandId, AssessmentStatus.Draft));
        await db.SaveChangesAsync();

        db.Add(NewAssessment(riskTwoId, AssessmentType.Current, matrixId, likelihoodId, bandId, AssessmentStatus.Draft));
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.RiskAssessments.CountAsync(a => a.RiskRecordId == riskOneId));
        Assert.Equal(1, await db.RiskAssessments.CountAsync(a => a.RiskRecordId == riskTwoId));
    }

    [IntegrationConnectionFact]
    public async Task Soft_deleted_in_progress_assessment_does_not_block_a_new_one()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var (matrixId, likelihoodId, bandId) = await SeedMinimalMatrixAsync(db);
        var riskId = await SeedRiskAsync(db);

        var deleted = NewAssessment(riskId, AssessmentType.Current, matrixId, likelihoodId, bandId, AssessmentStatus.Draft);
        deleted.IsDeleted = true;
        deleted.DeletedAtUtc = DateTimeOffset.UtcNow;
        db.Add(deleted);
        await db.SaveChangesAsync();

        db.Add(NewAssessment(riskId, AssessmentType.Current, matrixId, likelihoodId, bandId, AssessmentStatus.Draft));
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.RiskAssessments.IgnoreQueryFilters().CountAsync(a => a.RiskRecordId == riskId));
    }

    [IntegrationConnectionFact]
    public async Task Concurrent_assessment_creation_for_same_type_yields_one_success_and_one_conflict()
    {
        var barrier = new InProgressAssessmentRaceInterceptor();
        await using var scopedFactory = BaseeraApiFactory.WithInterceptor(barrier);
        await scopedFactory.SeedUserAsync("race-officer", "ضابط مخاطر", [RoleCodes.RiskOfficer], (Baseera.Domain.Common.ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var reference = await SeedRiskReferenceDirectAsync(scopedFactory);
        var riskId = await CreateRiskAsync(scopedFactory, reference.CategoryId);

        using (var scope = scopedFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var risk = await db.RiskRecords.SingleAsync(r => r.Id == riskId);
            risk.Status = RiskStatus.UnderAssessment;
            await db.SaveChangesAsync();
        }

        var payload = new
        {
            assessmentType = (int)AssessmentType.Current,
            matrixId = reference.MatrixId,
            likelihoodLevelId = reference.LikelihoodLevelId,
            impacts = new[] { new { impactDimensionId = reference.ImpactDimensionId, impactLevelId = reference.ImpactLevelId } },
            rationale = "مبرر اختباري كافٍ للتقييم."
        };

        var clientA = scopedFactory.CreateAuthenticatedClient("race-officer");
        var clientB = scopedFactory.CreateAuthenticatedClient("race-officer");

        var responses = await Task.WhenAll(
            clientA.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments", payload),
            clientB.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks/{riskId}/assessments", payload));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        using var verifyScope = scopedFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.Equal(
            1,
            await verifyDb.RiskAssessments.CountAsync(a => a.RiskRecordId == riskId
                && a.AssessmentType == AssessmentType.Current
                && (a.Status == AssessmentStatus.Draft || a.Status == AssessmentStatus.PendingReview || a.Status == AssessmentStatus.Reviewed)));
        Assert.Equal(0, await verifyDb.RiskStatusHistories.CountAsync(h => h.RiskRecordId == riskId && h.ToStatus == RiskStatus.UnderAssessment));
        Assert.Equal(1, await verifyDb.AuditLogs.CountAsync(a => a.Action == RiskAuditActions.RiskAssessmentCreated && a.EntityType == nameof(RiskAssessment)));
        var survivingAssessmentId = await verifyDb.RiskAssessments.Where(a => a.RiskRecordId == riskId).Select(a => a.Id).SingleAsync();
        Assert.Equal(1, await verifyDb.RiskAssessmentImpacts.CountAsync(i => i.RiskAssessmentId == survivingAssessmentId));
    }

    private async Task AssertStatusBlocksNewInProgressAsync(AssessmentStatus existingStatus, bool shouldBlock)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var (matrixId, likelihoodId, bandId) = await SeedMinimalMatrixAsync(db);
        var riskId = await SeedRiskAsync(db);

        db.Add(NewAssessment(riskId, AssessmentType.Current, matrixId, likelihoodId, bandId, existingStatus));
        await db.SaveChangesAsync();

        db.Add(NewAssessment(riskId, AssessmentType.Current, matrixId, likelihoodId, bandId, AssessmentStatus.Draft));

        if (shouldBlock)
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(1, await db.RiskAssessments.CountAsync(a => a.RiskRecordId == riskId));
        }
        else
        {
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.RiskAssessments.CountAsync(a => a.RiskRecordId == riskId));
        }
    }

    private static RiskAssessment NewAssessment(Guid riskId, AssessmentType assessmentType, Guid matrixId, Guid likelihoodId, Guid bandId, AssessmentStatus status) => new()
    {
        RiskRecordId = riskId,
        AssessmentType = assessmentType,
        MatrixId = matrixId,
        MatrixVersion = 1,
        LikelihoodLevelId = likelihoodId,
        CalculatedScore = 1,
        RatingBandId = bandId,
        AssessedAtUtc = DateTimeOffset.UtcNow,
        AssessedBy = "test-harness",
        Status = status,
        CreatedBy = "test-harness"
    };

    private static async Task<(Guid MatrixId, Guid LikelihoodId, Guid BandId)> SeedMinimalMatrixAsync(BaseeraDbContext db)
    {
        var matrix = new RiskAssessmentMatrix
        {
            OrganizationId = SeedIds.Organization,
            Code = $"MTX-{Guid.NewGuid():N}"[..12],
            Name = "مصفوفة تزامن",
            Version = 1,
            Status = MatrixStatus.Active,
            ScoreFormula = ScoreFormulaType.LikelihoodTimesMaximumImpact,
            EffectiveFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
            IsDefault = false
        };
        var likelihood = new LikelihoodLevel { Code = "L1", Name = "احتمالية 1", NumericValue = 1, DisplayOrder = 1 };
        matrix.LikelihoodLevels.Add(likelihood);
        var band = new RiskRatingBand { Code = "ANY", LabelAr = "أي درجة", MinimumScore = 1, MaximumScore = 25, Severity = RiskRatingSeverity.Low, ColorToken = "info" };
        matrix.RatingBands.Add(band);
        db.Add(matrix);
        await db.SaveChangesAsync();
        return (matrix.Id, likelihood.Id, band.Id);
    }

    private static async Task<Guid> SeedRiskAsync(BaseeraDbContext db)
    {
        var category = await db.RiskCategories.FirstOrDefaultAsync(c => c.OrganizationId == SeedIds.Organization && c.Code == "CONCURRENCY")
            ?? new RiskCategory { OrganizationId = SeedIds.Organization, Code = "CONCURRENCY", NameAr = "تصنيف تزامن", IsActive = true };
        if (db.Entry(category).State == EntityState.Detached)
        {
            db.Add(category);
        }

        var risk = new RiskRecord
        {
            OrganizationId = SeedIds.Organization,
            RiskCode = $"RSK-{Guid.NewGuid():N}"[..16],
            Title = "خطر اختبار تزامن",
            RiskCategoryId = category.Id,
            RiskType = RiskType.Other,
            ScopeLevel = Baseera.Domain.Common.ScopeType.Facility,
            FacilityId = SeedIds.FacilityA1,
            Status = RiskStatus.UnderAssessment,
            RecurrenceKey = $"RECUR-{Guid.NewGuid():N}",
            FirstIdentifiedAtUtc = DateTimeOffset.UtcNow
        };
        db.Add(risk);
        await db.SaveChangesAsync();
        return risk.Id;
    }

    private async Task<Guid> CreateRiskAsync(BaseeraApiFactory scopedFactory, Guid categoryId)
    {
        var client = scopedFactory.CreateAuthenticatedClient("race-officer");
        var response = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/risks", new
        {
            title = "خطر اختبار سباق التقييمات",
            riskCategoryId = categoryId,
            riskType = 0
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        return created!.Id;
    }

    private static async Task<RaceReference> SeedRiskReferenceDirectAsync(BaseeraApiFactory scopedFactory)
    {
        using var scope = scopedFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();

        var category = new RiskCategory { OrganizationId = SeedIds.Organization, Code = $"CAT-{Guid.NewGuid():N}"[..12], NameAr = "تصنيف سباق", IsActive = true };
        db.Add(category);

        var dimension = new ImpactDimension { OrganizationId = SeedIds.Organization, Code = "SEC", NameAr = "أمني", IsActive = true };
        db.Add(dimension);

        var matrix = new RiskAssessmentMatrix
        {
            OrganizationId = SeedIds.Organization,
            Code = $"MTX-{Guid.NewGuid():N}"[..12],
            Name = "مصفوفة سباق",
            Version = 1,
            Status = MatrixStatus.Active,
            ScoreFormula = ScoreFormulaType.LikelihoodTimesMaximumImpact,
            EffectiveFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
            IsDefault = true
        };
        var likelihood = new LikelihoodLevel { Code = "L1", Name = "احتمالية 1", NumericValue = 1, DisplayOrder = 1 };
        matrix.LikelihoodLevels.Add(likelihood);
        var impact = new ImpactLevel { ImpactDimensionId = dimension.Id, Code = "I1", Name = "أثر 1", NumericValue = 1, DisplayOrder = 1 };
        matrix.ImpactLevels.Add(impact);
        var band = new RiskRatingBand { Code = "ANY", LabelAr = "أي درجة", MinimumScore = 1, MaximumScore = 25, Severity = RiskRatingSeverity.Low, ColorToken = "info" };
        matrix.RatingBands.Add(band);
        db.Add(matrix);

        await db.SaveChangesAsync();

        return new RaceReference(category.Id, matrix.Id, likelihood.Id, dimension.Id, impact.Id);
    }

    private sealed record RaceReference(Guid CategoryId, Guid MatrixId, Guid LikelihoodLevelId, Guid ImpactDimensionId, Guid ImpactLevelId);

    private sealed record CreateResponse(Guid Id);

    /// <summary>
    /// Holds both concurrent CreateAsync calls at the exact moment they are about to persist their new
    /// RiskAssessment row, releasing them together so both attempt the insert at the same time — proving the
    /// UX_RiskAssessments_RiskRecordId_AssessmentType_InProgress index (not request/thread ordering) is what
    /// determines the single winner. Only the first two RiskAssessment-inserting saves participate; the
    /// interceptor is transparent to every other SaveChanges call (seeding, the audit write, etc.).
    /// </summary>
    private sealed class InProgressAssessmentRaceInterceptor : SaveChangesInterceptor
    {
        private readonly Barrier _barrier = new(2);
        private int _participants;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var isNewAssessment = eventData.Context?.ChangeTracker.Entries<RiskAssessment>().Any(e => e.State == EntityState.Added) == true;
            if (isNewAssessment && Interlocked.Increment(ref _participants) <= 2)
            {
                await Task.Run(() => _barrier.SignalAndWait(TimeSpan.FromSeconds(15)), cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
