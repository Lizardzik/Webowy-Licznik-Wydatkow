using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Middleware
{
    public class PinLockMiddleware
    {
        private readonly RequestDelegate _next;

        public PinLockMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, PinSecurityService pinSecurity)
        {
            PathString path = context.Request.Path;

            if (path.StartsWithSegments("/Auth"))
            {
                await _next(context);
                return;
            }

            User user = await dbContext.GetOrCreateDefaultUserAsync();

            if (string.IsNullOrWhiteSpace(user.PinHash))
            {
                if (IsApiOrAjaxRequest(context, path))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                context.Response.Redirect("/Auth/SetupPin");
                return;
            }

            string? cookieToken = context.Request.Cookies[PinSecurityService.AuthCookieName];
            string expectedToken = pinSecurity.GenerateSessionToken(user);

            if (!string.Equals(cookieToken, expectedToken, StringComparison.Ordinal))
            {
                if (IsApiOrAjaxRequest(context, path))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                context.Response.Redirect("/Auth/Unlock");
                return;
            }

            await _next(context);
        }

        private static bool IsApiOrAjaxRequest(HttpContext context, PathString path)
        {
            return path.StartsWithSegments("/Notification") ||
                   string.Equals(context.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }
    }
}