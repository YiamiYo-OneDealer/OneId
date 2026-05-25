using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.IntegrationTests.Helpers;
using OpenIddict.Abstractions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OneId.Server.IntegrationTests.OpenIddict;

[Collection("IntegrationTests")]
public class OpenIddictConfigurationTests(OneIdWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task DiscoveryEndpoint_Returns200_WithRequiredFields()
    {
        var response = await Client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType?.MediaType);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(doc.TryGetProperty("issuer", out _));
        Assert.True(doc.TryGetProperty("authorization_endpoint", out _));
        Assert.True(doc.TryGetProperty("token_endpoint", out _));
        Assert.True(doc.TryGetProperty("jwks_uri", out _));
        Assert.True(doc.TryGetProperty("introspection_endpoint", out _));

        var scopes = doc.GetProperty("scopes_supported").EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        Assert.Contains("openid", scopes);

        var responseTypes = doc.GetProperty("response_types_supported").EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        Assert.Contains("code", responseTypes);

        Assert.True(doc.TryGetProperty("token_endpoint_auth_methods_supported", out _));
    }

    [Fact]
    public async Task JwksEndpoint_ReturnsRS256Key()
    {
        var discoveryDoc = await Client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration");
        var jwksUri = discoveryDoc.GetProperty("jwks_uri").GetString()!;

        var jwksResponse = await Client.GetAsync(jwksUri);
        Assert.Equal(HttpStatusCode.OK, jwksResponse.StatusCode);

        var jwks = await jwksResponse.Content.ReadFromJsonAsync<JsonElement>();
        var keys = jwks.GetProperty("keys").EnumerateArray().ToList();
        Assert.NotEmpty(keys);
        Assert.Equal("RSA", keys[0].GetProperty("kty").GetString());
        Assert.True(keys[0].TryGetProperty("n", out _), "RSA modulus 'n' must be present in JWKS");
        Assert.True(keys[0].TryGetProperty("e", out _), "RSA exponent 'e' must be present in JWKS");
    }

    [Fact]
    public void DependencyInjection_ITenantContext_AndOpenIddict_BothResolvable()
    {
        // AR-5: Both must be resolvable; ITenantContext registered before AddOpenIddict() in Program.cs.
        // If this throws, Program.cs registration order is broken.
        using var scope = Factory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetService<ITenantContext>();
        var oidcManager = scope.ServiceProvider.GetService<IOpenIddictApplicationManager>();

        Assert.NotNull(tenantContext);  // ITenantContext must be registered
        Assert.NotNull(oidcManager);    // OpenIddict must be registered
    }
}
