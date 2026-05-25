using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneId.SampleClient.Helpers;
using System.Text.Json;

namespace OneId.SampleClient.Pages;

public class LoginModel(IHttpClientFactory httpClientFactory, IConfiguration configuration) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString(SessionKeys.AccessToken) is not null)
            return RedirectToPage("/Claims");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var clientId = configuration["Idp:ClientId"] ?? "oneid-dev-client";
        var client = httpClientFactory.CreateClient("idp");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = Email,
            ["password"] = Password,
            ["scope"] = "openid email profile roles offline_access",
        };

        var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form), ct);
        var bodyJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(bodyJson);
        var body = doc.RootElement;

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = body.TryGetProperty("error_description", out var ed)
                ? ed.GetString()
                : $"Login failed ({(int)response.StatusCode}).";
            return Page();
        }

        // MFA step required
        if (body.TryGetProperty("mfa_required", out var mfa) && mfa.GetBoolean())
        {
            var sessionToken = body.GetProperty("mfa_session_token").GetString()!;
            HttpContext.Session.SetString(SessionKeys.MfaSessionToken, sessionToken);

            if (body.TryGetProperty("totp_enrollment_uri", out var uri))
                HttpContext.Session.SetString(SessionKeys.TotpEnrollmentUri, uri.GetString()!);

            return RedirectToPage("/Totp");
        }

        // Direct token (password grant without MFA — should not reach here in current IDP config)
        if (body.TryGetProperty("access_token", out var at))
        {
            HttpContext.Session.SetString(SessionKeys.AccessToken, at.GetString()!);
            return RedirectToPage("/Claims");
        }

        ErrorMessage = "Unexpected response from the identity provider.";
        return Page();
    }
}
