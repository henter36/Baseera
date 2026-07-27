namespace Baseera.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class RiskManagementIntegrationCollection
    : ICollectionFixture<RiskManagementIntegrationFixture>
{
    public const string Name = "integration-risk-management";
}

public sealed class RiskManagementIntegrationFixture()
    : SharedIntegrationFixture("RiskManagement");
