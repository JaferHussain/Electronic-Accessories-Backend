namespace MoeezMobile.Api.Tests.Fixtures;

/// <summary>
/// Money values chosen to exercise behaviour that round numbers hide.
///
/// A suite built on 100.00 and 250.00 only ever proves round-number behaviour. Every value
/// here exists because something can go wrong at it — rounding direction, the boundary of
/// DECIMAL(18,2), or the half-way case where two rounding policies disagree.
/// </summary>
public static class MoneyFixtures
{
    /// <summary>Largest value DECIMAL(18,2) can hold — 16 integral digits plus 2 decimal.</summary>
    public const decimal DecimalColumnMax = 9_999_999_999_999_999.99m;

    /// <summary>Values that do not survive naive rounding intact.</summary>
    public static readonly decimal[] RequiringRounding =
    [
        33.335m,   // half-way at 2dp: away-from-zero gives 33.34, banker's gives 33.34
        33.345m,   // half-way where banker's and away-from-zero DISAGREE (33.34 vs 33.35)
        0.005m,    // rounds to 0.01, not 0.00
        0.004m,    // rounds to 0.00
        1.005m,    // the classic binary-representation trap — decimal must handle it exactly
        -33.345m   // negative half-way: away-from-zero means -33.35
    ];

    /// <summary>Quantity × price pairs whose product needs rounding before it is stored.</summary>
    public static IEnumerable<object[]> LineTotalRoundingCases() =>
    [
        //        qty, unitPrice,  expectedLineTotal
        [3,   33.335m,  100.01m],   // 100.005 rounds away from zero
        [7,    0.145m,    1.02m],   // 1.015 → 1.02
        [3,    0.005m,    0.02m],   // 0.015 → 0.02
        [1,    1.005m,    1.01m]
    ];

    /// <summary>Boundary values every money calculation must define behaviour for.</summary>
    public static readonly decimal[] Boundaries =
    [
        0m,
        0.01m,                  // smallest representable amount
        -0.01m,                 // smallest negative
        DecimalColumnMax,
        DecimalColumnMax - 0.01m
    ];

    /// <summary>Values that must be rejected, not silently coerced.</summary>
    public static readonly decimal[] InvalidNegatives = [-0.01m, -1m, -1000.50m];
}
