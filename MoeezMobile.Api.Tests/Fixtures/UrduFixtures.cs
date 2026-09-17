namespace MoeezMobile.Api.Tests.Fixtures;

/// <summary>
/// Real Urdu test data. Never ASCII placeholders.
///
/// An ASCII fixture round-trips cleanly through a misconfigured utf8mb4 connection and hides
/// exactly the truncation and collation defects the test exists to catch. "Test Product"
/// passing proves nothing about how the shop's actual data behaves.
/// </summary>
public static class UrduFixtures
{
    // Product names as the shop would actually enter them.
    public const string ProductCharger = "موبائل چارجر";
    public const string ProductHandsFree = "ہینڈز فری";
    public const string ProductBattery = "موبائل بیٹری";
    public const string ProductCable = "ڈیٹا کیبل";
    public const string ProductCover = "موبائل کور";

    // People and places.
    public const string CustomerName = "محمد اسلم";
    public const string SupplierName = "الفلاح ٹریڈرز";
    public const string ShopLocation = "ڈانوراں، لودھراں";

    // Ledger note text written by the existing SaleRepository.
    public const string LedgerNoteSale = "فروخت";

    /// <summary>
    /// Strings chosen to break naive encoding handling. Each has a distinct hazard:
    /// combining marks, a zero-width joiner, mixed direction, and a 4-byte emoji that
    /// utf8 (3-byte) columns silently truncate but utf8mb4 stores correctly.
    /// </summary>
    public static readonly string[] EncodingHazards =
    [
        "موبائل چارجر",           // baseline Urdu
        "ہینڈز فری ‍کیبل",         // contains a zero-width joiner
        "چارجر 20W فاسٹ",          // mixed RTL/LTR with digits
        "قیمت: ₨1,250",            // currency sign plus digits
        "موبائل 📱 کور"            // 4-byte character — the utf8 vs utf8mb4 tell
    ];

    /// <summary>Every fixture string, for parameterised round-trip tests.</summary>
    public static IEnumerable<object[]> AllProductNames() =>
    [
        [ProductCharger], [ProductHandsFree], [ProductBattery], [ProductCable], [ProductCover]
    ];

    /// <summary>The hazard set, for parameterised encoding tests.</summary>
    public static IEnumerable<object[]> AllEncodingHazards() =>
        EncodingHazards.Select(s => new object[] { s });
}
