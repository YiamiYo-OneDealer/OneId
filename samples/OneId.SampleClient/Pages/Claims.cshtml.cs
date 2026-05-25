using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneId.SampleClient.Helpers;

namespace OneId.SampleClient.Pages;

public class ClaimsModel : PageModel
{
    public List<(string Name, string Value)> Claims { get; set; } = [];
    public string? RawToken { get; set; }

    public IActionResult OnGet()
    {
        var token = HttpContext.Session.GetString(SessionKeys.AccessToken);
        if (token is null)
            return RedirectToPage("/Login");

        RawToken = token;
        Claims = JwtHelper.DecodePayloadClaims(token);
        return Page();
    }

    public IActionResult OnPostLogout()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Login");
    }
}
