namespace Sentium.Web;

/// <summary>
/// Serves the built portal with a Content-Security-Policy that allows only the portal's own origin, so any
/// third-party request the portal starts making fails loudly instead of leaking to another host. The policy
/// covers only the portal's files; API responses and the development-only Scalar UI are left alone.
/// </summary>
internal static class PortalStaticFiles
{
    // The hash is that of an empty <style> element. framer-motion's AnimatePresence "popLayout" mode inserts one
    // and fills it through CSSOM (insertRule), which CSP does not restrict.
    private const string EmptyStyleElementHash = "'sha256-47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU='";

    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        $"style-src 'self' {EmptyStyleElementHash}; " +
        "font-src 'self'; " +
        "img-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static StaticFileOptions Options { get; } = new()
    {
        OnPrepareResponse = context =>
            context.Context.Response.Headers.ContentSecurityPolicy = ContentSecurityPolicy
    };
}
