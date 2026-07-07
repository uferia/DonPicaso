using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Auth;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IdentityDbContext dbContext,
    IValidator<LoginCommand> validator,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan AdminAccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    // Used to give the "user not found" path the same cryptographic cost as
    // the "user found, wrong password" path - see the comment in
    // HandleAsync below for why this matters.
    private static readonly User DummyUser = User.CreateAdmin(
        "dummy@donpicaso.internal", "placeholder", "Dummy", UserRole.Corporate, null, null, DateTimeOffset.UnixEpoch);
    private static readonly string DummyPasswordHash =
        new PasswordHasher<User>().HashPassword(DummyUser, "dummy-password-for-timing-safety");

    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

        // Same generic failure whether the email doesn't exist or the
        // password is wrong, so a caller can't use this endpoint to
        // enumerate accounts. Always verify against a real hash (a fixed
        // dummy one when no user was found) so both paths take equal time -
        // otherwise the *response latency* alone leaks which emails exist,
        // even though the response body is identical either way.
        var verificationResult = passwordHasher.VerifyHashedPassword(
            user ?? DummyUser, user?.PasswordHash ?? DummyPasswordHash, command.Password);

        if (user is null || user.PasswordHash is null || verificationResult == PasswordVerificationResult.Failed)
        {
            return LoginResult.Failed();
        }

        var now = timeProvider.GetUtcNow();
        var accessToken = tokenService.CreateAccessToken(user, AdminAccessTokenLifetime);
        var refreshTokenValue = tokenService.GenerateRefreshTokenValue();
        var refreshTokenExpiresAt = now.Add(RefreshTokenLifetime);

        dbContext.RefreshTokens.Add(
            RefreshToken.Create(user.Id, tokenService.HashRefreshToken(refreshTokenValue), refreshTokenExpiresAt));
        await dbContext.SaveChangesAsync(cancellationToken);

        return LoginResult.Succeeded(accessToken.Value, accessToken.ExpiresAtUtc, refreshTokenValue, refreshTokenExpiresAt);
    }
}

public sealed record LoginResult(
    bool IsSuccess,
    string? AccessToken,
    DateTimeOffset? AccessTokenExpiresAtUtc,
    string? RefreshToken,
    DateTimeOffset? RefreshTokenExpiresAtUtc)
{
    public static LoginResult Failed() => new(false, null, null, null, null);

    public static LoginResult Succeeded(
        string accessToken, DateTimeOffset accessTokenExpiresAtUtc, string refreshToken, DateTimeOffset refreshTokenExpiresAtUtc) =>
        new(true, accessToken, accessTokenExpiresAtUtc, refreshToken, refreshTokenExpiresAtUtc);
}
