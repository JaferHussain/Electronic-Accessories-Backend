using System.Security.Claims;
using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Tests.Fixtures;

namespace MoeezMobile.Api.Tests.Unit.Helpers;

[Trait(TestCategories.Name, TestCategories.Unit)]
public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    [Fact]
    public void GetUserId_WithValidNameIdentifier_ReturnsId()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "42"));

        principal.GetUserId().Should().Be(42);
    }

    [Fact]
    public void GetUserId_WithNoNameIdentifier_ReturnsZero()
    {
        // Pins current behaviour. Zero is a sentinel, not a real user id — every caller that
        // writes CreatedBy from this value depends on it never silently becoming a valid id.
        var principal = PrincipalWith(new Claim(ClaimTypes.Name, "owner"));

        principal.GetUserId().Should().Be(0);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("")]
    [InlineData("4.2")]
    [InlineData("99999999999999999999")] // overflows int
    public void GetUserId_WithUnparseableNameIdentifier_ReturnsZero(string raw)
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, raw));

        principal.GetUserId().Should().Be(0);
    }

    [Fact]
    public void GetUsername_WithNameClaim_ReturnsUsername()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.Name, "salesman1"));

        principal.GetUsername().Should().Be("salesman1");
    }

    [Fact]
    public void GetUsername_WithNoNameClaim_ReturnsEmptyNotNull()
    {
        // Callers concatenate this into ledger notes and log lines; a null here would be a
        // NullReferenceException at the point of sale.
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "1"));

        principal.GetUsername().Should().BeEmpty();
    }

    [Fact]
    public void GetUsername_WithUrduFullName_PreservesTextExactly()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.Name, UrduFixtures.CustomerName));

        principal.GetUsername().Should().Be(UrduFixtures.CustomerName);
    }

    [Fact]
    public void GetUserId_OnAnonymousPrincipal_ReturnsZero()
    {
        new ClaimsPrincipal(new ClaimsIdentity()).GetUserId().Should().Be(0);
    }
}
