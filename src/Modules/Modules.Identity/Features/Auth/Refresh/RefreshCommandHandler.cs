using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Auth.Login;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.Refresh;

public sealed class RefreshCommandHandler(
    IdentityDbContext dbContext,
    IValidator<RefreshCommand> validator,
    IJwtTokenService tokenService,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan AdminAccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StaffAccessTokenLifetime = TimeSpan.FromHours(12);

    public async Task<LoginResult> HandleAsync(RefreshCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var tokenHash = tokenService.HashRefreshToken(command.RefreshToken);
        var now = timeProvider.GetUtcNow();

        var existing = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existing is null || existing.RevokedAtUtc is not null || existing.ExpiresAtUtc <= now)
        {
            return LoginResult.Failed();
        }

        var user = await dbContext.Users.FirstAsync(u => u.Id == existing.UserId, cancellationToken);

        var lifetime = user.Role == UserRole.Staff ? StaffAccessTokenLifetime : AdminAccessTokenLifetime;
        var accessToken = tokenService.CreateAccessToken(user, lifetime);

        return LoginResult.Succeeded(accessToken.Value, accessToken.ExpiresAtUtc, command.RefreshToken, existing.ExpiresAtUtc);
    }
}
