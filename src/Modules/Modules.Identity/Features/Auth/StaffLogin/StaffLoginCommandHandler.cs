using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Auth.Login;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.StaffLogin;

public sealed class StaffLoginCommandHandler(
    IdentityDbContext dbContext,
    IValidator<StaffLoginCommand> validator,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan StaffAccessTokenLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    // Used to give the "user not found" path the same cryptographic cost as
    // the "user found, wrong PIN" path - see the comment in HandleAsync
    // below for why this matters.
    private static readonly User DummyUser = User.CreateStaff(
        "placeholder", "Dummy", Guid.Empty, Guid.Empty, DateTimeOffset.UnixEpoch);
    private static readonly string DummyPasswordHash =
        new PasswordHasher<User>().HashPassword(DummyUser, "dummy-pin-for-timing-safety");

    public async Task<LoginResult> HandleAsync(StaffLoginCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await dbContext.Users.FirstOrDefaultAsync(
            u => u.Id == command.UserId && u.BranchId == command.BranchId, cancellationToken);

        // Same generic failure whether the user/branch pair doesn't exist or
        // the PIN is wrong. Always verify against a real hash (a fixed dummy
        // one when no user was found) so both paths take equal time - see
        // LoginCommandHandler for the same pattern and rationale.
        var verificationResult = passwordHasher.VerifyHashedPassword(
            user ?? DummyUser, user?.PinHash ?? DummyPasswordHash, command.Pin);

        if (user is null || user.PinHash is null || verificationResult == PasswordVerificationResult.Failed)
        {
            return LoginResult.Failed();
        }

        var now = timeProvider.GetUtcNow();
        var accessToken = tokenService.CreateAccessToken(user, StaffAccessTokenLifetime);
        var refreshTokenValue = tokenService.GenerateRefreshTokenValue();
        var refreshTokenExpiresAt = now.Add(RefreshTokenLifetime);

        dbContext.RefreshTokens.Add(
            RefreshToken.Create(user.Id, tokenService.HashRefreshToken(refreshTokenValue), refreshTokenExpiresAt));
        await dbContext.SaveChangesAsync(cancellationToken);

        return LoginResult.Succeeded(accessToken.Value, accessToken.ExpiresAtUtc, refreshTokenValue, refreshTokenExpiresAt);
    }
}
