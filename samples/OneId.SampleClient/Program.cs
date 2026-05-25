var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".OneId.Sample.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

var idpBaseUrl = builder.Configuration["Idp:BaseUrl"] ?? "http://localhost:5248";
builder.Services.AddHttpClient("idp", c => c.BaseAddress = new Uri(idpBaseUrl));

var app = builder.Build();

app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.MapRazorPages();

await app.RunAsync();
