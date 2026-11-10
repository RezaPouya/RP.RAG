using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using RP.Rag.Application.Models;
using RP.Rag.Infrastructure.Data;
using RP.Rag.Web.Components;
using RP.Rag.Web.Middleware;
using RP.Rag.Web.Services;
using RP.RAG.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. Services
// ============================================================

builder.Services.Configure<List<AppUser>>(builder.Configuration.GetSection("AppUsers"));
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "RP.RAG.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// ============================================================
// 2. Auto migration (Development only)
// ============================================================
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RpRagDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Auto migration failed. Run 'dotnet ef database update' manually.");
    }
}

// ============================================================
// 3. Middleware Pipeline
// ============================================================

// Custom exception middleware MUST be first
app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// ============================================================
// 4. Authentication Endpoints (NEW HTTP requests, no header issues)
// ============================================================

// Login endpoint: receives the REAL form POST from Login.razor
app.MapPost("/auth/login", async (HttpContext context, AuthService auth) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var user = auth.Validate(username, password);

    if (user is null)
    {
        return Results.Redirect("/login?error=" + Uri.EscapeDataString("Invalid username or password"));
    }

    await auth.SignInAsync(user, context);
    return Results.Redirect("/");
})
.DisableAntiforgery(); // Disable antiforgery for this simple login form

// Logout endpoint
app.MapPost("/auth/logout", async (HttpContext context) =>
{
    await AuthService.SignOutAsync(context);
    return Results.Redirect("/login");
})
.DisableAntiforgery();

// ============================================================
// 5. Razor Components
// ============================================================
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();