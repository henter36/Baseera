using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

public sealed class NoteCommandServiceTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    private (INoteCommandService Commands, Guid ActorId, Guid ReporterId) BuildService(params string[] permissions)
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        NoteTestFixtures.GrantPermissions(_db, actor.Id, "Actor", permissions);
        var current = FakeUser(actor.Id, permissions);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var orgScope = new OrganizationalScopeService(current, _db);
        var typeAccess = new NoteTypeAccessService(_db, current);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        var queries = new NoteQueryService(_db, current, scope, typeAccess, audit);
        var routing = new NoteRoutingService(_db, current, scope, typeAccess, audit, TimeProvider.System);
        return (new NoteCommandService(_db, current, scope, typeAccess, routing, audit, queries), actor.Id, reporter.Id);
    }

    private static string RowVersionOf(OperationalNote note) => Convert.ToBase64String(note.RowVersion);

    private static UpdateNoteRequest UpdateRequest(OperationalNote note) => new(
        Title: "عنوان محدّث",
        Description: "وصف محدّث بالكامل",
        NoteTypeId: NoteTestFixtures.DefaultNoteTypeId,
        Severity: NoteSeverity.High,
        SourceType: NoteSourceType.Report,
        SourceReference: "REP-1",
        Classification: ClassificationLevel.Restricted,
        OwnerDepartmentId: null,
        DueAtUtc: DateTimeOffset.UtcNow.AddDays(5),
        RowVersion: RowVersionOf(note));

    [Theory]
    [InlineData(NoteStatus.Draft)]
    [InlineData(NoteStatus.Open)]
    [InlineData(NoteStatus.Assigned)]
    [InlineData(NoteStatus.InProgress)]
    [InlineData(NoteStatus.PendingVerification)]
    [InlineData(NoteStatus.Reopened)]
    public async Task Update_succeeds_for_non_terminal_statuses(NoteStatus status)
    {
        var (commands, _, reporterId) = BuildService(PermissionCodes.NotesUpdate, PermissionCodes.NotesView);
        var note = SeedNote(status, reporterId);

        var result = await commands.UpdateAsync(note.Id, UpdateRequest(note));

        Assert.Equal("عنوان محدّث", result.Title);
        Assert.Equal(NoteSeverity.High, result.Severity);
    }

    [Theory]
    [InlineData(NoteStatus.Closed)]
    [InlineData(NoteStatus.Cancelled)]
    public async Task Update_is_blocked_for_terminal_statuses(NoteStatus status)
    {
        var (commands, _, reporterId) = BuildService(PermissionCodes.NotesUpdate, PermissionCodes.NotesView);
        var note = SeedNote(status, reporterId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.UpdateAsync(note.Id, UpdateRequest(note)));
    }

    [Fact]
    public async Task Update_without_permission_is_rejected()
    {
        var (commands, _, reporterId) = BuildService(/* no permission */);
        var note = SeedNote(NoteStatus.Draft, reporterId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => commands.UpdateAsync(note.Id, UpdateRequest(note)));
    }

    [Fact]
    public async Task Submit_moves_draft_to_open()
    {
        var (commands, _, reporterId) = BuildService(PermissionCodes.NotesUpdate, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.Draft, reporterId);

        var result = await commands.SubmitAsync(note.Id, new TransitionNoteRequest("تقديم الملاحظة", RowVersionOf(note)));

        Assert.Equal(NoteStatus.Open, result.Status);
    }

    [Fact]
    public async Task Archive_soft_deletes_and_restore_reverses_it()
    {
        var (commands, _, reporterId) = BuildService(PermissionCodes.NotesArchive, PermissionCodes.NotesRestore, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.Cancelled, reporterId);

        await commands.ArchiveAsync(note.Id, new TransitionNoteRequest("أرشفة", RowVersionOf(note)));
        var archivedNote = _db.OperationalNotes.IgnoreQueryFilters().Single(n => n.Id == note.Id);
        Assert.True(archivedNote.IsDeleted);

        await commands.RestoreAsync(note.Id, new TransitionNoteRequest("استعادة", Convert.ToBase64String(archivedNote.RowVersion)));
        var restored = _db.OperationalNotes.Single(n => n.Id == note.Id);
        Assert.False(restored.IsDeleted);
    }

    [Fact]
    public async Task Restoring_a_non_archived_note_is_rejected()
    {
        var (commands, _, reporterId) = BuildService(PermissionCodes.NotesRestore, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.Draft, reporterId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.RestoreAsync(note.Id, new TransitionNoteRequest("استعادة", RowVersionOf(note))));
    }

    [Fact]
    public async Task Archive_without_permission_is_rejected()
    {
        var (commands, _, reporterId) = BuildService(/* no permission */);
        var note = SeedNote(NoteStatus.Cancelled, reporterId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            commands.ArchiveAsync(note.Id, new TransitionNoteRequest("أرشفة", RowVersionOf(note))));
    }

    private OperationalNote SeedNote(NoteStatus status, Guid reporterId)
    {
        var note = NoteTestFixtures.NewNote(ScopeType.Global, reporterId, status: status);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        return note;
    }

    private static ICurrentUser FakeUser(Guid userId, params string[] permissions) =>
        new FakeCurrentUser(true, userId, userId.ToString(), "actor", permissions, [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
}
