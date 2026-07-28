using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests;

/// <summary>
/// NoteCommandService.CreateDraftAsync's facility/unit scope inheritance and its rejection of a
/// client-supplied scope the caller isn't authorized for (Phase 1A #143 requirement: "الخادم يشتق أو
/// يتحقق من FacilityId من Route/authorized context؛ أي FacilityId يرسله العميل يعتبر Presentation state").
/// </summary>
public sealed class NoteCommandServiceFacilityInheritanceTests : IDisposable
{
    private static readonly Guid RegionA = Guid.NewGuid();
    private static readonly Guid FacilityA1 = Guid.NewGuid();
    private static readonly Guid UnitA1East = Guid.NewGuid();
    private static readonly Guid RegionB = Guid.NewGuid();
    private static readonly Guid FacilityB1 = Guid.NewGuid();
    private static readonly Guid UnitB1North = Guid.NewGuid();

    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public NoteCommandServiceFacilityInheritanceTests()
    {
        _db.Regions.Add(new Region { Id = RegionA, OrganizationId = Guid.NewGuid(), Code = "A", NameAr = "أ", IsActive = true });
        _db.Regions.Add(new Region { Id = RegionB, OrganizationId = Guid.NewGuid(), Code = "B", NameAr = "ب", IsActive = true });
        _db.Facilities.Add(new Facility { Id = FacilityA1, RegionId = RegionA, Code = "A1", NameAr = "أ1", IsActive = true });
        _db.Facilities.Add(new Facility { Id = FacilityB1, RegionId = RegionB, Code = "B1", NameAr = "ب1", IsActive = true });
        _db.FacilityUnits.Add(new FacilityUnit { Id = UnitA1East, FacilityId = FacilityA1, Code = "E", NameAr = "شرق", IsActive = true });
        _db.FacilityUnits.Add(new FacilityUnit { Id = UnitB1North, FacilityId = FacilityB1, Code = "N", NameAr = "شمال", IsActive = true });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private (INoteCommandService Commands, Guid ReporterId) BuildService(params UserScopeSnapshot[] scopes)
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        NoteTestFixtures.GrantPermissions(_db, reporter.Id, "Reporter", PermissionCodes.NotesCreate);
        var current = new FakeCurrentUser(true, reporter.Id, reporter.Id.ToString(), "reporter", [PermissionCodes.NotesCreate], scopes);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var typeAccess = new NoteTypeAccessService(_db, current);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        var queries = new NoteQueryService(_db, current, scope, typeAccess, audit);
        var routing = new NoteRoutingService(_db, current, scope, typeAccess, audit, TimeProvider.System);
        return (new NoteCommandService(_db, current, scope, typeAccess, routing, audit, queries), reporter.Id);
    }

    private static CreateNoteRequest Request(Guid regionId, Guid facilityId, Guid? facilityUnitId = null) => new(
        Title: "ملاحظة تجريبية",
        Description: "وصف تجريبي كافٍ للاختبار",
        NoteTypeId: NoteTestFixtures.DefaultNoteTypeId,
        Severity: NoteSeverity.Medium,
        SourceType: NoteSourceType.Manual,
        SourceReference: null,
        Classification: Baseera.Domain.Attachments.ClassificationLevel.Internal,
        ScopeType: ScopeType.Facility,
        RegionId: regionId,
        FacilityId: facilityId,
        FacilityUnitId: facilityUnitId,
        OwnerDepartmentId: null,
        DueAtUtc: null);

    // The success paths (note actually persisted with the expected ScopeType/FacilityUnitId) are covered
    // by RiskAssessment-style integration tests against real SQL Server, not here: CreateDraftAsync calls
    // BaseeraDbContext.NextOperationalNoteSequenceValueAsync (a raw SQL sequence), which the InMemory EF
    // provider used by these unit tests does not support at all — see
    // NoteWorkspaceFacilityInheritanceIntegrationTests for the full round-trip coverage. Only the
    // rejection paths below run before that sequence call is reached, so they're safe to assert here.

    [Fact]
    public async Task Create_rejects_a_unit_that_belongs_to_a_different_facility()
    {
        var (commands, _) = BuildService(new UserScopeSnapshot(ScopeType.Facility, RegionA, FacilityA1, null));

        // UnitB1North exists and is active, but under FacilityB1 — requesting it while targeting
        // FacilityA1 must fail the facility/unit consistency check, not silently attach to the wrong unit.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => commands.CreateDraftAsync(Request(RegionA, FacilityA1, UnitB1North)));
    }

    [Fact]
    public async Task Create_rejects_a_client_supplied_facility_outside_the_callers_authorized_scope()
    {
        // Caller is scoped to Facility A1 only; the request asks for Facility B1 — this must be rejected
        // by the server regardless of what the client sends, per "أي FacilityId يرسله العميل يعتبر
        // Presentation state وليس مصدرًا موثوقًا".
        var (commands, _) = BuildService(new UserScopeSnapshot(ScopeType.Facility, RegionA, FacilityA1, null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => commands.CreateDraftAsync(Request(RegionB, FacilityB1)));
    }

    [Fact]
    public async Task Create_rejects_a_nonexistent_facility_unit()
    {
        var (commands, _) = BuildService(new UserScopeSnapshot(ScopeType.Facility, RegionA, FacilityA1, null));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => commands.CreateDraftAsync(Request(RegionA, FacilityA1, Guid.NewGuid())));
    }
}
