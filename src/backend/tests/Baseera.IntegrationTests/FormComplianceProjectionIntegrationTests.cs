using System.Net;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;

namespace Baseera.IntegrationTests;

public sealed class FormComplianceProjectionIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;

    public FormComplianceProjectionIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task FormCompliance_source_projection_endpoints_translate()
    {
        var subject = $"form-compliance-{Guid.NewGuid():N}";
        await _factory.SeedUserAsync(
            subject,
            "مستخدم لوحة الالتزام",
            [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        var client = _factory.CreateAuthenticatedClient(subject);
        var paths = new[]
        {
            "/api/v1/form-compliance/facilities?page=1&pageSize=20",
            "/api/v1/form-compliance/pending?page=1&pageSize=20",
            "/api/v1/form-compliance/export.csv?view=Facilities"
        };

        foreach (var path in paths)
        {
            var response = await client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{path} returned {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }
}
