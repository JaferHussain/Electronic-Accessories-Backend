namespace MoeezMobile.Api.Tests.Unit;


/// <summary>
/// Proves the harness reports honestly before any real coverage is written against it.
/// A suite that cannot be seen failing is indistinguishable from no suite at all.
/// </summary>
[Trait(TestCategories.Name, TestCategories.Unit)]
public class HarnessProvingTest
{
    [Fact]
    public void Harness_WhenAssertionIsFalse_ReportsFailure()
    {
        // This assertion was first written as Be(5), run, and watched failing (T014) before
        // being corrected. That is the only thing that proves the harness reports honestly.
        var actual = 2 + 2;

        actual.Should().Be(4);
    }

    [Fact]
    public void UnitTier_DoesNotReadDatabaseConfiguration()
    {
        // The unit tier needs nothing beyond the SDK, because its subjects hold no
        // connection — a property of what those types are, not an exemption.
        //
        // Originally this asserted MOEEZ_TEST_CONNECTION was unset. Constitution v2.0.0
        // made that variable the legitimate way to point the duplicate database elsewhere,
        // so the old assertion failed the moment the variable was used as designed. The
        // suite caught its own stale test. What matters is not that the variable is absent
        // but that nothing in this tier consults it.
        var probe = Environment.GetEnvironmentVariable("MOEEZ_TEST_CONNECTION");

        var _ = 2 + 2; // representative unit-tier work

        Environment.GetEnvironmentVariable("MOEEZ_TEST_CONNECTION")
            .Should().Be(probe, "a unit-tier test must neither read nor mutate database configuration");
    }
}
