using Modules.Identity.Features.Users;

namespace Modules.Identity.Infrastructure;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(User user, TimeSpan lifetime);

    string GenerateRefreshTokenValue();

    string HashRefreshToken(string refreshTokenValue);
}
