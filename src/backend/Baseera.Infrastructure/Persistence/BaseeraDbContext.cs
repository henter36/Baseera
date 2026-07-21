namespace Baseera.Infrastructure.Persistence;

using Baseera.Domain.Attachments;
using Baseera.Domain.Audit;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Escalations;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public sealed class BaseeraDbContext(DbContextOptions<BaseeraDbContext> options) : DbContext(options), Application.Abstractions.IBaseeraDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<FacilityUnit> FacilityUnits => Set<FacilityUnit>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<FacilityAssetLocation> FacilityAssetLocations => Set<FacilityAssetLocation>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserScope> UserScopes => Set<UserScope>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<NoteType> NoteTypes => Set<NoteType>();
    public DbSet<RoleNoteTypeGrant> RoleNoteTypeGrants => Set<RoleNoteTypeGrant>();
    public DbSet<UserNoteTypeOverride> UserNoteTypeOverrides => Set<UserNoteTypeOverride>();
    public DbSet<UserNoteIntakeProfile> UserNoteIntakeProfiles => Set<UserNoteIntakeProfile>();
    public DbSet<NoteRoutingRule> NoteRoutingRules => Set<NoteRoutingRule>();
    public DbSet<NoteRoutingDecision> NoteRoutingDecisions => Set<NoteRoutingDecision>();
    public DbSet<NoteRoutingRuleHistory> NoteRoutingRuleHistories => Set<NoteRoutingRuleHistory>();
    public DbSet<NoteTypeAccessChangeHistory> NoteTypeAccessChangeHistories => Set<NoteTypeAccessChangeHistory>();
    public DbSet<OperationalNote> OperationalNotes => Set<OperationalNote>();
    public DbSet<NoteAssignment> NoteAssignments => Set<NoteAssignment>();
    public DbSet<NoteStatusHistory> NoteStatusHistories => Set<NoteStatusHistory>();
    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();
    public DbSet<CorrectiveActionAssignment> CorrectiveActionAssignments => Set<CorrectiveActionAssignment>();
    public DbSet<CorrectiveActionStatusHistory> CorrectiveActionStatusHistories => Set<CorrectiveActionStatusHistory>();
    public DbSet<EscalationPolicy> EscalationPolicies => Set<EscalationPolicy>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<EscalationOccurrence> EscalationOccurrences => Set<EscalationOccurrence>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();
    public DbSet<BackgroundJobLease> BackgroundJobLeases => Set<BackgroundJobLease>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<FormReviewDecision> FormReviewDecisions => Set<FormReviewDecision>();
    public DbSet<FormGovernancePolicy> FormGovernancePolicies => Set<FormGovernancePolicy>();
    public DbSet<FormAccessGrant> FormAccessGrants => Set<FormAccessGrant>();

    IQueryable<Organization> Application.Abstractions.IBaseeraDbContext.Organizations => Organizations;
    IQueryable<Region> Application.Abstractions.IBaseeraDbContext.Regions => Regions;
    IQueryable<Facility> Application.Abstractions.IBaseeraDbContext.Facilities => Facilities;
    IQueryable<FacilityUnit> Application.Abstractions.IBaseeraDbContext.FacilityUnits => FacilityUnits;
    IQueryable<Building> Application.Abstractions.IBaseeraDbContext.Buildings => Buildings;
    IQueryable<FacilityAssetLocation> Application.Abstractions.IBaseeraDbContext.FacilityAssetLocations => FacilityAssetLocations;
    IQueryable<Department> Application.Abstractions.IBaseeraDbContext.Departments => Departments;
    IQueryable<User> Application.Abstractions.IBaseeraDbContext.Users => Users;
    IQueryable<User> Application.Abstractions.IBaseeraDbContext.UsersIncludingDeleted => Users.IgnoreQueryFilters();
    IQueryable<Role> Application.Abstractions.IBaseeraDbContext.Roles => Roles;
    IQueryable<Permission> Application.Abstractions.IBaseeraDbContext.Permissions => Permissions;
    IQueryable<UserRole> Application.Abstractions.IBaseeraDbContext.UserRoles => UserRoles;
    IQueryable<RolePermission> Application.Abstractions.IBaseeraDbContext.RolePermissions => RolePermissions;
    IQueryable<UserScope> Application.Abstractions.IBaseeraDbContext.UserScopes => UserScopes;
    IQueryable<AuditLog> Application.Abstractions.IBaseeraDbContext.AuditLogs => AuditLogs;
    IQueryable<Attachment> Application.Abstractions.IBaseeraDbContext.Attachments => Attachments;
    IQueryable<NoteType> Application.Abstractions.IBaseeraDbContext.NoteTypes => NoteTypes;
    IQueryable<RoleNoteTypeGrant> Application.Abstractions.IBaseeraDbContext.RoleNoteTypeGrants => RoleNoteTypeGrants;
    IQueryable<UserNoteTypeOverride> Application.Abstractions.IBaseeraDbContext.UserNoteTypeOverrides => UserNoteTypeOverrides;
    IQueryable<UserNoteIntakeProfile> Application.Abstractions.IBaseeraDbContext.UserNoteIntakeProfiles => UserNoteIntakeProfiles;
    IQueryable<NoteRoutingRule> Application.Abstractions.IBaseeraDbContext.NoteRoutingRules => NoteRoutingRules;
    IQueryable<NoteRoutingRule> Application.Abstractions.IBaseeraDbContext.NoteRoutingRulesIncludingDeleted => NoteRoutingRules.IgnoreQueryFilters();
    IQueryable<NoteRoutingDecision> Application.Abstractions.IBaseeraDbContext.NoteRoutingDecisions => NoteRoutingDecisions;
    IQueryable<NoteRoutingRuleHistory> Application.Abstractions.IBaseeraDbContext.NoteRoutingRuleHistories => NoteRoutingRuleHistories;
    IQueryable<NoteTypeAccessChangeHistory> Application.Abstractions.IBaseeraDbContext.NoteTypeAccessChangeHistories => NoteTypeAccessChangeHistories;
    IQueryable<OperationalNote> Application.Abstractions.IBaseeraDbContext.OperationalNotes => OperationalNotes;
    IQueryable<OperationalNote> Application.Abstractions.IBaseeraDbContext.OperationalNotesIncludingDeleted => OperationalNotes.IgnoreQueryFilters();
    IQueryable<NoteAssignment> Application.Abstractions.IBaseeraDbContext.NoteAssignments => NoteAssignments;
    IQueryable<NoteStatusHistory> Application.Abstractions.IBaseeraDbContext.NoteStatusHistories => NoteStatusHistories;
    IQueryable<CorrectiveAction> Application.Abstractions.IBaseeraDbContext.CorrectiveActions => CorrectiveActions;
    IQueryable<CorrectiveAction> Application.Abstractions.IBaseeraDbContext.CorrectiveActionsIncludingDeleted => CorrectiveActions.IgnoreQueryFilters();
    IQueryable<CorrectiveActionAssignment> Application.Abstractions.IBaseeraDbContext.CorrectiveActionAssignments => CorrectiveActionAssignments;
    IQueryable<CorrectiveActionStatusHistory> Application.Abstractions.IBaseeraDbContext.CorrectiveActionStatusHistories => CorrectiveActionStatusHistories;
    IQueryable<EscalationPolicy> Application.Abstractions.IBaseeraDbContext.EscalationPolicies => EscalationPolicies;
    IQueryable<EscalationPolicy> Application.Abstractions.IBaseeraDbContext.EscalationPoliciesIncludingDeleted => EscalationPolicies.IgnoreQueryFilters();
    IQueryable<EscalationRule> Application.Abstractions.IBaseeraDbContext.EscalationRules => EscalationRules;
    IQueryable<EscalationRule> Application.Abstractions.IBaseeraDbContext.EscalationRulesIncludingDeleted => EscalationRules.IgnoreQueryFilters();
    IQueryable<EscalationOccurrence> Application.Abstractions.IBaseeraDbContext.EscalationOccurrences => EscalationOccurrences;
    IQueryable<Notification> Application.Abstractions.IBaseeraDbContext.Notifications => Notifications;
    IQueryable<NotificationDeliveryAttempt> Application.Abstractions.IBaseeraDbContext.NotificationDeliveryAttempts => NotificationDeliveryAttempts;
    IQueryable<BackgroundJobLease> Application.Abstractions.IBaseeraDbContext.BackgroundJobLeases => BackgroundJobLeases;
    IQueryable<FormDefinition> Application.Abstractions.IBaseeraDbContext.FormDefinitions => FormDefinitions;
    IQueryable<FormDefinition> Application.Abstractions.IBaseeraDbContext.FormDefinitionsIncludingDeleted => FormDefinitions.IgnoreQueryFilters();
    IQueryable<FormReviewDecision> Application.Abstractions.IBaseeraDbContext.FormReviewDecisions => FormReviewDecisions;
    IQueryable<FormGovernancePolicy> Application.Abstractions.IBaseeraDbContext.FormGovernancePolicies => FormGovernancePolicies;
    IQueryable<FormAccessGrant> Application.Abstractions.IBaseeraDbContext.FormAccessGrants => FormAccessGrants;
    IQueryable<FormAccessGrant> Application.Abstractions.IBaseeraDbContext.FormAccessGrantsIncludingDeleted => FormAccessGrants.IgnoreQueryFilters();

    public void Detach<TEntity>(TEntity entity) where TEntity : class => Entry(entity).State = EntityState.Detached;
    public void ClearChanges() => ChangeTracker.Clear();

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return await operation(cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public new void Add<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Add(entity);
    public new void Update<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Update(entity);

    public async Task<long> NextOperationalNoteSequenceValueAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Database
            .SqlQueryRaw<SequenceValueRow>("SELECT NEXT VALUE FOR [OperationalNoteReferenceSequence] AS [Value]")
            .ToListAsync(cancellationToken);
        return rows.Single().Value;
    }

    public async Task<long> NextCorrectiveActionSequenceValueAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Database
            .SqlQueryRaw<SequenceValueRow>("SELECT NEXT VALUE FOR [CorrectiveActionReferenceSequence] AS [Value]")
            .ToListAsync(cancellationToken);
        return rows.Single().Value;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("OperationalNoteReferenceSequence")
            .StartsAt(1)
            .IncrementsBy(1);
        modelBuilder.HasSequence<long>("CorrectiveActionReferenceSequence")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BaseeraDbContext).Assembly);

        modelBuilder.Entity<Organization>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Region>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Facility>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FacilityUnit>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Building>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FacilityAssetLocation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Department>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Role>().HasQueryFilter(e => !e.IsDeleted);
        // Join entities must filter deleted Role (and User) to avoid EF 10622 with required navigations.
        modelBuilder.Entity<UserRole>().HasQueryFilter(ur => !ur.Role.IsDeleted && !ur.User.IsDeleted);
        modelBuilder.Entity<RolePermission>().HasQueryFilter(rp => !rp.Role.IsDeleted);
        modelBuilder.Entity<UserScope>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Attachment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RoleNoteTypeGrant>().HasQueryFilter(g => !g.Role.IsDeleted);
        modelBuilder.Entity<UserNoteTypeOverride>().HasQueryFilter(o => !o.User.IsDeleted);
        modelBuilder.Entity<UserNoteIntakeProfile>().HasQueryFilter(p => !p.User.IsDeleted);
        modelBuilder.Entity<NoteRoutingRule>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<OperationalNote>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CorrectiveAction>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<EscalationPolicy>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<EscalationRule>().HasQueryFilter(e => !e.IsDeleted);
        // Join/dependent entities must filter deleted OperationalNote/User to avoid EF 10622 with required navigations.
        modelBuilder.Entity<NoteAssignment>().HasQueryFilter(na => !na.OperationalNote.IsDeleted && !na.AssignedByUser.IsDeleted);
        modelBuilder.Entity<NoteStatusHistory>().HasQueryFilter(h => !h.OperationalNote.IsDeleted && !h.ChangedByUser.IsDeleted);
        modelBuilder.Entity<CorrectiveActionAssignment>().HasQueryFilter(a => !a.CorrectiveAction.IsDeleted && !a.AssignedByUser.IsDeleted);
        modelBuilder.Entity<CorrectiveActionStatusHistory>().HasQueryFilter(h => !h.CorrectiveAction.IsDeleted && !h.ChangedByUser.IsDeleted);
        modelBuilder.Entity<EscalationRule>().HasQueryFilter(r => !r.IsDeleted && !r.EscalationPolicy.IsDeleted);
        modelBuilder.Entity<FormDefinition>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<FormAccessGrant>().HasQueryFilter(g => !g.IsDeleted);
        modelBuilder.Entity<FormReviewDecision>().HasQueryFilter(d => !d.FormDefinition.IsDeleted);
        modelBuilder.Entity<Notification>().HasQueryFilter(n => !n.RecipientUser.IsDeleted);
        modelBuilder.Entity<NotificationDeliveryAttempt>().HasQueryFilter(a => !a.Notification.RecipientUser.IsDeleted);

        modelBuilder.Entity<UserScope>().ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_UserScopes_GlobalHq_NoIds",
                "([ScopeType] NOT IN (0, 1)) OR ([RegionId] IS NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserScopes_Region_RequiresRegion",
                "([ScopeType] NOT IN (2, 5)) OR ([RegionId] IS NOT NULL AND [FacilityId] IS NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserScopes_Facility_RequiresFacility",
                "([ScopeType] NOT IN (3, 6)) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NULL)");
            t.HasCheckConstraint(
                "CK_UserScopes_Unit_RequiresFacilityAndUnit",
                "([ScopeType] <> 4) OR ([FacilityId] IS NOT NULL AND [FacilityUnitId] IS NOT NULL)");
        });
    }

    public override int SaveChanges()
    {
        EnforceAppendOnlyGuards();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceAppendOnlyGuards();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void EnforceAppendOnlyGuards()
    {
        AuditAppendOnlyGuard.EnsureAuditEntriesAreAppendOnly(this);
        NoteStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(this);
        CorrectiveActionStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(this);
        EscalationAppendOnlyGuard.EnsureEntriesAreAppendOnly(this);
        NoteRoutingAppendOnlyGuard.EnsureEntriesAreAppendOnly(this);
    }
}

