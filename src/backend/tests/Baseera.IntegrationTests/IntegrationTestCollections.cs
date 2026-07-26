namespace Baseera.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class CoreIntegrationCollection
    : ICollectionFixture<CoreIntegrationFixture>
{
    public const string Name = "integration-core";
}

[CollectionDefinition(Name)]
public sealed class FormsIntegrationCollection
    : ICollectionFixture<FormsIntegrationFixture>
{
    public const string Name = "integration-forms";
}

[CollectionDefinition(Name)]
public sealed class OperationsIntegrationCollection
    : ICollectionFixture<OperationsIntegrationFixture>
{
    public const string Name = "integration-operations";
}

[CollectionDefinition(Name)]
public sealed class WorkforceIntegrationCollection
    : ICollectionFixture<WorkforceIntegrationFixture>
{
    public const string Name = "integration-workforce";
}

public sealed class CoreIntegrationFixture()
    : SharedIntegrationFixture("Core");

public sealed class FormsIntegrationFixture()
    : SharedIntegrationFixture("Forms");

public sealed class OperationsIntegrationFixture()
    : SharedIntegrationFixture("Operations");

public sealed class WorkforceIntegrationFixture()
    : SharedIntegrationFixture("Workforce");
