namespace Baseera.Application.Forms.Campaigns;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;
using Baseera.Domain.Organization;
using Microsoft.EntityFrameworkCore;

public interface IFormTargetResolver
{
    Task<FormTargetResolutionResult> ResolveAsync(
        Guid organizationId,
        IReadOnlyList<FormCampaignTargetRequest> targets,
        IReadOnlyList<FormCampaignExclusionRequest> exclusions,
        CancellationToken cancellationToken = default);
}

public sealed class FormTargetResolver(
    IBaseeraDbContext db,
    IOrganizationalScopeService scope) : IFormTargetResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<FormTargetResolutionResult> ResolveAsync(
        Guid organizationId,
        IReadOnlyList<FormCampaignTargetRequest> targets,
        IReadOnlyList<FormCampaignExclusionRequest> exclusions,
        CancellationToken cancellationToken = default)
    {
        if (targets is null || targets.Count == 0)
        {
            throw new InvalidOperationException("يجب تحديد قاعدة استهداف واحدة على الأقل.");
        }

        var warnings = new List<string>();
        var invalid = new List<string>();
        var matched = new Dictionary<Guid, ResolvedFacilityTarget>();

        foreach (var rule in targets)
        {
            var query = scope.FilterFacilities(
                db.Facilities.AsNoTracking().Where(f => f.Region.OrganizationId == organizationId && f.IsActive));

            query = rule.RuleType switch
            {
                FormTargetRuleType.AllFacilities => query,
                FormTargetRuleType.Regions => ApplyRegions(query, rule.RegionIds, invalid),
                FormTargetRuleType.Facilities => ApplyFacilities(query, rule.FacilityIds, invalid),
                FormTargetRuleType.DynamicCriteria => ApplyDynamic(query, rule.DynamicCriteria, invalid),
                _ => throw new InvalidOperationException("نوع قاعدة الاستهداف غير مدعوم.")
            };

            var rows = await query
                .Select(f => new
                {
                    f.Id,
                    f.RegionId,
                    f.Code,
                    f.NameAr,
                    f.FacilityType,
                    RegionNameAr = f.Region.NameAr,
                    f.IsActive
                })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                matched[row.Id] = new ResolvedFacilityTarget(
                    row.Id,
                    row.RegionId,
                    row.Code,
                    row.NameAr,
                    row.RegionNameAr,
                    row.FacilityType,
                    rule.RuleType,
                    row.IsActive,
                    row.IsActive ? null : "الموقع غير نشط");
            }
        }

        var exclusionMap = (exclusions ?? [])
            .GroupBy(e => e.FacilityId)
            .ToDictionary(g => g.Key, g => g.First().Reason?.Trim() ?? string.Empty);

        foreach (var (facilityId, reason) in exclusionMap)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException("سبب الاستثناء مطلوب.");
            }
        }

        var excluded = new List<(Guid FacilityId, string Reason)>();
        foreach (var facilityId in exclusionMap.Keys)
        {
            if (matched.Remove(facilityId, out _))
            {
                excluded.Add((facilityId, exclusionMap[facilityId]));
            }
            else
            {
                warnings.Add($"استثناء لموقع غير مدرج: {facilityId}");
                excluded.Add((facilityId, exclusionMap[facilityId]));
            }
        }

        var included = matched.Values.OrderBy(x => x.FacilityCode).ToList();
        var byRegion = included.GroupBy(x => x.RegionNameAr).ToDictionary(g => g.Key, g => g.Count());
        var byType = included
            .GroupBy(x => string.IsNullOrWhiteSpace(x.FacilityType) ? "غير محدد" : x.FacilityType!)
            .ToDictionary(g => g.Key, g => g.Count());
        var fingerprint = FormTargetSnapshotHasher.HashFacilityIds(included.Select(x => x.FacilityId));

        return new FormTargetResolutionResult(
            included,
            excluded,
            byRegion,
            byType,
            fingerprint,
            warnings,
            invalid.Distinct().ToList());
    }

    private static IQueryable<Facility> ApplyRegions(
        IQueryable<Facility> query,
        IReadOnlyList<Guid>? regionIds,
        List<string> invalid)
    {
        if (regionIds is null || regionIds.Count == 0)
        {
            invalid.Add("قائمة المناطق فارغة.");
            return query.Where(_ => false);
        }

        var ids = regionIds.Distinct().ToArray();
        return query.Where(f => ids.Contains(f.RegionId));
    }

    private static IQueryable<Facility> ApplyFacilities(
        IQueryable<Facility> query,
        IReadOnlyList<Guid>? facilityIds,
        List<string> invalid)
    {
        if (facilityIds is null || facilityIds.Count == 0)
        {
            invalid.Add("قائمة المواقع فارغة.");
            return query.Where(_ => false);
        }

        var ids = facilityIds.Distinct().ToArray();
        return query.Where(f => ids.Contains(f.Id));
    }

    private static IQueryable<Facility> ApplyDynamic(
        IQueryable<Facility> query,
        DynamicCriteriaRequest? criteria,
        List<string> invalid)
    {
        if (criteria is null)
        {
            invalid.Add("معايير ديناميكية مطلوبة.");
            return query.Where(_ => false);
        }

        if (criteria.RegionIds is { Count: > 0 } regionIds)
        {
            var ids = regionIds.Distinct().ToArray();
            query = query.Where(f => ids.Contains(f.RegionId));
        }

        if (criteria.FacilityTypes is { Count: > 0 } types)
        {
            var allowed = types
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (allowed.Length == 0)
            {
                invalid.Add("أنواع المواقع غير صالحة.");
                return query.Where(_ => false);
            }

            query = query.Where(f => f.FacilityType != null && allowed.Contains(f.FacilityType));
        }

        if (criteria.IsActive is { } isActive)
        {
            query = query.Where(f => f.IsActive == isActive);
        }

        return query;
    }

    public static string SerializeTarget(FormCampaignTargetRequest target) =>
        JsonSerializer.Serialize(target, JsonOptions);

    public static FormCampaignTargetRequest DeserializeTarget(FormTargetRuleType type, string json)
    {
        var parsed = JsonSerializer.Deserialize<FormCampaignTargetRequest>(json, JsonOptions)
            ?? new FormCampaignTargetRequest(type, null, null, null);
        return parsed with { RuleType = type };
    }
}

public static class FormTargetSnapshotHasher
{
    public static string HashFacilityIds(IEnumerable<Guid> facilityIds)
    {
        var payload = string.Join('\n', facilityIds.Distinct().OrderBy(x => x).Select(x => x.ToString("N")));
        return Sha256(payload);
    }

    public static string HashAssignments(IEnumerable<ResolvedFacilityTarget> targets)
    {
        var lines = targets
            .OrderBy(t => t.FacilityId)
            .Select(t =>
                $"{t.FacilityId:N}|{t.RegionId:N}|{t.FacilityCode}|{t.FacilityNameAr}|{t.RegionNameAr}|{t.FacilityType}");
        return Sha256(string.Join('\n', lines));
    }

    private static string Sha256(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