internal sealed class SequenceValueRow
{
    public long Value { get; set; }
}

/// <summary>
/// Shared append-only enforcement used by DbContext overrides and the interceptor.
/// </summary>
internal static class AuditAppendOnlyGuard
{
    public static void EnsureAuditEntriesAreAppendOnly(DbContext context)
    {
        var invalidEntries = context.ChangeTracker
            .Entries<AuditLog>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidEntries.Any())
        {
            throw new InvalidOperationException("AuditLog is append-only and cannot be modified or deleted.");
        }
    }
}

internal static class NoteStatusHistoryAppendOnlyGuard
{
    public static void EnsureEntriesAreAppendOnly(DbContext context)
    {
        var invalidEntries = context.ChangeTracker
            .Entries<NoteStatusHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidEntries.Any())
        {
            throw new InvalidOperationException("NoteStatusHistory is append-only and cannot be modified or deleted.");
        }
    }
}

internal static class CorrectiveActionStatusHistoryAppendOnlyGuard
{
    public static void EnsureEntriesAreAppendOnly(DbContext context)
    {
        var invalidEntries = context.ChangeTracker
            .Entries<CorrectiveActionStatusHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidEntries.Any())
        {
            throw new InvalidOperationException("CorrectiveActionStatusHistory is append-only and cannot be modified or deleted.");
        }
    }
}

