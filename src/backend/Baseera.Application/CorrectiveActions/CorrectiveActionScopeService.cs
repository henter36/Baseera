namespace Baseera.Application.CorrectiveActions;

using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Domain.CorrectiveActions;
using Microsoft.EntityFrameworkCore;

public interface ICorrectiveActionScopeService
{
    Task<bool> CanAccessAsync(CorrectiveAction action, CancellationToken cancellationToken = default);
    Task<IQueryable<CorrectiveAction>> FilterQueryableAsync(IQueryable<CorrectiveAction> query, CancellationToken cancellationToken = default);
}

public sealed class CorrectiveActionScopeService(
    IBaseeraDbContext db,
    INoteScopeService noteScope) : ICorrectiveActionScopeService
{
    public async Task<bool> CanAccessAsync(CorrectiveAction action, CancellationToken cancellationToken = default)
    {
        var note = await db.OperationalNotes.FirstOrDefaultAsync(n => n.Id == action.OperationalNoteId, cancellationToken);
        return note is not null && noteScope.CanAccess(note);
    }

    public async Task<IQueryable<CorrectiveAction>> FilterQueryableAsync(
        IQueryable<CorrectiveAction> query,
        CancellationToken cancellationToken = default)
    {
        var scopedNotes = await noteScope.FilterQueryableAsync(db.OperationalNotes, cancellationToken);
        return query.Where(a => scopedNotes.Any(n => n.Id == a.OperationalNoteId));
    }
}
