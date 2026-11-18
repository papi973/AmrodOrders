using System.Net;
using System.Net.Http.Json;
using Orders.API.Auth;
using Orders.API.Contracts;
using Orders.API.IntegrationTests.Infrastructure;

namespace Orders.API.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class CustomersApiTests(OrdersApiFactory factory)
{
    private readonly HttpClient _writer = factory.CreateClientWithRoles(AuthPolicies.Read, AuthPolicies.Write);

    [Fact]
    public async Task Create_then_get_returns_the_customer()
    {
        var email = $"{Guid.NewGuid():N}@acme.co.za";

        var create = await _writer.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest("Acme Trading", email, "za"), TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CustomerResponse>(TestJson.Options);
        Assert.Equal("ZA", created!.CountryCode); // normalised

        var fetched = await _writer.GetFromJsonAsync<CustomerResponse>($"/api/customers/{created.Id}", TestJson.Options);
        Assert.Equal(email, fetched!.Email);
    }

    [Fact]
    public async Task Non_SADC_country_returns_400_validation_problem()
    {
        var response = await _writer.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest("Nairobi Ltd", $"{Guid.NewGuid():N}@x.co.ke", "KE"), TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Duplicate_email_returns_409()
    {
        var email = $"{Guid.NewGuid():N}@acme.co.za";
        await _writer.PostAsJsonAsync("/api/customers", new CreateCustomerRequest("A", email, "ZA"), TestJson.Options);

        var duplicate = await _writer.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest("B", email.ToUpperInvariant(), "ZA"), TestJson.Options);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task PageSize_over_100_returns_400()
    {
        var response = await _writer.GetAsync("/api/customers?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_gets_401_and_reader_cannot_write_403()
    {
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/customers")).StatusCode);

        var reader = factory.CreateClientWithRoles(AuthPolicies.Read);
        var response = await reader.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest("Nope", $"{Guid.NewGuid():N}@x.co.za", "ZA"), TestJson.Options);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}