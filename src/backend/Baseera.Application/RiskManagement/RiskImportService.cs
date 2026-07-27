namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Upload -> validate -> preview -> confirm, idempotent on (facility, ImportKind.RiskRecords, FileHash) —
/// the same shape as ISensitiveCustodyImportService/IResourceImportService. Rows never carry a
/// client-supplied score: Confirm creates each valid risk plus a Draft Inherent assessment whose score is
/// computed by RiskScoringEngine exactly as a manually-created assessment would be, and that assessment
/// still requires the normal submit/review/approve cycle before it becomes authoritative.
/// </summary>
public sealed class RiskImportService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskImportService
{
    public async Task<RiskImportResult> PreviewAsync(Guid facilityId, RiskImportPreviewRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksImport);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var (rowResults, _, _) = await ValidateRowsAsync(facility.Region.OrganizationId, facilityId, request, cancellationToken);

        var batch = await FindOrCreateBatchAsync(facility.Region.OrganizationId, facilityId, request, cancellationToken);
        batch.TotalRows = request.Rows.Count;
        batch.ValidRows = rowResults.Count(r => r.IsValid && !r.IsDuplicate);
        batch.RejectedRows = rowResults.Count(r => !r.IsValid);
        batch.DuplicateRows = rowResults.Count(r => r.IsDuplicate);
        // batch is already tracked (freshly Added by FindOrCreateBatchAsync, or Unchanged if reused) —
        // calling Db.Update() here would force a brand-new Added entity into Modified state and EF would
        // emit an UPDATE instead of an INSERT, failing with a spurious concurrency exception.
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskImportPreviewed, nameof(RiskImportBatch), batch.Id, new { batch.TotalRows, batch.ValidRows }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);

        return new RiskImportResult(batch.Id, batch.TotalRows, batch.ValidRows, batch.RejectedRows, batch.DuplicateRows, 0, rowResults);
    }

    public async Task<RiskImportResult> ConfirmAsync(Guid facilityId, RiskImportPreviewRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksImport);
        var facility = await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var organizationId = facility.Region.OrganizationId;

        var existing = await Db.RiskImportBatches.FirstOrDefaultAsync(b =>
            b.FacilityId == facilityId && b.ImportKind == RiskImportKind.RiskRecords
            && b.SourceSystem == request.SourceSystem && b.SourceReference == request.SourceReference && b.FileHash == request.FileHash,
            cancellationToken);
        if (existing is { Status: RiskImportStatuses.Confirmed })
        {
            // Idempotent replay: return the previously recorded outcome without re-applying anything.
            return new RiskImportResult(existing.Id, existing.TotalRows, existing.ValidRows, existing.RejectedRows, existing.DuplicateRows, existing.AppliedRows, []);
        }

        var (rowResults, categoryLookup, matrixLookup) = await ValidateRowsAsync(organizationId, facilityId, request, cancellationToken);
        var batch = existing ?? await FindOrCreateBatchAsync(organizationId, facilityId, request, cancellationToken);

        var appliedCount = 0;
        foreach (var row in request.Rows)
        {
            var result = rowResults.First(r => r.RowKey == row.RowKey);
            if (!result.IsValid || result.IsDuplicate)
            {
                continue;
            }

            await CreateRiskFromRowAsync(organizationId, facilityId, row, categoryLookup[row.CategoryCode], matrixLookup[row.MatrixId], cancellationToken);
            appliedCount++;
        }

        batch.TotalRows = request.Rows.Count;
        batch.ValidRows = rowResults.Count(r => r.IsValid && !r.IsDuplicate);
        batch.RejectedRows = rowResults.Count(r => !r.IsValid);
        batch.DuplicateRows = rowResults.Count(r => r.IsDuplicate);
        batch.AppliedRows = appliedCount;
        batch.Status = RiskImportStatuses.Confirmed;
        batch.ConfirmedAtUtc = DateTimeOffset.UtcNow;
        // batch is already tracked (see PreviewAsync) — no explicit Db.Update() needed or safe to call.
        try
        {
            await Db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (existing is null)
        {
            // A concurrent request confirmed the same (FacilityId, ImportKind, FileHash) batch first —
            // the unique index rejected our insert. Replay idempotently instead of surfacing a spurious
            // conflict or double-creating the risk records this request already added.
            var winner = await Db.RiskImportBatches.AsNoTracking().FirstAsync(b =>
                b.FacilityId == facilityId && b.ImportKind == RiskImportKind.RiskRecords && b.FileHash == request.FileHash,
                cancellationToken);
            return new RiskImportResult(winner.Id, winner.TotalRows, winner.ValidRows, winner.RejectedRows, winner.DuplicateRows, winner.AppliedRows, []);
        }

        await AuditAsync(RiskAuditActions.RiskImportConfirmed, nameof(RiskImportBatch), batch.Id, new { batch.AppliedRows }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);

        return new RiskImportResult(batch.Id, batch.TotalRows, batch.ValidRows, batch.RejectedRows, batch.DuplicateRows, batch.AppliedRows, rowResults);
    }

    private async Task<RiskImportBatch> FindOrCreateBatchAsync(Guid organizationId, Guid facilityId, RiskImportPreviewRequest request, CancellationToken cancellationToken)
    {
        var batch = await Db.RiskImportBatches.FirstOrDefaultAsync(b =>
            b.FacilityId == facilityId && b.ImportKind == RiskImportKind.RiskRecords
            && b.SourceSystem == request.SourceSystem && b.SourceReference == request.SourceReference && b.FileHash == request.FileHash,
            cancellationToken);
        if (batch is not null)
        {
            return batch;
        }

        batch = new RiskImportBatch
        {
            OrganizationId = organizationId,
            FacilityId = facilityId,
            ImportKind = RiskImportKind.RiskRecords,
            SourceSystem = request.SourceSystem,
            SourceReference = request.SourceReference,
            FileHash = request.FileHash,
            Status = RiskImportStatuses.Previewed,
            CreatedBy = ActorReference()
        };
        Db.Add(batch);
        return batch;
    }

    private async Task<(List<RiskImportRowResult> Results, Dictionary<string, RiskCategory> CategoryLookup, Dictionary<Guid, RiskAssessmentMatrix> MatrixLookup)> ValidateRowsAsync(
        Guid organizationId, Guid facilityId, RiskImportPreviewRequest request, CancellationToken cancellationToken)
    {
        var categories = await Db.RiskCategories.Where(c => c.OrganizationId == organizationId).ToDictionaryAsync(c => c.Code, cancellationToken);
        var matrices = await LoadMatricesAsync(organizationId, request.Rows, cancellationToken);
        var existingTitles = await LoadExistingTitleKeysAsync(facilityId, cancellationToken);

        var seenInBatch = new HashSet<string>();
        var results = request.Rows
            .Select(row => ValidateRow(row, categories, matrices, existingTitles, seenInBatch))
            .ToList();

        return (results, categories, matrices);
    }

    private async Task<Dictionary<Guid, RiskAssessmentMatrix>> LoadMatricesAsync(Guid organizationId, IReadOnlyList<RiskImportRow> rows, CancellationToken cancellationToken)
    {
        var matrixIds = rows.Select(r => r.MatrixId).Distinct().ToList();
        return await Db.RiskAssessmentMatrices
            .Include(m => m.LikelihoodLevels)
            .Include(m => m.ImpactLevels).ThenInclude(l => l.ImpactDimension)
            .Include(m => m.RatingBands)
            .Where(m => matrixIds.Contains(m.Id) && m.OrganizationId == organizationId)
            .ToDictionaryAsync(m => m.Id, cancellationToken);
    }

    private async Task<HashSet<(string TitleKey, Guid CategoryId)>> LoadExistingTitleKeysAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        var existing = await Db.RiskRecords.AsNoTracking()
            .Where(r => r.FacilityId == facilityId)
            .Select(r => new { r.Title, r.RiskCategoryId })
            .ToListAsync(cancellationToken);
        return existing.Select(r => (r.Title.Trim().ToUpperInvariant(), r.RiskCategoryId)).ToHashSet();
    }

    private static RiskImportRowResult ValidateRow(
        RiskImportRow row,
        Dictionary<string, RiskCategory> categories,
        Dictionary<Guid, RiskAssessmentMatrix> matrices,
        HashSet<(string TitleKey, Guid CategoryId)> existingTitles,
        HashSet<string> seenInBatch)
    {
        var errors = new List<string>();
        categories.TryGetValue(row.CategoryCode, out var category);
        if (category is null)
        {
            errors.Add("رمز تصنيف الخطر غير موجود.");
        }

        ValidateMatrixAndImpacts(row, matrices, errors);

        if (string.IsNullOrWhiteSpace(row.Title))
        {
            errors.Add("عنوان الخطر مطلوب.");
        }

        var isDuplicate = IsDuplicateRow(row, category, existingTitles, seenInBatch);
        return new RiskImportRowResult(row.RowKey, errors.Count == 0, isDuplicate, errors);
    }

    private static void ValidateMatrixAndImpacts(RiskImportRow row, Dictionary<Guid, RiskAssessmentMatrix> matrices, List<string> errors)
    {
        if (!matrices.TryGetValue(row.MatrixId, out var matrix) || matrix.Status != MatrixStatus.Active)
        {
            errors.Add("مصفوفة التقييم غير موجودة أو غير نشطة.");
            return;
        }

        if (!matrix.LikelihoodLevels.Any(l => l.Code == row.LikelihoodCode))
        {
            errors.Add("رمز مستوى الاحتمالية غير موجود ضمن المصفوفة.");
        }

        foreach (var (dimensionCode, impactCode) in row.ImpactCodesByDimensionCode)
        {
            if (!matrix.ImpactLevels.Any(l => l.ImpactDimension.Code == dimensionCode && l.Code == impactCode))
            {
                errors.Add($"رمز مستوى الأثر '{impactCode}' غير صالح للبُعد '{dimensionCode}'.");
            }
        }

        if (row.ImpactCodesByDimensionCode.Count == 0)
        {
            errors.Add("يلزم تقييم بُعد أثر واحد على الأقل.");
        }
    }

    private static bool IsDuplicateRow(RiskImportRow row, RiskCategory? category, HashSet<(string TitleKey, Guid CategoryId)> existingTitles, HashSet<string> seenInBatch)
    {
        if (category is null || string.IsNullOrWhiteSpace(row.Title))
        {
            return false;
        }

        var titleKey = row.Title.Trim().ToUpperInvariant();
        return existingTitles.Contains((titleKey, category.Id)) || !seenInBatch.Add($"{category.Id}|{titleKey}");
    }

    private async Task CreateRiskFromRowAsync(Guid organizationId, Guid facilityId, RiskImportRow row, RiskCategory category, RiskAssessmentMatrix matrix, CancellationToken cancellationToken)
    {
        var likelihood = matrix.LikelihoodLevels.First(l => l.Code == row.LikelihoodCode);
        var impacts = row.ImpactCodesByDimensionCode
            .Select(kv => matrix.ImpactLevels.First(l => l.ImpactDimension.Code == kv.Key && l.Code == kv.Value))
            .ToList();

        var score = RiskScoringEngine.CalculateScore(matrix.ScoreFormula, likelihood.NumericValue,
            impacts.Select(i => new RiskImpactInput(i.ImpactDimensionId, i.NumericValue)).ToList());
        var band = RiskScoringEngine.SelectRatingBand(matrix.RatingBands.ToList(), score);

        var sequence = await Db.NextRiskRecordSequenceValueAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var risk = new RiskRecord
        {
            OrganizationId = organizationId,
            RiskCode = $"RSK-{sequence:00000000}",
            Title = row.Title.Trim(),
            Description = row.Description,
            RiskCategoryId = category.Id,
            RiskType = row.RiskType,
            ScopeLevel = ScopeType.Facility,
            FacilityId = facilityId,
            Status = RiskStatus.UnderAssessment,
            SourceType = RiskOriginType.Import,
            SourceReference = row.RowKey,
            FirstIdentifiedAtUtc = now,
            DataFreshAsOfUtc = now,
            RecurrenceKey = RiskRecurrenceKeyBuilder.Build(category.Code, facilityId, row.Title),
            CreatedBy = ActorReference()
        };
        Db.Add(risk);

        var assessment = new RiskAssessment
        {
            RiskRecordId = risk.Id,
            AssessmentType = AssessmentType.Inherent,
            MatrixId = matrix.Id,
            MatrixVersion = matrix.Version,
            LikelihoodLevelId = likelihood.Id,
            OverallImpactLevelId = impacts.OrderByDescending(i => i.NumericValue).First().Id,
            CalculatedScore = score,
            RatingBandId = band.Id,
            Rationale = "مستورد من مصدر خارجي — بانتظار المراجعة والاعتماد.",
            AssessedAtUtc = now,
            AssessedBy = ActorReference(),
            Status = AssessmentStatus.Draft,
            CreatedBy = ActorReference()
        };
        foreach (var level in impacts)
        {
            assessment.ImpactBreakdown.Add(new RiskAssessmentImpact
            {
                ImpactDimensionId = level.ImpactDimensionId,
                ImpactLevelId = level.Id,
                CreatedBy = ActorReference()
            });
        }

        // The assessment stays Draft — RiskRecord.CurrentInherentAssessmentId is only ever set on approval
        // (IRiskAssessmentService.ApproveAsync), so an imported risk never appears to have a "current" score
        // until a human runs it through the normal submit/review/approve cycle.
        Db.Add(assessment);
    }
}
