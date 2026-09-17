namespace MoeezMobile.Api.Tests;

/// <summary>
/// Trait values that let the two test tiers be filtered separately:
/// <c>dotnet test --filter "Category=Unit"</c> / <c>"Category=Integration"</c>.
///
/// A developer with no Docker running must still be able to execute the full unit tier
/// and the coverage gate that depends on it — see contracts/test-conventions.md.
///
/// Applied as <c>[Trait(TestCategories.Name, TestCategories.Unit)]</c>. Using xUnit's
/// built-in trait attribute rather than a custom one keeps this working across xUnit
/// versions, which move the custom-trait interface between namespaces.
/// </summary>
public static class TestCategories
{
    public const string Name = "Category";

    /// <summary>No database, no network, no clock. Whole tier under 60 seconds.</summary>
    public const string Unit = "Unit";

    /// <summary>Real MySQL 8. Unbounded runtime. Separately invocable.</summary>
    public const string Integration = "Integration";
}
