using Baseera.Application.Forms;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests.Forms;

public sealed class FormGrantResolverTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RoleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-21T12:00:00Z");

    [Fact]
    public void Deny_beats_allow_for_same_capability()
    {
        var form = GlobalForm();
        var grants = new[]
        {
            Grant(FormAccessCapability.View, FormAccessGrantEffect.Allow),
            Grant(FormAccessCapability.View, FormAccessGrantEffect.Deny)
        };

        var result = FormGrantResolver.ResolveEffectiveGrant(
            grants,
            FormAccessCapability.View,
            UserId,
            [RoleId],
            form,
            Now);

        Assert.False(result);
    }

    [Fact]
    public void Expired_deny_grant_does_not_block()
    {
        var form = GlobalForm();
        var grants = new[]
        {
            Grant(
                FormAccessCapability.View,
                FormAccessGrantEffect.Deny,
                validToUtc: Now.AddDays(-1))
        };

        var result = FormGrantResolver.ResolveEffectiveGrant(
            grants,
            FormAccessCapability.View,
            UserId,
            [RoleId],
            form,
            Now);

        Assert.Null(result);
    }

    [Fact]
    public void Future_deny_grant_does_not_block_yet()
    {
        var form = GlobalForm();
        var grants = new[]
        {
            Grant(
                FormAccessCapability.View,
                FormAccessGrantEffect.Deny,
                validFromUtc: Now.AddDays(1))
        };

        var result = FormGrantResolver.ResolveEffectiveGrant(
            grants,
            FormAccessCapability.View,
            UserId,
            [RoleId],
            form,
            Now);

        Assert.Null(result);
    }

    [Fact]
    public void Active_allow_grant_returns_true()
    {
        var form = GlobalForm();
        var grants = new[] { Grant(FormAccessCapability.Approve, FormAccessGrantEffect.Allow) };

        var result = FormGrantResolver.ResolveEffectiveGrant(
            grants,
            FormAccessCapability.Approve,
            UserId,
            [RoleId],
            form,
            Now);

        Assert.True(result);
    }

    [Fact]
    public void No_matching_grants_returns_null()
    {
        var form = GlobalForm();
        var grants = new[] { Grant(FormAccessCapability.Design, FormAccessGrantEffect.Allow) };

        var result = FormGrantResolver.ResolveEffectiveGrant(
            grants,
            FormAccessCapability.View,
            UserId,
            [RoleId],
            form,
            Now);

        Assert.Null(result);
    }

    [Fact]
    public void User_principal_must_match()
    {
        var form = GlobalForm();
        var grants = new[]
        {
            new FormAccessGrant
            {
                PrincipalType = FormAccessGrantPrincipalType.User,
                PrincipalId = Guid.NewGuid(),
                Capability = FormAccessCapability.View,
                Effect = FormAccessGrantEffect.Allow
            }
        };

        var result = FormGrantResolver.ResolveEffectiveGrant(
            grants,
            FormAccessCapability.View,
            UserId,
            [RoleId],
            form,
            Now);

        Assert.Null(result);
    }

    [Fact]
    public void Role_principal_matches_role_ids()
    {
        var form = GlobalForm();
        var grants = new[]
        {
            new FormAccessGrant
            {
                PrincipalType = FormAccessGrantPrincipalType.Role,
                PrincipalId = RoleId,
                Capability = FormAccessCapability.Review,
                Effect = FormAccessGrantEffect.Allow
            }
        };

        var result = FormGrantResolver.ResolveEffectiveGrant(
            grants,
            FormAccessCapability.Review,
            UserId,
            [RoleId],
            form,
            Now);

        Assert.True(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsWithinValidityWindow_respects_bounds(bool inWindow)
    {
        var grant = Grant(
            FormAccessCapability.View,
            FormAccessGrantEffect.Allow,
            validFromUtc: Now.AddHours(-1),
            validToUtc: Now.AddHours(1));

        Assert.Equal(inWindow, FormGrantResolver.IsWithinValidityWindow(grant, inWindow ? Now : Now.AddHours(2)));
    }

    [Fact]
    public void Region_grant_only_covers_matching_region_form()
    {
        var regionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var grant = new FormAccessGrant
        {
            ScopeType = ScopeType.Region,
            RegionId = regionId,
            Capability = FormAccessCapability.View,
            Effect = FormAccessGrantEffect.Allow,
            PrincipalType = FormAccessGrantPrincipalType.User,
            PrincipalId = UserId
        };

        var matching = new FormDefinition { ScopeType = ScopeType.Region, RegionId = regionId };
        var other = new FormDefinition { ScopeType = ScopeType.Region, RegionId = Guid.NewGuid() };

        Assert.True(FormGrantResolver.GrantScopeCoversForm(grant, matching));
        Assert.False(FormGrantResolver.GrantScopeCoversForm(grant, other));
    }

    [Fact]
    public void Facility_grant_covers_facility_and_unit_forms()
    {
        var facilityId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var grant = new FormAccessGrant
        {
            ScopeType = ScopeType.Facility,
            FacilityId = facilityId,
            Capability = FormAccessCapability.View,
            Effect = FormAccessGrantEffect.Allow,
            PrincipalType = FormAccessGrantPrincipalType.User,
            PrincipalId = UserId
        };

        var facilityForm = new FormDefinition { ScopeType = ScopeType.Facility, FacilityId = facilityId };
        var unitForm = new FormDefinition { ScopeType = ScopeType.FacilityUnit, FacilityId = facilityId };
        var other = new FormDefinition { ScopeType = ScopeType.Facility, FacilityId = Guid.NewGuid() };

        Assert.True(FormGrantResolver.GrantScopeCoversForm(grant, facilityForm));
        Assert.True(FormGrantResolver.GrantScopeCoversForm(grant, unitForm));
        Assert.False(FormGrantResolver.GrantScopeCoversForm(grant, other));
    }

    [Fact]
    public void GrantScopeWithinGrantorScope_requires_national_access_for_global_grants()
    {
        var grant = new FormAccessGrant { ScopeType = ScopeType.Global };
        var national = new StubOrgScope(hasNational: true);
        var regional = new StubOrgScope(hasNational: false, regionId: SeedIds.RegionA);

        Assert.True(FormGrantResolver.GrantScopeWithinGrantorScope(grant, national));
        Assert.False(FormGrantResolver.GrantScopeWithinGrantorScope(grant, regional));
    }

    [Fact]
    public void GrantScopeWithinGrantorScope_checks_region_and_facility()
    {
        var regionGrant = new FormAccessGrant { ScopeType = ScopeType.Region, RegionId = SeedIds.RegionA };
        var facilityGrant = new FormAccessGrant { ScopeType = ScopeType.Facility, FacilityId = SeedIds.FacilityA1 };

        var org = new StubOrgScope(regionId: SeedIds.RegionA, facilityId: SeedIds.FacilityA1);
        var outsider = new StubOrgScope(regionId: SeedIds.RegionB, facilityId: SeedIds.FacilityB1);

        Assert.True(FormGrantResolver.GrantScopeWithinGrantorScope(regionGrant, org));
        Assert.False(FormGrantResolver.GrantScopeWithinGrantorScope(regionGrant, outsider));
        Assert.True(FormGrantResolver.GrantScopeWithinGrantorScope(facilityGrant, org));
        Assert.False(FormGrantResolver.GrantScopeWithinGrantorScope(facilityGrant, outsider));
    }

    private static FormDefinition GlobalForm() =>
        new() { ScopeType = ScopeType.Global };

    private static FormAccessGrant Grant(
        FormAccessCapability capability,
        FormAccessGrantEffect effect,
        DateTimeOffset? validFromUtc = null,
        DateTimeOffset? validToUtc = null) => new()
    {
        PrincipalType = FormAccessGrantPrincipalType.User,
        PrincipalId = UserId,
        Capability = capability,
        Effect = effect,
        ValidFromUtc = validFromUtc,
        ValidToUtc = validToUtc
    };

    private sealed class StubOrgScope : Application.Abstractions.IOrganizationalScopeService
    {
        private readonly bool _hasNational;
        private readonly bool _hasHq;
        private readonly Guid? _regionId;
        private readonly Guid? _facilityId;

        public StubOrgScope(
            bool hasNational = false,
            bool hasHq = false,
            Guid? regionId = null,
            Guid? facilityId = null)
        {
            _hasNational = hasNational;
            _hasHq = hasHq;
            _regionId = regionId;
            _facilityId = facilityId;
        }

        public bool HasNationalAccess => _hasNational;
        public bool HasHeadquartersAccess => _hasNational || _hasHq;
        public bool CanAccessRegion(Guid regionId) => _hasNational || _regionId == regionId;
        public bool CanAccessFacility(Guid facilityId) => _hasNational || _facilityId == facilityId;
        public bool CanAccessFacilityUnit(Guid facilityUnitId) => _hasNational;
        public IQueryable<Domain.Organization.Region> FilterRegions(IQueryable<Domain.Organization.Region> query) => query;
        public IQueryable<Domain.Organization.Facility> FilterFacilities(IQueryable<Domain.Organization.Facility> query) => query;
        public bool CanAccess(IScopedEntity entity) => true;
        public string SummarizeScopes() => "stub";
    }
}
