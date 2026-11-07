using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using RP.Rag.Application.Models;
using RP.Rag.Infrastructure.Data;
using RP.Rag.Web.Components;
using RP.Rag.Web.Services;
using RP.RAG.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. پیکربندی سرویس‌ها
// ============================================================

// خواندن لیست کاربران از appsettings.json
builder.Services.Configure<List<AppUser>>(builder.Configuration.GetSection("AppUsers"));

// ثبت سرویس‌های لایه Infrastructure (EF Core, Elasticsearch, Ollama, Laya, ...)
builder.Services.AddInfrastructure(builder.Configuration);

// احراز هویت مبتنی بر کوکی (Cookie Authentication)
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

// برای دسترسی به HttpContext درون کامپوننت‌های Blazor (مثلاً در Logout)
builder.Services.AddHttpContextAccessor();

// سرویس سفارشی لاگین / لاگ‌اوت
builder.Services.AddScoped<AuthService>();

// AuthenticationStateProvider برای کار کردن <AuthorizeView> و @attribute [Authorize]
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// پشتیبانی از Cascade Authentication State در کل درخت کامپوننت‌ها
builder.Services.AddCascadingAuthenticationState();

// Blazor Razor Components با حالت Interactive Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ============================================================
// 2. ساخت اپلیکیشن
// ============================================================
var app = builder.Build();

// ============================================================
// 3. اعمال Migration در محیط Development (اختیاری ولی مفید)
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
        logger.LogWarning(ex, "Automatic migration failed. You may need to run 'dotnet ef database update' manually.");
    }
}

// ============================================================
// 4. پیکربندی Pipeline میان‌افزارها (Middleware)
// ============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// ضد جعل (Antiforgery) برای فرم‌ها و درخواست‌های Blazor
app.UseAntiforgery();

// ترتیب احراز هویت و مجوزدهی بسیار مهم است
app.UseAuthentication();
app.UseAuthorization();

// ============================================================
// 5. Map کردن Razor Components
// ============================================================
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ============================================================
// 6. اجرای اپلیکیشن
// ============================================================
app.Run();