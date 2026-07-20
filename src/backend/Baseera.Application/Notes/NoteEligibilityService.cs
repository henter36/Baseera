namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public interface INoteEligibilityService
{
    Task<IReadOnlyList<EligibleUserDto>> GetEligibleAssigneesAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EligibleUserDto>> GetEligibleReviewersAsync(Guid noteId, CancellationToken cancellationToken = default);
}

public sealed class NoteEligibilityService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    INoteTypeAccessService typeAccess) : INoteEligibilityService
{
    public Task<IReadOnlyList<EligibleUserDto>> GetEligibleAssigneesAsync(Guid noteId, CancellationToken cancellationToken = default) =>
        GetEligibleUsersAsync(noteId, PermissionCodes.NotesStartWork, NoteTypeCapability.Process, enforceSoD: false, cancellationToken);

    public Task<IReadOnlyList<EligibleUserDto>> GetEligibleReviewersAsync(Guid noteId, CancellationToken cancellationToken = default) =>
        GetEligibleUsersAsync(noteId, PermissionCodes.NotesVerifyClosure, NoteTypeCapability.Review, enforceSoD: true, cancellationToken);

    private async Task<IReadOnlyList<EligibleUserDto>> GetEligibleUsersAsync(
        Guid noteId,
        string permission,
        NoteTypeCapability capability,
        bool enforceSoD,
        CancellationToken cancellationToken)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesView);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        if (!await typeAccess.CanViewAsync(note.NoteTypeId, cancellationToken))
        {
            throw new KeyNotFoundException("الملاحظة غير موجودة أو خارج نطاقك.");
        }

        var candidates = await db.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.ProvisioningStatus == UserProvisioningStatus.Active)
            .Where(user => db.UserRoles.Any(ur => ur.UserId == user.Id &&
                db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId && rp.Permission.Code == permission)))
            .OrderBy(user => user.DisplayNameAr)
            .ToListAsync(cancellationToken);
        var candidateIds = candidates.Select(user => user.Id).ToList();
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var scopes = await db.UserScopes
            .AsNoTracking()
            .Where(scope => candidateIds.Contains(scope.UserId) && scope.IsActive)
            .ToListAsync(cancellationToken);
        var scopesByUser = scopes.ToLookup(scope => scope.UserId);
        var accessByUser = await typeAccess.GetEffectiveAccessForUsersAsync(candidateIds, note.NoteTypeId, cancellationToken);

        HashSet<Guid> blocked = enforceSoD && note.Severity == NoteSeverity.Critical
            ? await ProcessingParticipantIdsAsync(note.Id, cancellationToken)
            : new HashSet<Guid>();
        var result = new List<EligibleUserDto>();
        foreach (var user in candidates)
        {
            if (blocked.Contains(user.Id))
            {
                continue;
            }

            if (!IntersectsAny(scopesByUser[user.Id], note))
            {
                continue;
            }

            accessByUser.TryGetValue(user.Id, out var access);
            if (CapabilityAllowed(access, capability))
            {
                result.Add(new EligibleUserDto(user.Id, user.DisplayNameAr, user.UserName));
            }
        }

        return result;
    }

    private async Task<HashSet<Guid>> ProcessingParticipantIdsAsync(Guid noteId, CancellationToken cancellationToken)
    {
        return await db.NoteStatusHistories
            .Where(history =>
                history.OperationalNoteId == noteId &&
                (
                    (history.FromStatus == NoteStatus.Assigned && history.ToStatus == NoteStatus.InProgress) ||
                    (history.FromStatus == NoteStatus.Reopened && history.ToStatus == NoteStatus.InProgress) ||
                    (history.FromStatus == NoteStatus.InProgress && history.ToStatus == NoteStatus.PendingVerification)
                ))
            .Select(history => history.ChangedByUserId)
            .ToHashSetAsync(cancellationToken);
    }

    private static bool IntersectsAny(IEnumerable<UserScope> scopes, OperationalNote note) =>
        scopes.Any(scope => Intersects(scope, note));

    private static bool Intersects(UserScope scope, OperationalNote note)
    {
        return scope.ScopeType switch
        {
            Domain.Common.ScopeType.Global => true,
            Domain.Common.ScopeType.Headquarters => note.ScopeType == Domain.Common.ScopeType.Headquarters,
            Domain.Common.ScopeType.Region => note.RegionId == scope.RegionId,
            Domain.Common.ScopeType.Facility => note.FacilityId == scope.FacilityId,
            Domain.Common.ScopeType.FacilityUnit => note.FacilityUnitId == scope.FacilityUnitId || (!note.FacilityUnitId.HasValue && note.FacilityId == scope.FacilityId),
            _ => false
        };
    }

    private static bool CapabilityAllowed(EffectiveNoteTypeAccessDto? access, NoteTypeCapability capability) => capability switch
    {
        NoteTypeCapability.Process => access?.Process.Allowed == true,
        NoteTypeCapability.Review => access?.Review.Allowed == true,
        _ => access?.View.Allowed == true
    };
}
