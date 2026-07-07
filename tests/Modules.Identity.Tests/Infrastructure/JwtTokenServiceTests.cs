using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;

namespace Modules.Identity.Tests.Infrastructure;

[TestClass]
public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "DonPicaso.Tests",
        Audience = "DonPicaso.Tests.Pos",
        SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
    };

    private readonly JwtTokenService _tokenService = new(Options);

    [TestMethod]
    public void CreateAccessToken_ForBranchScopedUser_IncludesSubRoleBrandAndBranchClaims()
    {
        var user = User.CreateStaff(
            "pin-hash", "Staff Member",
            brandId: Guid.NewGuid(), branchId: Guid.NewGuid(), DateTimeOffset.UtcNow);

        var accessToken = _tokenService.CreateAccessToken(user, TimeSpan.FromMinutes(15));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Value);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == nameof(UserRole.Staff));
        jwt.Claims.Should().Contain(c => c.Type == "brandId" && c.Value == user.BrandId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "branchId" && c.Value == user.BranchId.ToString());
    }

    [TestMethod]
    public void CreateAccessToken_ForCorporateUser_OmitsBrandAndBranchClaims()
    {
        var user = User.CreateAdmin(
            "corporate@donpicaso.dev", "password-hash", "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, DateTimeOffset.UtcNow);

        var accessToken = _tokenService.CreateAccessToken(user, TimeSpan.FromMinutes(15));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Value);

        jwt.Claims.Should().NotContain(c => c.Type == "brandId");
        jwt.Claims.Should().NotContain(c => c.Type == "branchId");
    }

    [TestMethod]
    public void HashRefreshToken_CalledTwiceWithTheSameValue_ProducesTheSameHash()
    {
        var value = _tokenService.GenerateRefreshTokenValue();

        _tokenService.HashRefreshToken(value).Should().Be(_tokenService.HashRefreshToken(value));
    }

    [TestMethod]
    public void GenerateRefreshTokenValue_CalledTwice_ProducesDifferentValues()
    {
        _tokenService.GenerateRefreshTokenValue().Should().NotBe(_tokenService.GenerateRefreshTokenValue());
    }
}
