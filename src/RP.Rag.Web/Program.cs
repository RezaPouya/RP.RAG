using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using RP.Rag.Application.Models;
using RP.Rag.Web.Services;
using RP.RAG.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<List<AppUser>>(builder.Configuration.GetSection("AppUsers"));

builder.Services.AddInfrastructure(builder.Configuration);

// احراز هویت با کوکی
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// سرویس احراز هویت سفارشی
builder.Services.AddScoped<AuthService>();

// این خط برای کار کردن <AuthorizeView> و @attribute [Authorize] الزامی است
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// ... ادامه کد