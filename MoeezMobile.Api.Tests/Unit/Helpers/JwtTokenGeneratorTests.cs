using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Models.Entities;
using MoeezMobile.Api.Tests.Fixtures;

namespace MoeezMobile.Api.Tests.Unit.Helpers;

[Trait(TestCategories.Name, TestCategories.Unit)]
public class JwtTokenGeneratorTests
{
    private static readonly JwtSettings Settings = new()
    {
        // Test-only key. HMAC-SHA256 requires at least 256 bits of key material.
        Key = "test-only-signing-key-of-sufficient-length-0123456789",
        Issuer = "MoeezMobile",
        Audience = "MoeezMobile",
        ExpiryHours = 12
    };

    private static readonly JwtTokenGenerator Generator = new(Settings);

    private static User AdminUser() => new()
    {
        Id = 7,
        Username = "owner",
        FullName = UrduFixtures.CustomerName,
        Role = Roles.Admin,
        IsActive = true
    };

    private static JwtSecurityToken Decode(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void Generate_IncludesUserIdAsNameIdentifier()
    {
        var (token, _) = Generator.Generate(AdminUser());

        Decode(token).Claims.Should().Contain(c =>
            c.Type == ClaimTypes.NameIdentifier && c.Value == "7");
    }

    [Fact]
    public void Generate_IncludesUsername()
    {
        var (token, _) = Generator.Generate(AdminUser());

        Decode(token).Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "owner");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Salesman")]
    public void Generate_EncodesTheUsersRole(string role)
    {
        // Role authorisation for every Admin-only endpoint rests on this claim being right.
        var user = AdminUser();
        user.Role = role;

        var (token, _) = Generator.Generate(user);

        Decode(token).Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == role);
    }

    [Fact]
    public void Generate_WithUrduFullName_PreservesTextThroughEncoding()
    {
        var (token, _) = Generator.Generate(AdminUser());

        Decode(token).Claims.Should().Contain(c =>
            c.Type == "fullName" && c.Value == UrduFixtures.CustomerName);
    }

    [Fact]
    public void Generate_WhenFullNameIsMissing_FallsBackToUsername()
    {
        var user = AdminUser();
        user.FullName = null;

        var (token, _) = Generator.Generate(user);

        Decode(token).Claims.Should().Contain(c => c.Type == "fullName" && c.Value == "owner");
    }

    [Fact]
    public void Generate_IssuesDistinctJtiPerToken()
    {
        // Two tokens for the same user must not be interchangeable artefacts.
        var (first, _) = Generator.Generate(AdminUser());
        var (second, _) = Generator.Generate(AdminUser());

        var firstJti = Decode(first).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var secondJti = Decode(second).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        firstJti.Should().NotBe(secondJti);
    }

    [Fact]
    public void Generate_SetsIssuerAndAudienceFromSettings()
    {
        var (token, _) = Generator.Generate(AdminUser());
        var decoded = Decode(token);

        decoded.Issuer.Should().Be("MoeezMobile");
        decoded.Audiences.Should().Contain("MoeezMobile");
    }

    [Fact]
    public void Generate_SetsExpiryToConfiguredHoursAhead()
    {
        // NOTE (FR-007): JwtTokenGenerator reads DateTime.UtcNow directly, so expiry cannot
        // be asserted exactly — only within a tolerance. This is the ambient-clock dependency
        // that task T049/T050 replaces with an injected TimeProvider. Once injected, this
        // assertion tightens to an exact equality and the tolerance disappears.
        var before = DateTime.UtcNow;

        var (_, expiresAt) = Generator.Generate(AdminUser());

        expiresAt.Should().BeCloseTo(before.AddHours(12), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Generate_ReturnedExpiryMatchesTokenExpiryClaim()
    {
        var (token, expiresAt) = Generator.Generate(AdminUser());

        // The value handed back to the caller must agree with what the token actually carries,
        // or the frontend will refresh at the wrong moment.
        Decode(token).ValidTo.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }
}
