using Dapper;
using MySqlConnector;

namespace MoeezMobile.Api.Helpers;

/// <summary>
/// Race-safe document numbering. Always called inside the caller's transaction so the
/// counter row stays locked until the document is committed.
/// </summary>
public static class InvoiceNumberGenerator
{
    /// <summary>Sentinel date for counters that are a single global sequence rather than per-day.</summary>
    private const string GlobalCounterDate = "1000-01-01";

    /// <summary>Daily sequence, e.g. SAL-20260830-001 / PUR-20260830-001.</summary>
    public static async Task<string> NextDailyAsync(
        MySqlConnection conn, MySqlTransaction tx, string docType, DateTime date)
    {
        var seq = await NextNumberAsync(conn, tx, docType, date.Date);
        return $"{docType}-{date:yyyyMMdd}-{seq:D3}";
    }

    /// <summary>Global sequence, e.g. PRD-0001.</summary>
    public static async Task<string> NextGlobalAsync(
        MySqlConnection conn, MySqlTransaction tx, string docType, int padding = 4)
    {
        var seq = await NextNumberAsync(conn, tx, docType, DateTime.Parse(GlobalCounterDate));
        return $"{docType}-{seq.ToString().PadLeft(padding, '0')}";
    }

    private static async Task<int> NextNumberAsync(
        MySqlConnection conn, MySqlTransaction tx, string docType, DateTime counterDate)
    {
        // The upsert both creates the row and increments it atomically, and leaves the row
        // write-locked for the rest of this transaction - so the SELECT that follows cannot
        // read a value another session is about to take.
        // (LAST_INSERT_ID() is deliberately not used here: DocumentCounters has no
        //  AUTO_INCREMENT column, so on a fresh INSERT it would return a stale value from an
        //  earlier statement on the same connection.)
        await conn.ExecuteAsync(@"
            INSERT INTO DocumentCounters (DocType, CounterDate, LastNumber)
            VALUES (@DocType, @CounterDate, 1)
            ON DUPLICATE KEY UPDATE LastNumber = LastNumber + 1;",
            new { DocType = docType, CounterDate = counterDate }, tx);

        return await conn.ExecuteScalarAsync<int>(
            "SELECT LastNumber FROM DocumentCounters WHERE DocType = @DocType AND CounterDate = @CounterDate;",
            new { DocType = docType, CounterDate = counterDate }, tx);
    }
}
