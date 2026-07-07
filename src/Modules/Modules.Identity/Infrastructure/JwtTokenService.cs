using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Infrastructure;

public sealed class JwtTokenService(JwtOptions options) : IJwtTokenService
{
    public AccessToken CreateAccessToken(User user, TimeSpan lifetime)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(lifetime);

        // Plain short claim types on purpose - see Task 4's implementation
        // note about JwtBearerOptions.MapInboundClaims.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("role", user.Role.ToString()),
        };

        if (user.BrandId is { } brandId)
        {
            claims.Add(new Claim("brandId", brandId.ToString()));
        }

        if (user.BranchId is { } branchId)
        {
            claims.Add(new Claim("branchId", branchId.ToString()));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    public string GenerateRefreshTokenValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string refreshTokenValue) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshTokenValue)));
}
