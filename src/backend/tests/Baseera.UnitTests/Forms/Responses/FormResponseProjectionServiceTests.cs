using Baseera.Application.Abstractions;
using Baseera.Application.Forms;
using Baseera.Application.Forms.Responses;
using Baseera.Application.Security;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Forms.Schema;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using System.Text.Json;

namespace Baseera.UnitTests.Forms.Responses;

public sealed class FormResponseProjectionServiceTests
{
    private readonly FormResponseProjectionService _sut = new();

    private static FormSchemaDocument SensitiveSchema() => new()
    {
        Pages =
        [
            new FormPageSchema
            {
                Key = "p1",
                TitleAr = "صفحة",
                Sections =
                [
                    new FormSectionSchema
                    {
                        Key = "s1",
                        TitleAr = "قسم",
                        Fields =
                        [
                            new FormFieldSchema
                            {
                                Key = "secret",
                                Type = FormFieldType.ShortText,
                                LabelAr = "سري",
                                ClassificationOverride = ClassificationLevel.Confidential
                            },
                            new FormFieldSchema
                            {
                                Key = "public",
                                Type = FormFieldType.ShortText,
                                LabelAr = "عام"
                            }
                        ]
                    }
                ]
            }
        ]
    };

    [Fact]
    public void Owner_respondent_sees_sensitive_values()
    {
        var (json, visibility, redacted) = _sut.ProjectAnswers(
            SensitiveSchema(),
            ClassificationLevel.Internal,
            """{"secret":"حساس","public":"ظاهر"}""",
            canViewSensitive: false,
            isOwnerRespondent: true);

        using var doc = JsonDocument.Parse(json!);
        Assert.Equal("حساس", doc.RootElement.GetProperty("secret").GetString());
        Assert.True(visibility["secret"]);
        Assert.False(redacted["secret"]);
    }

    [Fact]
    public void Reviewer_without_view_sensitive_gets_redacted()
    {
        var (json, _, redacted) = _sut.ProjectAnswers(
            SensitiveSchema(),
            ClassificationLevel.Internal,
            """{"secret":"حساس","public":"ظاهر"}""",
            canViewSensitive: false,
            isOwnerRespondent: false);

        using var doc = JsonDocument.Parse(json!);
        Assert.Equal("***", doc.RootElement.GetProperty("secret").GetString());
        Assert.True(redacted["secret"]);
        Assert.Equal("ظاهر", doc.RootElement.GetProperty("public").GetString());
    }

    [Fact]
    public void Reviewer_with_view_sensitive_sees_values()
    {
        var (json, visibility, redacted) = _sut.ProjectAnswers(
            SensitiveSchema(),
            ClassificationLevel.Internal,
            """{"secret":"حساس","public":"ظاهر"}""",
            canViewSensitive: true,
            isOwnerRespondent: false);

        using var doc = JsonDocument.Parse(json!);
        Assert.Equal("حساس", doc.RootElement.GetProperty("secret").GetString());
        Assert.True(visibility["secret"]);
        Assert.False(redacted["secret"]);
    }

    [Fact]
    public void Access_coordinator_owner_in_scope_sees_sensitive_on_detail()
    {
        using var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var owner = FormTestFixtures.AddUser(db, "مالك");
        var other = FormTestFixtures.AddUser(db, "آخر");
        var form = FormTestFixtures.NewForm(owner.Id, classification: ClassificationLevel.Confidential);
        db.FormDefinitions.Add(form);
        var response = NewResponse(form, owner.Id, SeedIds.FacilityA1);
        db.FormResponses.Add(response);
        db.SaveChanges();

        var ownerAccess = CreateCoordinator(db, owner.Id, [PermissionCodes.FormsRespond],
            new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));
        var otherAccess = CreateCoordinator(db, other.Id, [PermissionCodes.FormsViewResponses],
            new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA2, null));

        Assert.True(ownerAccess.IsRespondentOwner(response));
        Assert.False(otherAccess.IsRespondentOwner(response));
        Assert.False(otherAccess.CanViewSensitiveResponses());

        var ownerProjection = _sut.ProjectAnswers(
            SensitiveSchema(), form.Classification, """{"secret":"x"}""",
            ownerAccess.CanViewSensitiveResponses(), ownerAccess.IsRespondentOwner(response));
        using (var doc = JsonDocument.Parse(ownerProjection.AnswersJson!))
        {
            Assert.Equal("x", doc.RootElement.GetProperty("secret").GetString());
        }

        var otherProjection = _sut.ProjectAnswers(
            SensitiveSchema(), form.Classification, """{"secret":"x"}""",
            otherAccess.CanViewSensitiveResponses(), otherAccess.IsRespondentOwner(response));
        using (var doc = JsonDocument.Parse(otherProjection.AnswersJson!))
        {
            Assert.Equal("***", doc.RootElement.GetProperty("secret").GetString());
        }
    }

    private static FormResponseAccessCoordinator CreateCoordinator(
        Baseera.Infrastructure.Persistence.BaseeraDbContext db,
        Guid userId,
        string[] permissions,
        UserScopeSnapshot scope)
    {
        var current = FormTestFixtures.CurrentUser(userId, permissions, scope);
        var orgScope = new OrganizationalScopeService(current, db);
        var effective = new FormEffectiveAccessService(db, current);
        return new FormResponseAccessCoordinator(current, orgScope, effective, db);
    }

    private static FormResponse NewResponse(FormDefinition form, Guid ownerUserId, Guid facilityId) => new()
    {
        AssignmentId = Guid.NewGuid(),
        CampaignId = Guid.NewGuid(),
        CycleId = Guid.NewGuid(),
        FacilityId = facilityId,
        FormSchemaSnapshotId = Guid.NewGuid(),
        SchemaHash = "hash",
        Status = FormResponseStatus.Draft,
        LastSavedByUserId = ownerUserId,
        DraftAnswersJson = "{}"
    };
}
