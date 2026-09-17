using MoeezMobile.Api.Helpers;
using MoeezMobile.Api.Tests.Fixtures;

namespace MoeezMobile.Api.Tests.Unit.Helpers;

[Trait(TestCategories.Name, TestCategories.Unit)]
public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ThenVerifyWithSamePassword_Succeeds()
    {
        var hash = _hasher.Hash("correct-horse-battery");

        _hasher.Verify("correct-horse-battery", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithWrongPassword_Fails()
    {
        var hash = _hasher.Hash("correct-horse-battery");

        _hasher.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_IsCaseSensitive()
    {
        var hash = _hasher.Hash("ShopOwner1");

        _hasher.Verify("shopowner1", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_ForSamePasswordTwice_ProducesDifferentHashes()
    {
        // BCrypt salts each hash. Identical hashes for identical passwords would mean the
        // salt is missing or fixed, which makes the stored hashes rainbow-table fodder.
        var first = _hasher.Hash("ShopOwner1");
        var second = _hasher.Hash("ShopOwner1");

        first.Should().NotBe(second);
        _hasher.Verify("ShopOwner1", first).Should().BeTrue();
        _hasher.Verify("ShopOwner1", second).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("$2a$corrupted$salt")]
    public void Verify_WithMalformedHash_ReturnsFalseRatherThanThrowing(string malformedHash)
    {
        // A corrupted hash column must fail the login, not take the API down with an
        // unhandled SaltParseException.
        _hasher.Verify("any-password", malformedHash).Should().BeFalse();
    }

    [Fact]
    public void Hash_WithUrduPassword_VerifiesCorrectly()
    {
        // Passwords are not restricted to ASCII. If BCrypt's byte handling truncated
        // multi-byte input, two different Urdu passwords could collide.
        var hash = _hasher.Hash(UrduFixtures.CustomerName);

        _hasher.Verify(UrduFixtures.CustomerName, hash).Should().BeTrue();
        _hasher.Verify(UrduFixtures.SupplierName, hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_WithEmptyPassword_StillProducesVerifiableHash()
    {
        // Pins current behaviour: the hasher does not reject empty input — rejecting empty
        // passwords is the caller's job. Recorded so a future change here is deliberate.
        var hash = _hasher.Hash(string.Empty);

        _hasher.Verify(string.Empty, hash).Should().BeTrue();
        _hasher.Verify("x", hash).Should().BeFalse();
    }
}
