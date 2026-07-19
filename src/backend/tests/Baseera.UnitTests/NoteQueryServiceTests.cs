using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests;

public sealed class NoteQueryServiceTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    private INoteQueryService BuildService(Guid userId, params string[] permissions)
    {
        var current = FakeUser(userId, permissions);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        return new NoteQueryService(_db, current, scope, audit);
    }

    [Fact]
    public async Task List_requires_notes_view_permission()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        var queries = BuildService(viewer.Id /* no permission */);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => queries.ListAsync(new NoteListQuery()));
    }

    [Fact]
    public async Task Sensitive_note_is_redacted_in_list_without_view_sensitive_permission()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        var note = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, classification: ClassificationLevel.Secret);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();

        var queries = BuildService(viewer.Id, PermissionCodes.NotesView);
        var result = await queries.ListAsync(new NoteListQuery());

        // Sensitive notes stay visible in the list (users must know they exist within their
        // scope) but their title/description are redacted rather than shown outright.
        var item = Assert.Single(result.Items);
        Assert.True(item.IsSensitiveRedacted);
        Assert.Equal("[محجوب]", item.Title);
        Assert.Null(item.DescriptionSnippet);
    }

    [Fact]
    public async Task Sensitive_note_is_visible_with_view_sensitive_permission()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        var note = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, classification: ClassificationLevel.Secret);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();

        var queries = BuildService(viewer.Id, PermissionCodes.NotesView, PermissionCodes.NotesViewSensitive);
        var result = await queries.ListAsync(new NoteListQuery());

        var item = Assert.Single(result.Items);
        Assert.False(item.IsSensitiveRedacted);
        Assert.Equal(note.Title, item.Title);
    }

    [Fact]
    public async Task Sensitive_note_detail_is_fully_redacted_without_permission()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        var note = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, classification: ClassificationLevel.Confidential);
        note.ClosureSummary = "تفاصيل حساسة جدًا";
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();

        var queries = BuildService(viewer.Id, PermissionCodes.NotesView);
        var detail = await queries.GetDetailAsync(note.Id);

        Assert.NotNull(detail);
        Assert.True(detail!.IsSensitiveRedacted);
        Assert.Equal("[محجوب]", detail.Title);
        Assert.Null(detail.ClosureSummary);
    }

    [Fact]
    public async Task Non_sensitive_notes_are_not_redacted()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        var note = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, classification: ClassificationLevel.Internal);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();

        var queries = BuildService(viewer.Id, PermissionCodes.NotesView);
        var detail = await queries.GetDetailAsync(note.Id);

        Assert.NotNull(detail);
        Assert.False(detail!.IsSensitiveRedacted);
        Assert.Equal(note.Title, detail.Title);
    }

    [Fact]
    public async Task Overdue_flag_is_true_for_past_due_open_note_and_false_once_closed()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        var overdueNote = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, status: NoteStatus.InProgress, reference: "OBS-00000001");
        overdueNote.DueAtUtc = DateTimeOffset.UtcNow.AddDays(-3);
        var closedOverdueNote = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, status: NoteStatus.Closed, reference: "OBS-00000002");
        closedOverdueNote.DueAtUtc = DateTimeOffset.UtcNow.AddDays(-3);
        _db.OperationalNotes.AddRange(overdueNote, closedOverdueNote);
        _db.SaveChanges();

        var queries = BuildService(viewer.Id, PermissionCodes.NotesView);
        var result = await queries.ListAsync(new NoteListQuery { PageSize = 50 });

        Assert.True(result.Items.Single(i => i.Id == overdueNote.Id).IsOverdue);
        Assert.False(result.Items.Single(i => i.Id == closedOverdueNote.Id).IsOverdue);
    }

    [Fact]
    public async Task Classification_filter_is_applied_server_side()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        var internalNote = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, classification: ClassificationLevel.Internal, reference: "OBS-00000001");
        var restrictedNote = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, classification: ClassificationLevel.Restricted, reference: "OBS-00000002");
        _db.OperationalNotes.AddRange(internalNote, restrictedNote);
        _db.SaveChanges();

        var queries = BuildService(viewer.Id, PermissionCodes.NotesView, PermissionCodes.NotesViewSensitive);
        var result = await queries.ListAsync(new NoteListQuery { Classification = ClassificationLevel.Restricted });

        var item = Assert.Single(result.Items);
        Assert.Equal(restrictedNote.Id, item.Id);
    }

    [Fact]
    public async Task OverdueOnly_filter_excludes_notes_with_future_or_missing_due_dates()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        var overdue = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, status: NoteStatus.Open, reference: "OBS-00000001");
        overdue.DueAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
        var future = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, status: NoteStatus.Open, reference: "OBS-00000002");
        future.DueAtUtc = DateTimeOffset.UtcNow.AddDays(5);
        var noDueDate = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, status: NoteStatus.Open, reference: "OBS-00000003");
        _db.OperationalNotes.AddRange(overdue, future, noDueDate);
        _db.SaveChanges();

        var queries = BuildService(viewer.Id, PermissionCodes.NotesView);
        var result = await queries.ListAsync(new NoteListQuery { OverdueOnly = true });

        var item = Assert.Single(result.Items);
        Assert.Equal(overdue.Id, item.Id);
    }

    [Fact]
    public async Task Unknown_sort_key_falls_back_to_created_at_without_throwing()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        _db.OperationalNotes.Add(NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id));
        _db.SaveChanges();

        var queries = BuildService(viewer.Id, PermissionCodes.NotesView);
        var result = await queries.ListAsync(new NoteListQuery { SortBy = "'; DROP TABLE OperationalNotes; --" });

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Page_size_is_capped_at_200()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        _db.OperationalNotes.Add(NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id));
        _db.SaveChanges();

        var queries = BuildService(viewer.Id, PermissionCodes.NotesView);
        var result = await queries.ListAsync(new NoteListQuery { PageSize = 10_000 });

        Assert.Equal(200, result.PageSize);
    }

    [Fact]
    public async Task Out_of_scope_note_detail_returns_null_not_throw()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        _db.Regions.Add(new Domain.Organization.Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ" });
        var note = NoteTestFixtures.NewNote(ScopeType.Region, reporter.Id, regionId: SeedIds.RegionA);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();

        var current = new FakeCurrentUser(true, viewer.Id, viewer.Id.ToString(), "viewer",
            [PermissionCodes.NotesView],
            [new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionB, null, null)]);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        var queries = new NoteQueryService(_db, current, scope, audit);

        var detail = await queries.GetDetailAsync(note.Id);
        Assert.Null(detail);
    }

    private static ICurrentUser FakeUser(Guid userId, params string[] permissions) =>
        new FakeCurrentUser(true, userId, userId.ToString(), "actor", permissions, [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
}
