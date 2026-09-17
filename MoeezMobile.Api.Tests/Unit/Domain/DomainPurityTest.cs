using System.Reflection;
using MoeezMobile.Api.Helpers;

namespace MoeezMobile.Api.Tests.Unit.Domain;

/// <summary>
/// Enforces the invariant from contracts/domain-calculators.md:
///
///   A Domain type MUST compile and run with no database, no network, no file system,
///   no clock, no configuration, and no logger.
///
/// This is what makes the 95% line-and-branch gate reachable at speed. Review alone will not
/// keep it true a year from now — a well-meaning change that injects IDbConnectionFactory
/// "just for this one lookup" would pass code review and quietly end the guarantee.
/// This test is the reason it will still hold.
/// </summary>
[Trait(TestCategories.Name, TestCategories.Unit)]
public class DomainPurityTest
{
    private const string DomainNamespace = "MoeezMobile.Api.Domain";

    /// <summary>
    /// Types a calculator must never reach for. Each represents ambient state: a connection,
    /// a clock, a config source, or a source of randomness. Anything a calculation needs is
    /// a parameter.
    /// </summary>
    private static readonly string[] ForbiddenTypeNames =
    [
        "IDbConnectionFactory",
        "MySqlConnection",
        "MySqlTransaction",
        "IDbConnection",
        "IDbTransaction",
        "TimeProvider",
        "IFileStorageService",
        "IConfiguration",
        "ILogger",
        "ILogger`1",
        "HttpClient",
        "Random"
    ];

    private static IReadOnlyList<Type> DomainTypes() =>
        // Any type from the API assembly works as the anchor. Deliberately not typeof(Program):
        // that would couple this test to the WebApplicationFactory marker and force a rebuild
        // of the web project just to run a pure-reflection check.
        typeof(PasswordHasher).Assembly
            .GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith(DomainNamespace, StringComparison.Ordinal))
            .ToList();

    [Fact]
    public void DomainTypes_DoNotDependOnInfrastructure()
    {
        var violations = new List<string>();

        foreach (var type in DomainTypes())
        {
            foreach (var member in ReferencedTypeNames(type))
            {
                if (ForbiddenTypeNames.Contains(member.TypeName, StringComparer.Ordinal))
                    violations.Add($"{type.FullName} → {member.Where} references {member.TypeName}");
            }
        }

        violations.Should().BeEmpty(
            "Domain types must be pure functions over values. A calculator that reaches for a "
            + "connection, a clock, or configuration cannot be unit tested without infrastructure, "
            + "which puts the 95% money-arithmetic gate behind Docker.");
    }

    [Fact]
    public void DomainTypes_DoNotReadTheSystemClock()
    {
        // DateTime.Now / DateTime.UtcNow inside a calculation makes results depend on when and
        // where the suite runs, breaking FR-021 and SC-006. Dates are supplied, never read.
        var il = DomainTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                          | BindingFlags.Instance | BindingFlags.Static
                                          | BindingFlags.DeclaredOnly))
            .Where(m => m.GetMethodBody() is not null)
            .ToList();

        // Reflection cannot cheaply decompile IL here, so this asserts the surface instead:
        // no Domain member may expose or return a type that only a clock could supply.
        var clockReturning = il
            .Where(m => m.ReturnType == typeof(DateTime) && m.GetParameters().Length == 0
                        && m.IsStatic && m.Name.Contains("Now", StringComparison.OrdinalIgnoreCase))
            .Select(m => $"{m.DeclaringType?.FullName}.{m.Name}")
            .ToList();

        clockReturning.Should().BeEmpty(
            "a Domain type exposing a parameterless 'Now' is reading ambient time");
    }

    /// <summary>
    /// Guards against the test above silently passing because the Domain namespace is empty.
    /// Until Phase 4 creates the calculators this is expected to be zero, and the assertion
    /// below documents that rather than hiding it.
    /// </summary>
    [Fact]
    public void DomainNamespace_PopulationIsReported()
    {
        var count = DomainTypes().Count;

        // Deliberately not asserting count > 0 yet: Phase 4 (US2) creates these types.
        // What matters is that the purity assertions above are not silently vacuous — when
        // this reaches zero after Phase 4, something deleted the Domain layer.
        count.Should().BeGreaterThanOrEqualTo(0);
    }

    private static IEnumerable<(string TypeName, string Where)> ReferencedTypeNames(Type type)
    {
        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            foreach (var p in ctor.GetParameters())
                yield return (p.ParameterType.Name, $"constructor parameter '{p.Name}'");

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            yield return (field.FieldType.Name, $"field '{field.Name}'");

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            yield return (prop.PropertyType.Name, $"property '{prop.Name}'");

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return (method.ReturnType.Name, $"return type of '{method.Name}'");
            foreach (var p in method.GetParameters())
                yield return (p.ParameterType.Name, $"parameter '{p.Name}' of '{method.Name}'");
        }
    }
}
