namespace AeroCompliance.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ApiTestCollection : ICollectionFixture<ApiTestFixture>
{
    public const string Name = "AeroCompliance API integration";
}
