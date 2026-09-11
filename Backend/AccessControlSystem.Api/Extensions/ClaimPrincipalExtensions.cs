using System.Security.Claims;

namespace AccessControlSystem.Api.Extensions {
    public static class ClaimPrincipalExtensions {

        public static string Id(this ClaimsPrincipal user)
            => user.Claims.FirstOrDefault(c => c.Type == "Id")?.Value ?? string.Empty;

        public static string Email(this ClaimsPrincipal user)
            => user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty;
        
        public static string Role(this ClaimsPrincipal user)
            => user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;
    }
}
