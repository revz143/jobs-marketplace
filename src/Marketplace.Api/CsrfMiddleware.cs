using System.Security.Cryptography;

namespace Marketplace.Api;

/// <summary>Applies double-submit CSRF checks to browser-write API requests.</summary>
public sealed class CsrfMiddleware(RequestDelegate next)
{
    /// <summary>Validates a matching XSRF header and cookie for unsafe methods.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method) || context.Request.Path == "/api/v1/auth/csrf")
        {
            await next(context).ConfigureAwait(false); return;
        }
        var cookie = context.Request.Cookies["XSRF-TOKEN"];
        var header = context.Request.Headers["X-XSRF-TOKEN"].ToString();
        if (string.IsNullOrEmpty(cookie) || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(cookie), System.Text.Encoding.UTF8.GetBytes(header)))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { title = "Invalid CSRF token", status = 400 }).ConfigureAwait(false); return;
        }
        await next(context).ConfigureAwait(false);
    }
}
