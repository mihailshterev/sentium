namespace Sentium.Web;

/// <summary>
/// Headers every response carries: no MIME sniffing (an API body or a download is never run as a script or page), no
/// referrer leaving the origin, and no browser feature the portal does not use. The microphone stays allowed for the
/// portal itself: push-to-talk records through it. HSTS is set by Caddy, the TLS endpoint.
/// </summary>
internal static class SecurityHeaders
{
    private const string PermissionsPolicy =
        "camera=(), geolocation=(), payment=(), usb=(), serial=(), bluetooth=(), microphone=(self)";

    public static IApplicationBuilder UseSentiumSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = PermissionsPolicy;

            return next(context);
        });
    }
}
