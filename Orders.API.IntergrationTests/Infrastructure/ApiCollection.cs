namespace Orders.API.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<OrdersApiFactory>
{
    public const string Name = "api";
}