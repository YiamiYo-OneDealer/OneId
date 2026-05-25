using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OneId.SampleClient.Helpers;

namespace OneId.SampleClient.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString(SessionKeys.AccessToken) is not null)
            return RedirectToPage("/Claims");

        return RedirectToPage("/Login");
    }
}
