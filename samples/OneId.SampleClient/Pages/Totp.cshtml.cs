using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneId.SampleClient.Helpers;
using System.Text.Json;

namespace OneId.SampleClient.Pages;

public class TotpModel(IHttpClientFactory httpClientFactory, IConfiguration configuration) : PageModel
{
    [BindProperty]
    public string Code { get; set; } = string.Empty;

    public string? EnrollmentUri { get; set; }
    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString(SessionKeys.MfaSessionToken) is null)
            return RedirectToPage("/Login");

        EnrollmentUri = HttpContext.Session.GetString(SessionKeys.TotpEnrollmentUri);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var mfaSessionToken = HttpContext.Session.GetString(SessionKeys.MfaSessionToken);
        if (mfaSessionToken is null)
            return RedirectToPage("/Login");

        var clientId = configuration["Idp:ClientId"] ?? "oneid-dev-client";
        var client = httpClientFactory.CreateClient("idp");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "urn:oneid:mfa",
            ["client_id"] = clientId,
            ["mfa_session_token"] = mfaSessionToken,
            ["totp_code"] = Code,
        };

        var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form), ct);
        var bodyJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(bodyJson);
        var body = doc.RootElement;

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = body.TryGetProperty("error_description", out var ed)
                ? ed.GetString()
                : $"Verification failed ({(int)response.StatusCode}).";
            EnrollmentUri = HttpContext.Session.GetString(SessionKeys.TotpEnrollmentUri);
            return Page();
        }

        if (body.TryGetProperty("access_token", out var at))
        {
            HttpContext.Session.SetString(SessionKeys.AccessToken, at.GetString()!);
            HttpContext.Session.Remove(SessionKeys.MfaSessionToken);
            HttpContext.Session.Remove(SessionKeys.TotpEnrollmentUri);
            return RedirectToPage("/Claims");
        }

        ErrorMessage = "Unexpected response from the identity provider.";
        EnrollmentUri = HttpContext.Session.GetString(SessionKeys.TotpEnrollmentUri);
        return Page();
    }
}
