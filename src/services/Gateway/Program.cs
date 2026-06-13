using System.Net.Http.Headers;
using System.Security.Claims;
using Sentium.ApiGateway;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var identityAuthority = builder.Configuration["Identity:Authority"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173", "https://localhost:5173"];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddHttpClient("IdpClient").AddServiceDiscovery();
builder.Services.AddScoped<TokenRefreshService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "Sentium.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = builder.Environment.IsProduction() ? SameSiteMode.Strict : SameSiteMode.None;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = identityAuthority;
    options.ClientId = "gateway-bff";

    var gatewayBffSecret = builder.Configuration["Identity:GatewayBffSecret"];

    options.ClientSecret = string.IsNullOrWhiteSpace(gatewayBffSecret)
        ? throw new InvalidOperationException("Gateway BFF secret is not configured.")
        : gatewayBffSecret;

    options.ResponseType = OpenIdConnectResponseType.Code;
    options.UsePkce = true;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("api");
    options.Scope.Add("roles");
    options.Scope.Add("offline_access");

    options.CallbackPath = "/bff/callback";
    options.SignedOutCallbackPath = "/bff/logged-out";

    options.TokenValidationParameters.NameClaimType = ClaimTypes.Name;
    options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;

    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing");

    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProvider = context =>
        {
            if (context.Properties.Items.TryGetValue("returnUrl", out var returnUrl))
            {
                context.Properties.RedirectUri = "/bff/login-complete?returnUrl=" + Uri.EscapeDataString(returnUrl ?? "/");
            }

            return Task.CompletedTask;
        }
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Authority = identityAuthority;
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing");
    options.TokenValidationParameters.ValidateAudience = false;
});

builder.Services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
    .PostConfigure<IHttpClientFactory>((options, factory) =>
    {
        options.Backchannel = factory.CreateClient("IdpClient");
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(async transformContext =>
        {
            var httpContext = transformContext.HttpContext;

            var expiresAt = await httpContext.GetTokenAsync("expires_at");
            if (expiresAt != null &&
                DateTimeOffset.TryParse(expiresAt, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var tokenExpiry) &&
                tokenExpiry < DateTimeOffset.UtcNow.AddSeconds(30))
            {
                var refreshService = httpContext.RequestServices.GetRequiredService<TokenRefreshService>();
                await refreshService.TryRefreshAsync(httpContext);
            }

            var accessToken = await httpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(accessToken))
            {
                transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        });
    });

var app = builder.Build();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapBffEndpoints(app.Configuration);

app.MapReverseProxy();

app.Run();
