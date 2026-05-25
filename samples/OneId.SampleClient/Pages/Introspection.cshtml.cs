using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneId.SampleClient.Helpers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OneId.SampleClient.Pages;

public class IntrospectionModel(IHttpClientFactory httpClientFactory, IConfiguration configuration) : PageModel
{
    public string? ResultJson { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsActive { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var token = HttpContext.Session.GetString(SessionKeys.AccessToken);
        if (token is null)
            return RedirectToPage("/Login");

        var clientId = configuration["Idp:IntrospectionClientId"] ?? "oneid-sample-app";
        var clientSecret = configuration["Idp:IntrospectionClientSecret"] ?? "sample-app-secret";

        var client = httpClientFactory.CreateClient("idp");

        // Confidential client authentication via HTTP Basic (RFC 7617)
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var form = new Dictionary<string, string>
        {
            ["token"] = token,
            ["token_type_hint"] = "access_token",
        };

        try
        {
            var response = await client.PostAsync("/connect/introspect", new FormUrlEncodedContent(form), ct);
            var bodyJson = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                using var errDoc = JsonDocument.Parse(bodyJson);
                ErrorMessage = errDoc.RootElement.TryGetProperty("error_description", out var ed)
                    ? ed.GetString()
                    : $"The introspection endpoint returned HTTP {(int)response.StatusCode}.";
                return Page();
            }

            // Pretty-print the JSON response
            using var doc = JsonDocument.Parse(bodyJson);
            ResultJson = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });

            IsActive = doc.RootElement.TryGetProperty("active", out var active) && active.GetBoolean();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Could not reach the identity provider: {ex.Message}";
        }
        catch (JsonException)
        {
            ErrorMessage = "The introspection endpoint returned a non-JSON response.";
        }

        return Page();
    }
}
