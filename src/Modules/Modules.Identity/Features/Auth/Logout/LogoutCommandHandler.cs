using Microsoft.EntityFrameworkCore;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.Logout;

public sealed class LogoutCommandHandler(
    IdentityDbContext dbContext,
    IJwtTokenService tokenService,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(command.RefreshToken);

        var existing = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (existing is null || existing.RevokedAtUtc is not null)
        {
            return;
        }

        existing.Revoke(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
