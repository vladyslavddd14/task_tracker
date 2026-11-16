using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace TaskTracker.Api;

public static class ClaimsExtensions
{
    /// <summary>Id of the authenticated user, taken from the "sub" claim of the JWT.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new TaskTracker.Application.Common.UnauthorizedException("The token has no valid user id.");
    }
}
