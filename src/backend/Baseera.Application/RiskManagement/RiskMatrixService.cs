namespace Baseera.Application.RiskManagement;

using System.Text.Json;
using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Matrix lifecycle: Draft (created + editable) -> PendingApproval (validated and signed off via ApproveAsync,
/// four-eyes vs creator) -> Active (put into effect via ActivateAsync, retiring the previous default matrix)
/// -> Retired. Once a matrix leaves Draft its Likelihood/Impact/RatingBand rows are never edited again — a
/// correction always means creating a new Draft matrix with PreviousVersionMatrixId set.
/// </summary>
public sealed class RiskMatrixService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskMatrixService
{
    public async Task<IReadOnlyList<RiskMatrixDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksView);
        var matrices = await Db.RiskAssessmentMatrices.AsNoTracking()
            .Include(m => m.LikelihoodLevels)
            .Include(m => m.ImpactLevels).ThenInclude(l => l.ImpactDimension)
            .Include(m => m.RatingBands)
            .Where(m => m.OrganizationId == organizationId)
            .OrderByDescending(m => m.Code).ThenByDescending(m => m.Version)
            .ToListAsync(cancellationToken);

        return matrices.Select(Map).ToList();
    }

    private static RiskMatrixDto Map(RiskAssessmentMatrix m) => new(
        m.Id, m.Code, m.Name, m.Version, m.Status, m.ScoreFormula, m.EffectiveFromUtc, m.EffectiveToUtc, m.IsDefault,
        m.LikelihoodLevels.OrderBy(l => l.DisplayOrder)
            .Select(l => new RiskLikelihoodLevelDto(l.Id, l.Code, l.Name, l.NumericValue, l.Description)).ToList(),
        m.ImpactLevels.OrderBy(l => l.DisplayOrder)
            .Select(l => new RiskImpactLevelDto(l.Id, l.ImpactDimensionId, l.ImpactDimension.NameAr, l.Code, l.Name, l.NumericValue)).ToList(),
        m.RatingBands.OrderBy(b => b.MinimumScore)
            .Select(b => new RiskRatingBandDto(b.Id, b.Code, b.LabelAr, b.MinimumScore, b.MaximumScore, b.Severity, b.ResponseTimeHours, b.EscalationRequired, b.ReviewFrequencyDays, b.ColorToken)).ToList(),
        Convert.ToBase64String(m.RowVersion));

    public async Task<Guid> CreateAsync(Guid organizationId, RiskMatrixCreateRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksManageMatrices);

        var bands = request.RatingBands.Select(b => new RiskRatingBand
        {
            Code = b.Code,
            LabelAr = b.LabelAr,
            MinimumScore = b.MinimumScore,
            MaximumScore = b.MaximumScore,
            Severity = b.Severity,
            ResponseTimeHours = b.ResponseTimeHours,
            EscalationRequired = b.EscalationRequired,
            ReviewFrequencyDays = b.ReviewFrequencyDays,
            ColorToken = b.ColorToken,
            CreatedBy = ActorReference()
        }).ToList();
        RiskMatrixValidation.ValidateRatingBands(bands);

        if (request.LikelihoodLevels.Count == 0 || request.ImpactLevels.Count == 0)
        {
            throw new InvalidOperationException("يجب تعريف مستوى احتمالية واحد على الأقل ومستوى أثر واحد على الأقل لكل بُعد.");
        }

        var dimensionIds = request.ImpactLevels.Select(l => l.ImpactDimensionId).Distinct().ToList();
        var validDimensionCount = await Db.ImpactDimensions.CountAsync(d => d.OrganizationId == organizationId && dimensionIds.Contains(d.Id), cancellationToken);
        if (validDimensionCount != dimensionIds.Count)
        {
            throw new InvalidOperationException("أحد أبعاد الأثر المحددة غير موجود ضمن المنظمة.");
        }

        string? weightsJson = null;
        if (request.ScoreFormula == ScoreFormulaType.LikelihoodTimesWeightedImpact)
        {
            if (request.ImpactDimensionWeights is null)
            {
                throw new InvalidOperationException("أوزان الأبعاد مطلوبة لصيغة الأثر الموزون.");
            }

            RiskMatrixValidation.ValidateWeights(request.ImpactDimensionWeights, dimensionIds);
            weightsJson = JsonSerializer.Serialize(request.ImpactDimensionWeights);
        }

        var maxVersion = await Db.RiskAssessmentMatrices
            .Where(m => m.OrganizationId == organizationId && m.Code == request.Code)
            .Select(m => (int?)m.Version)
            .MaxAsync(cancellationToken) ?? 0;

        var matrix = new RiskAssessmentMatrix
        {
            OrganizationId = organizationId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Version = maxVersion + 1,
            Status = MatrixStatus.Draft,
            ScoreFormula = request.ScoreFormula,
            ImpactWeightingJson = weightsJson,
            EffectiveFromUtc = request.EffectiveFromUtc,
            IsDefault = request.IsDefault,
            PreviousVersionMatrixId = request.PreviousVersionMatrixId,
            CreatedBy = ActorReference()
        };

        foreach (var level in request.LikelihoodLevels.Select((l, index) => (l, index)))
        {
            matrix.LikelihoodLevels.Add(new LikelihoodLevel
            {
                Code = level.l.Code,
                Name = level.l.Name,
                NumericValue = level.l.NumericValue,
                Description = level.l.Description,
                Criteria = level.l.Criteria,
                DisplayOrder = level.index,
                CreatedBy = ActorReference()
            });
        }

        foreach (var level in request.ImpactLevels.Select((l, index) => (l, index)))
        {
            matrix.ImpactLevels.Add(new ImpactLevel
            {
                ImpactDimensionId = level.l.ImpactDimensionId,
                Code = level.l.Code,
                Name = level.l.Name,
                NumericValue = level.l.NumericValue,
                Description = level.l.Description,
                Criteria = level.l.Criteria,
                DisplayOrder = level.index,
                CreatedBy = ActorReference()
            });
        }

        foreach (var band in bands)
        {
            matrix.RatingBands.Add(band);
        }

        Db.Add(matrix);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskMatrixCreated, nameof(RiskAssessmentMatrix), matrix.Id, new { matrix.Code, matrix.Version }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return matrix.Id;
    }

    public async Task ApproveAsync(Guid organizationId, Guid matrixId, RiskRowVersionRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksApproveMatrices);
        var matrix = await LoadOrgMatrixAsync(organizationId, matrixId, cancellationToken);
        EnsureCurrentRowVersion(matrix, request.RowVersion);
        EnforceFourEyes(matrix.CreatedBy ?? string.Empty);

        if (matrix.Status != MatrixStatus.Draft)
        {
            throw new InvalidOperationException("لا يمكن اعتماد إلا مصفوفة في حالة مسودة.");
        }

        RiskMatrixValidation.ValidateRatingBands(matrix.RatingBands.ToList());

        matrix.Status = MatrixStatus.PendingApproval;
        matrix.ApprovedBy = ActorReference();
        matrix.ApprovedAtUtc = DateTimeOffset.UtcNow;
        Db.Update(matrix);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskMatrixApproved, nameof(RiskAssessmentMatrix), matrix.Id, new { matrix.Code, matrix.Version }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    public async Task ActivateAsync(Guid organizationId, Guid matrixId, RiskRowVersionRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksApproveMatrices);
        var matrix = await LoadOrgMatrixAsync(organizationId, matrixId, cancellationToken);
        EnsureCurrentRowVersion(matrix, request.RowVersion);

        if (matrix.Status != MatrixStatus.PendingApproval)
        {
            throw new InvalidOperationException("لا يمكن تفعيل إلا مصفوفة معتمدة بانتظار التفعيل.");
        }

        if (matrix.IsDefault)
        {
            var currentDefault = await Db.RiskAssessmentMatrices
                .Where(m => m.OrganizationId == organizationId && m.IsDefault && m.Status == MatrixStatus.Active && m.Id != matrixId)
                .ToListAsync(cancellationToken);
            foreach (var previous in currentDefault)
            {
                previous.Status = MatrixStatus.Retired;
                previous.EffectiveToUtc = DateTimeOffset.UtcNow;
                previous.IsDefault = false;
                Db.Update(previous);
            }
        }

        matrix.Status = MatrixStatus.Active;
        Db.Update(matrix);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskMatrixActivated, nameof(RiskAssessmentMatrix), matrix.Id, new { matrix.Code, matrix.Version }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    private async Task<RiskAssessmentMatrix> LoadOrgMatrixAsync(Guid organizationId, Guid matrixId, CancellationToken cancellationToken)
    {
        return await Db.RiskAssessmentMatrices
            .Include(m => m.RatingBands)
            .FirstOrDefaultAsync(m => m.Id == matrixId && m.OrganizationId == organizationId, cancellationToken)
            ?? throw new KeyNotFoundException("مصفوفة التقييم غير موجودة.");
    }
}
