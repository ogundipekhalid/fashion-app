namespace FashionCommerce.API.Middlewares;

/// <summary>
/// Adds the standard set of defensive HTTP response headers to every
/// response. These are cheap, broadly-recommended defaults for an API
/// that also serves Swagger UI in Development — none of them require
/// per-endpoint configuration, so they're applied globally.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff"; // stops browsers guessing content-type, blocking some XSS/MIME-confusion attacks
        headers["X-Frame-Options"] = "DENY"; // prevents this API's responses (and Swagger UI) from being framed — clickjacking defense
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-XSS-Protection"] = "0"; // deliberately disabled: the legacy browser XSS filter this controls is deprecated and can itself introduce vulnerabilities; CSP below is the modern replacement
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()"; // this API has no legitimate use for these — deny by default

        // A relatively permissive CSP since this same origin serves Swagger UI
        // (which needs inline styles/scripts to render). A pure API-only
        // deployment (Swagger disabled outside Development) could tighten this further.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'";

        if (context.Request.IsHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains"; // 1 year — only meaningful over HTTPS, so gated on that

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
