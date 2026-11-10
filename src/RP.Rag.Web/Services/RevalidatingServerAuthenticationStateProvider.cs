using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace RP.Rag.Web.Services;

// نام کلاس را تغییر دادیم تا با کلاس پایه تداخل نداشته باشد
public sealed class CustomAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    public CustomAuthenticationStateProvider(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // بررسی ساده اینکه آیا کاربر احراز هویت شده است یا خیر
        return Task.FromResult(authenticationState?.User?.Identity?.IsAuthenticated ?? false);
    }
}