internal static class EscalationAppendOnlyGuard
{
    public static void EnsureEntriesAreAppendOnly(DbContext context)
    {
        var invalidOccurrences = context.ChangeTracker
            .Entries<EscalationOccurrence>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        var invalidAttempts = context.ChangeTracker
            .Entries<NotificationDeliveryAttempt>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidOccurrences.Any())
        {
            throw new InvalidOperationException("EscalationOccurrence is append-only and cannot be modified or deleted.");
        }

        if (invalidAttempts.Any())
        {
            throw new InvalidOperationException("NotificationDeliveryAttempt is append-only and cannot be modified or deleted.");
        }
    }
}

internal static class NoteRoutingAppendOnlyGuard
{
    public static void EnsureEntriesAreAppendOnly(DbContext context)
    {
        var invalidDecisions = context.ChangeTracker
            .Entries<NoteRoutingDecision>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        var invalidRuleHistory = context.ChangeTracker
            .Entries<NoteRoutingRuleHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        var invalidAccessHistory = context.ChangeTracker
            .Entries<NoteTypeAccessChangeHistory>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidDecisions.Any())
        {
            throw new InvalidOperationException("NoteRoutingDecision is append-only and cannot be modified or deleted.");
        }

        if (invalidRuleHistory.Any())
        {
            throw new InvalidOperationException("NoteRoutingRuleHistory is append-only and cannot be modified or deleted.");
        }

        if (invalidAccessHistory.Any())
        {
            throw new InvalidOperationException("NoteTypeAccessChangeHistory is append-only and cannot be modified or deleted.");
        }
    }
}

public sealed class AuditImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            AuditAppendOnlyGuard.EnsureAuditEntriesAreAppendOnly(eventData.Context);
            NoteStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
        CorrectiveActionStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            EscalationAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            NoteRoutingAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            AuditAppendOnlyGuard.EnsureAuditEntriesAreAppendOnly(eventData.Context);
            NoteStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            CorrectiveActionStatusHistoryAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            EscalationAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
            NoteRoutingAppendOnlyGuard.EnsureEntriesAreAppendOnly(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
