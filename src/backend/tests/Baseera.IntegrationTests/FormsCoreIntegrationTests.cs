using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class FormsCoreIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static int _codeSequence;

    public FormsCoreIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Region_user_cannot_see_other_region_form()
    {
        await SeedWorkflowUsersAsync();
        var admin = _factory.CreateAuthenticatedClient("forms-admin");
        var createdB = await CreateFormAsync(admin, ScopeType.Region, SeedIds.RegionB, null, "FRM-REG-B");

        var regionClient = _factory.CreateAuthenticatedClient("forms-region-a");
        var detail = await regionClient.GetAsync($"/api/v1/forms/{createdB.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var list = await regionClient.GetFromJsonAsync<PagedEnvelope<FormListItem>>("/api/v1/forms?page=1&pageSize=50");
        Assert.NotNull(list);
        Assert.DoesNotContain(list!.Items, f => f.Id == createdB.Id);
    }

    [IntegrationConnectionFact]
    public async Task Facility_user_cannot_see_other_facility_form()
    {
        await SeedWorkflowUsersAsync();
        var admin = _factory.CreateAuthenticatedClient("forms-admin");
        var facilityB = await CreateFormAsync(
            admin,
            ScopeType.Facility,
            SeedIds.RegionB,
            SeedIds.FacilityB1,
            "FRM-FAC-B1");

        var facilityA = _factory.CreateAuthenticatedClient("forms-facility-a1");
        var detail = await facilityA.GetAsync($"/api/v1/forms/{facilityB.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Draft_crud_happy_path()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");

        var created = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());
        Assert.Equal(FormDefinitionStatus.Draft, created.Status);

        var updated = await UpdateFormAsync(designer, created.Id, created.RowVersion, "عنوان محدث");
        Assert.Equal("عنوان محدث", updated.NameAr);

        var detail = await designer.GetFromJsonAsync<FormDetail>($"/api/v1/forms/{created.Id}");
        Assert.NotNull(detail);
        Assert.Equal(updated.NameAr, detail!.NameAr);

        var list = await designer.GetFromJsonAsync<PagedEnvelope<FormListItem>>("/api/v1/forms?page=1&pageSize=50");
        Assert.Contains(list!.Items, f => f.Id == created.Id);
    }

    [IntegrationConnectionFact]
    public async Task Workflow_submit_review_approve_and_decisions()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var reviewer = _factory.CreateAuthenticatedClient("forms-reviewer");
        var approver = _factory.CreateAuthenticatedClient("forms-approver");

        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());
        form = await PostTransitionAsync(designer, $"/api/v1/forms/{form.Id}/submit-review", form.RowVersion, "إرسال للمراجعة");
        Assert.Equal(FormDefinitionStatus.InReview, form.Status);

        form = await PostTransitionAsync(reviewer, $"/api/v1/forms/{form.Id}/request-changes", form.RowVersion, "تعديلات مطلوبة");
        Assert.Equal(FormDefinitionStatus.ChangesRequested, form.Status);

        form = await PostTransitionAsync(designer, $"/api/v1/forms/{form.Id}/submit-review", form.RowVersion, "إعادة الإرسال");
        Assert.Equal(FormDefinitionStatus.InReview, form.Status);

        form = await PostTransitionAsync(approver, $"/api/v1/forms/{form.Id}/approve", form.RowVersion, "اعتماد");
        Assert.Equal(FormDefinitionStatus.Approved, form.Status);

        var decisions = await approver.GetFromJsonAsync<List<FormReviewDecisionItem>>($"/api/v1/forms/{form.Id}/review-decisions");
        Assert.NotNull(decisions);
        Assert.Contains(decisions!, d => d.Decision == FormReviewDecisionType.Approve);
        Assert.Contains(decisions!, d => d.Decision == FormReviewDecisionType.RequestChanges);
    }

    [IntegrationConnectionFact]
    public async Task Reject_and_archive_flow()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var reviewer = _factory.CreateAuthenticatedClient("forms-reviewer");
        var admin = _factory.CreateAuthenticatedClient("forms-admin");

        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());
        form = await PostTransitionAsync(designer, $"/api/v1/forms/{form.Id}/submit-review", form.RowVersion, "تقديم");
        form = await PostTransitionAsync(reviewer, $"/api/v1/forms/{form.Id}/reject", form.RowVersion, "رفض");
        Assert.Equal(FormDefinitionStatus.Rejected, form.Status);

        var archive = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/archive", new
        {
            reason = "أرشفة بعد الرفض",
            rowVersion = form.RowVersion
        });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var archived = await db.FormDefinitions.SingleAsync(f => f.Id == form.Id);
        Assert.Equal(FormDefinitionStatus.Archived, archived.Status);

        var restore = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/restore", new
        {
            reason = "استعادة",
            rowVersion = Convert.ToBase64String(archived.RowVersion)
        });
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);
        var restored = await db.FormDefinitions.SingleAsync(f => f.Id == form.Id);
        Assert.Equal(FormDefinitionStatus.Rejected, restored.Status);
    }

    [IntegrationConnectionFact]
    public async Task Invalid_transition_returns_409()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var approver = _factory.CreateAuthenticatedClient("forms-approver");
        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());

        // Approver has Forms.Approve but Draft→Approved is illegal — expect state-machine 409.
        var response = await approver.PostAsJsonAsync($"/api/v1/forms/{form.Id}/approve", new
        {
            reason = "انتقال غير صالح",
            rowVersion = form.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Stale_rowversion_returns_409()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());
        var stale = form.RowVersion;

        var first = await UpdateFormAsync(designer, form.Id, form.RowVersion, "تحديث أول");
        Assert.Equal("تحديث أول", first.NameAr);

        var second = await designer.PutAsJsonAsync($"/api/v1/forms/{form.Id}", new
        {
            nameAr = "تحديث متأخر",
            nameEn = (string?)null,
            description = "وصف",
            classification = ClassificationLevel.Internal,
            ownerDepartmentId = (Guid?)null,
            rowVersion = stale
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Duplicate_code_returns_409()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var code = NextCode();
        await CreateFormAsync(designer, ScopeType.Global, null, null, code);

        var response = await designer.PostAsJsonAsync("/api/v1/forms", new
        {
            code,
            nameAr = "نموذج مكرر",
            nameEn = (string?)null,
            description = "وصف",
            classification = ClassificationLevel.Internal,
            scopeType = ScopeType.Global,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            facilityUnitId = (Guid?)null,
            ownerDepartmentId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Validation_error_returns_400()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var response = await designer.PostAsJsonAsync("/api/v1/forms", new
        {
            code = "   ",
            nameAr = " ",
            description = " ",
            classification = ClassificationLevel.Internal,
            scopeType = ScopeType.Global,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            facilityUnitId = (Guid?)null,
            ownerDepartmentId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Missing_permission_returns_403()
    {
        await _factory.SeedUserAsync("forms-no-perm", "بدون صلاحية", [RoleCodes.ReadOnlyUser],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("forms-no-perm");
        var response = await client.GetAsync("/api/v1/forms");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Creator_cannot_review_own_form_sod()
    {
        await SeedWorkflowUsersAsync();
        await _factory.SeedUserWithPermissionsAsync(
            "forms-designer-reviewer",
            "منشئ مراجع",
            [RoleCodes.FormDesigner],
            [PermissionCodes.FormsRequestChanges],
            (ScopeType.Global, null, null));

        var designer = _factory.CreateAuthenticatedClient("forms-designer-reviewer");
        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());
        form = await PostTransitionAsync(designer, $"/api/v1/forms/{form.Id}/submit-review", form.RowVersion, "تقديم");

        var response = await designer.PostAsJsonAsync($"/api/v1/forms/{form.Id}/request-changes", new
        {
            reason = "محاولة مراجعة ذاتية",
            rowVersion = form.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Creator_cannot_approve_after_submit_sod()
    {
        await SeedWorkflowUsersAsync();
        await _factory.SeedUserWithPermissionsAsync(
            "forms-designer-approver",
            "منشئ معتمد",
            [RoleCodes.FormDesigner],
            [PermissionCodes.FormsApprove],
            (ScopeType.Global, null, null));

        var client = _factory.CreateAuthenticatedClient("forms-designer-approver");
        var form = await CreateFormAsync(client, ScopeType.Global, null, null, NextCode());
        form = await PostTransitionAsync(client, $"/api/v1/forms/{form.Id}/submit-review", form.RowVersion, "تقديم");

        var response = await client.PostAsJsonAsync($"/api/v1/forms/{form.Id}/approve", new
        {
            reason = "محاولة اعتماد ذاتي",
            rowVersion = form.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Sensitive_form_is_redacted_without_view_sensitive_permission()
    {
        await SeedWorkflowUsersAsync();
        var admin = _factory.CreateAuthenticatedClient("forms-admin");
        var viewer = _factory.CreateAuthenticatedClient("forms-viewer");

        var created = await CreateFormAsync(
            admin,
            ScopeType.Global,
            null,
            null,
            NextCode(),
            ClassificationLevel.Confidential,
            "نموذج سري");

        var detail = await viewer.GetFromJsonAsync<FormDetail>($"/api/v1/forms/{created.Id}");
        Assert.NotNull(detail);
        Assert.True(detail!.IsSensitiveRedacted);
        Assert.Equal("[محجوب]", detail.NameAr);
        Assert.Equal("[محتوى حساس — يتطلب صلاحية عرض]", detail.Description);
    }

    [IntegrationConnectionFact]
    public async Task Deny_grant_blocks_form_view()
    {
        await SeedWorkflowUsersAsync();
        var admin = _factory.CreateAuthenticatedClient("forms-admin");
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var viewer = _factory.CreateAuthenticatedClient("forms-viewer");
        // Create as designer so admin is not the form creator (SoD blocks creator grants).
        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());

        Guid viewerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            viewerId = await db.Users.Where(u => u.ExternalSubject == "forms-viewer").Select(u => u.Id).FirstAsync();
        }

        var grantResponse = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/access-grants", new
        {
            principalType = FormAccessGrantPrincipalType.User,
            principalId = viewerId,
            capability = FormAccessCapability.View,
            effect = FormAccessGrantEffect.Deny,
            scopeType = (ScopeType?)null,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            validFromUtc = (DateTimeOffset?)null,
            validToUtc = (DateTimeOffset?)null,
            reason = "منع العرض"
        });
        Assert.Equal(HttpStatusCode.Created, grantResponse.StatusCode);

        var detail = await viewer.GetAsync($"/api/v1/forms/{form.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Expired_deny_grant_allows_view()
    {
        await SeedWorkflowUsersAsync();
        var admin = _factory.CreateAuthenticatedClient("forms-admin");
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var viewer = _factory.CreateAuthenticatedClient("forms-viewer");
        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());

        Guid viewerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            viewerId = await db.Users.Where(u => u.ExternalSubject == "forms-viewer").Select(u => u.Id).FirstAsync();
        }

        var grantResponse = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/access-grants", new
        {
            principalType = FormAccessGrantPrincipalType.User,
            principalId = viewerId,
            capability = FormAccessCapability.View,
            effect = FormAccessGrantEffect.Deny,
            scopeType = (ScopeType?)null,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            validFromUtc = (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(-2),
            validToUtc = (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(-1),
            reason = "منع منتهي"
        });
        Assert.Equal(HttpStatusCode.Created, grantResponse.StatusCode);

        var detail = await viewer.GetFromJsonAsync<FormDetail>($"/api/v1/forms/{form.Id}");
        Assert.NotNull(detail);
        Assert.Equal(form.Id, detail!.Id);
    }

    [IntegrationConnectionFact]
    public async Task Audit_logs_are_written_for_create_and_approve()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var approver = _factory.CreateAuthenticatedClient("forms-approver");

        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());
        form = await PostTransitionAsync(designer, $"/api/v1/forms/{form.Id}/submit-review", form.RowVersion, "تقديم");
        form = await PostTransitionAsync(approver, $"/api/v1/forms/{form.Id}/approve", form.RowVersion, "اعتماد");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == form.Id.ToString() && a.Action == "FormCreated"));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == form.Id.ToString() && a.Action == "FormApproved"));
    }

    [IntegrationConnectionFact]
    public async Task Soft_deleted_form_is_hidden_from_api()
    {
        await SeedWorkflowUsersAsync();
        var admin = _factory.CreateAuthenticatedClient("forms-admin");
        var form = await CreateFormAsync(admin, ScopeType.Global, null, null, NextCode());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var entity = await db.FormDefinitions.SingleAsync(f => f.Id == form.Id);
            entity.IsDeleted = true;
            entity.DeletedAtUtc = DateTimeOffset.UtcNow;
            entity.DeletedBy = "test";
            await db.SaveChangesAsync();
        }

        var detail = await admin.GetAsync($"/api/v1/forms/{form.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Filtered_unique_code_allows_reuse_after_soft_delete()
    {
        await SeedWorkflowUsersAsync();
        var admin = _factory.CreateAuthenticatedClient("forms-admin");
        var code = NextCode();
        var first = await CreateFormAsync(admin, ScopeType.Global, null, null, code);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var entity = await db.FormDefinitions.IgnoreQueryFilters().SingleAsync(f => f.Id == first.Id);
            entity.IsDeleted = true;
            entity.DeletedAtUtc = DateTimeOffset.UtcNow;
            entity.DeletedBy = "test";
            await db.SaveChangesAsync();
        }

        var second = await CreateFormAsync(admin, ScopeType.Global, null, null, code);
        Assert.Equal(code.ToUpperInvariant(), second.Code);
        Assert.NotEqual(first.Id, second.Id);
    }

    [IntegrationConnectionFact]
    public async Task Retention_status_reflects_approved_anchor()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var approver = _factory.CreateAuthenticatedClient("forms-approver");

        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());
        form = await PostTransitionAsync(designer, $"/api/v1/forms/{form.Id}/submit-review", form.RowVersion, "تقديم");
        form = await PostTransitionAsync(approver, $"/api/v1/forms/{form.Id}/approve", form.RowVersion, "اعتماد");

        var retention = await approver.GetFromJsonAsync<FormRetentionStatusItem>($"/api/v1/forms/{form.Id}/retention-status");
        Assert.NotNull(retention);
        Assert.True(retention!.IsRetentionApplicable);
        Assert.NotNull(retention.RetentionAnchorUtc);
        Assert.Equal(365, retention.RetentionDays);
        Assert.False(retention.IsEligibleForArchive);
    }

    [IntegrationConnectionFact]
    public async Task Approved_archive_before_retention_expires_returns_409()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var approver = _factory.CreateAuthenticatedClient("forms-approver");
        var admin = _factory.CreateAuthenticatedClient("forms-admin");

        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());
        form = await PostTransitionAsync(designer, $"/api/v1/forms/{form.Id}/submit-review", form.RowVersion, "تقديم");
        form = await PostTransitionAsync(approver, $"/api/v1/forms/{form.Id}/approve", form.RowVersion, "اعتماد");

        var archive = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/archive", new
        {
            reason = "محاولة أرشفة مبكرة",
            rowVersion = form.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, archive.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Creator_cannot_grant_access_on_own_form_sod()
    {
        await SeedWorkflowUsersAsync();
        await _factory.SeedUserWithPermissionsAsync(
            "forms-designer-access",
            "منشئ وصول",
            [RoleCodes.FormDesigner],
            [PermissionCodes.FormsManageAccess],
            (ScopeType.Global, null, null));

        var designer = _factory.CreateAuthenticatedClient("forms-designer-access");
        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());

        Guid roleId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            roleId = await db.Roles.Where(r => r.Code == RoleCodes.FormReviewer).Select(r => r.Id).FirstAsync();
        }

        var response = await designer.PostAsJsonAsync($"/api/v1/forms/{form.Id}/access-grants", new
        {
            principalType = FormAccessGrantPrincipalType.Role,
            principalId = roleId,
            capability = FormAccessCapability.Review,
            effect = FormAccessGrantEffect.Allow,
            scopeType = (ScopeType?)null,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            validFromUtc = (DateTimeOffset?)null,
            validToUtc = (DateTimeOffset?)null,
            reason = "محاولة منح ذاتي"
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Design_deny_grant_blocks_update_and_submit()
    {
        await SeedWorkflowUsersAsync();
        var admin = _factory.CreateAuthenticatedClient("forms-admin");
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());

        Guid designerUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            designerUserId = await db.Users.Where(u => u.ExternalSubject == "forms-designer").Select(u => u.Id).FirstAsync();
        }

        var grantResponse = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/access-grants", new
        {
            principalType = FormAccessGrantPrincipalType.User,
            principalId = designerUserId,
            capability = FormAccessCapability.Design,
            effect = FormAccessGrantEffect.Deny,
            scopeType = (ScopeType?)null,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            validFromUtc = (DateTimeOffset?)null,
            validToUtc = (DateTimeOffset?)null,
            reason = "منع التصميم"
        });
        Assert.Equal(HttpStatusCode.Created, grantResponse.StatusCode);

        var detail = await designer.GetFromJsonAsync<FormDetailWithActions>($"/api/v1/forms/{form.Id}");
        Assert.NotNull(detail);
        Assert.DoesNotContain("UpdateDraft", detail!.AllowedActions);
        Assert.DoesNotContain("SubmitForReview", detail.AllowedActions);

        var update = await designer.PutAsJsonAsync($"/api/v1/forms/{form.Id}", new
        {
            nameAr = "محاولة تحديث",
            nameEn = (string?)null,
            description = "وصف",
            classification = ClassificationLevel.Internal,
            ownerDepartmentId = (Guid?)null,
            rowVersion = form.RowVersion
        });
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

        var submit = await designer.PostAsJsonAsync($"/api/v1/forms/{form.Id}/submit-review", new
        {
            reason = "محاولة إرسال",
            rowVersion = form.RowVersion
        });
        Assert.Equal(HttpStatusCode.Forbidden, submit.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Restore_returns_rejected_prior_status_and_revokes_via_post()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var reviewer = _factory.CreateAuthenticatedClient("forms-reviewer");
        var admin = _factory.CreateAuthenticatedClient("forms-admin");

        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());
        form = await PostTransitionAsync(designer, $"/api/v1/forms/{form.Id}/submit-review", form.RowVersion, "إرسال");
        form = await PostTransitionAsync(reviewer, $"/api/v1/forms/{form.Id}/reject", form.RowVersion, "رفض");
        var archive = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/archive", new
        {
            reason = "أرشفة مرفوض",
            rowVersion = form.RowVersion
        });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        string archivedRowVersion;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var entity = await db.FormDefinitions.IgnoreQueryFilters().FirstAsync(f => f.Id == form.Id);
            Assert.Equal(FormDefinitionStatus.Archived, entity.Status);
            archivedRowVersion = Convert.ToBase64String(entity.RowVersion);
        }

        var restore = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/restore", new
        {
            reason = "استعادة مرفوض",
            rowVersion = archivedRowVersion
        });
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);

        var restored = await admin.GetFromJsonAsync<FormDetail>($"/api/v1/forms/{form.Id}");
        Assert.Equal(FormDefinitionStatus.Rejected, restored!.Status);

        Guid roleId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            roleId = await db.Roles.Where(r => r.Code == RoleCodes.FormAnalyst).Select(r => r.Id).FirstAsync();
        }

        var createGrant = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/access-grants", new
        {
            principalType = FormAccessGrantPrincipalType.Role,
            principalId = roleId,
            capability = FormAccessCapability.View,
            effect = FormAccessGrantEffect.Allow,
            scopeType = (ScopeType?)null,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            validFromUtc = (DateTimeOffset?)null,
            validToUtc = (DateTimeOffset?)null,
            reason = "منح عرض"
        });
        Assert.Equal(HttpStatusCode.Created, createGrant.StatusCode);
        var grant = await createGrant.Content.ReadFromJsonAsync<FormAccessGrantItem>(JsonOptions);
        Assert.NotNull(grant);

        var revoke = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/access-grants/{grant!.Id}/revoke", new
        {
            reason = "إلغاء المنح",
            rowVersion = grant.RowVersion
        });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var revokeAgain = await admin.PostAsJsonAsync($"/api/v1/forms/{form.Id}/access-grants/{grant.Id}/revoke", new
        {
            reason = "إعادة إلغاء",
            rowVersion = grant.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, revokeAgain.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Missing_or_invalid_rowversion_returns_400()
    {
        await SeedWorkflowUsersAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-designer");
        var form = await CreateFormAsync(designer, ScopeType.Global, null, null, NextCode());

        var missing = await designer.PutAsJsonAsync($"/api/v1/forms/{form.Id}", new
        {
            nameAr = "بدون إصدار",
            nameEn = (string?)null,
            description = "وصف",
            classification = ClassificationLevel.Internal,
            ownerDepartmentId = (Guid?)null,
            rowVersion = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var invalid = await designer.PutAsJsonAsync($"/api/v1/forms/{form.Id}", new
        {
            nameAr = "إصدار باطل",
            nameEn = (string?)null,
            description = "وصف",
            classification = ClassificationLevel.Internal,
            ownerDepartmentId = (Guid?)null,
            rowVersion = "!!!not-base64!!!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private async Task SeedWorkflowUsersAsync()
    {
        await _factory.SeedUserAsync("forms-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("forms-designer", "مصمم", [RoleCodes.FormDesigner],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("forms-reviewer", "مراجع", [RoleCodes.FormReviewer],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("forms-approver", "معتمد", [RoleCodes.FormApprover],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("forms-viewer", "عارض", [RoleCodes.FormAnalyst],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("forms-region-a", "منطقة أ", [RoleCodes.FormDesigner, RoleCodes.FormReviewer],
            (ScopeType.Region, SeedIds.RegionA, null));
        await _factory.SeedUserAsync("forms-facility-a1", "سجن أ1", [RoleCodes.FormDesigner],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
    }

    private static string NextCode() => $"TFR{Interlocked.Increment(ref _codeSequence):D4}";

    private static async Task<FormDetail> CreateFormAsync(
        HttpClient client,
        ScopeType scopeType,
        Guid? regionId,
        Guid? facilityId,
        string code,
        ClassificationLevel classification = ClassificationLevel.Internal,
        string nameAr = "نموذج تجريبي")
    {
        var response = await client.PostAsJsonAsync("/api/v1/forms", new
        {
            code,
            nameAr,
            nameEn = (string?)null,
            description = "وصف تفصيلي للنموذج التجريبي",
            classification,
            scopeType,
            regionId,
            facilityId,
            facilityUnitId = (Guid?)null,
            ownerDepartmentId = (Guid?)null
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<FormDetail>(body, JsonOptions)!;
    }

    private static async Task<FormDetail> UpdateFormAsync(
        HttpClient client,
        Guid id,
        string rowVersion,
        string nameAr)
    {
        var response = await client.PutAsJsonAsync($"/api/v1/forms/{id}", new
        {
            nameAr,
            nameEn = (string?)null,
            description = "وصف محدث",
            classification = ClassificationLevel.Internal,
            ownerDepartmentId = (Guid?)null,
            rowVersion
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<FormDetail>(body, JsonOptions)!;
    }

    private static async Task<FormDetail> PostTransitionAsync(
        HttpClient client,
        string url,
        string rowVersion,
        string reason)
    {
        var response = await client.PostAsJsonAsync(url, new { reason, rowVersion });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<FormDetail>(body, JsonOptions)!;
    }
}

internal sealed record FormListItem(Guid Id, string Code, string NameAr, FormDefinitionStatus Status, bool IsSensitiveRedacted);
internal sealed record FormDetail(
    Guid Id,
    string Code,
    string NameAr,
    string Description,
    FormDefinitionStatus Status,
    ClassificationLevel Classification,
    string RowVersion,
    bool IsSensitiveRedacted);
internal sealed record FormDetailWithActions(
    Guid Id,
    string Code,
    FormDefinitionStatus Status,
    string RowVersion,
    IReadOnlyList<string> AllowedActions);
internal sealed record FormAccessGrantItem(Guid Id, string RowVersion);
internal sealed record FormReviewDecisionItem(FormReviewDecisionType Decision, FormDefinitionStatus ToStatus);
internal sealed record FormRetentionStatusItem(
    Guid FormDefinitionId,
    bool IsRetentionApplicable,
    DateTimeOffset? RetentionAnchorUtc,
    int RetentionDays,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired,
    bool IsEligibleForArchive);
