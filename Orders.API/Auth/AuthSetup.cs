using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace Orders.API.Auth
{
    public static class  AuthPolicies
    {
        public const string Read = "Orders.Read";
        public const string Write = "Orders.Write";
        public const string Admin = "Orders.Admin";

    }
    public static class AuthSetup
    {
        public static IServiceCollection AddOrdersAuth(this IServiceCollection services, IConfiguration config)
        {
            var mode = config["Auth:Mode"] ?? "Entra";

            if (mode.Equals("Entra", StringComparison.OrdinalIgnoreCase))
            {
                // Real Microsoft Entra ID: validates issuer, audience, signature, lifetime; maps the "roles" claim.
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddMicrosoftIdentityWebApi(config.GetSection("AzureAd"));
            }
            else
            {
                // Mock mode: tokens from `dotnet user-jwts` (config written to appsettings.Development.json).
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer();
            }

            // Roles are hierarchical: Admin > Write > Read.
            services.AddAuthorizationBuilder()
                .AddPolicy(AuthPolicies.Read, p => p.RequireRole(AuthPolicies.Read, AuthPolicies.Write, AuthPolicies.Admin))
                .AddPolicy(AuthPolicies.Write, p => p.RequireRole(AuthPolicies.Write, AuthPolicies.Admin))
                .AddPolicy(AuthPolicies.Admin, p => p.RequireRole(AuthPolicies.Admin))
                // Secure by default: any endpoint without an explicit policy still requires a signed-in user.
                .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

            return services;
        }
    }
}
