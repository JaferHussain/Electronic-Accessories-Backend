using Dapper;
using Microsoft.Data.SqlClient;

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
        SqlConnection conn, SqlTransaction tx, string docType, DateTime date)
    {
        var seq = await NextNumberAsync(conn, tx, docType, date.Date);
        return $"{docType}-{date:yyyyMMdd}-{seq:D3}";
    }

    /// <summary>Global sequence, e.g. PRD-0001.</summary>
    public static async Task<string> NextGlobalAsync(
        SqlConnection conn, SqlTransaction tx, string docType, int padding = 4)
    {
        var seq = await NextNumberAsync(conn, tx, docType, DateTime.Parse(GlobalCounterDate));
        return $"{docType}-{seq.ToString().PadLeft(padding, '0')}";
    }

    private static async Task<int> NextNumberAsync(
        SqlConnection conn, SqlTransaction tx, string docType, DateTime counterDate)
    {
        // MySQL's INSERT ... ON DUPLICATE KEY UPDATE did the create-or-increment in one
        // statement. The T-SQL equivalent is UPDATE-then-INSERT, and the ordering matters:
        //
        //   UPDATE first, with (UPDLOCK, HOLDLOCK) on the key range, so the row is locked
        //   for the rest of this transaction exactly as it was before. When the row exists
        //   this is the whole operation and no INSERT is attempted.
        //
        //   HOLDLOCK is what makes the missing-row case safe: it takes a range lock on the
        //   key even when the UPDATE matches nothing, so a second session cannot slip its
        //   own INSERT in between our UPDATE and our INSERT. Without it two tills opening
        //   the first invoice of the day would both insert and one would hit the unique
        //   index. The UPDATE ... OUTPUT returns the new value directly, so there is no
        //   second read that a concurrent session could race.
        var updated = await conn.QuerySingleOrDefaultAsync<int?>(@"
            UPDATE DocumentCounters WITH (UPDLOCK, HOLDLOCK)
            SET LastNumber = LastNumber + 1
            OUTPUT INSERTED.LastNumber
            WHERE DocType = @DocType AND CounterDate = @CounterDate;",
            new { DocType = docType, CounterDate = counterDate }, tx);

        if (updated.HasValue) return updated.Value;

        await conn.ExecuteAsync(@"
            INSERT INTO DocumentCounters (DocType, CounterDate, LastNumber)
            VALUES (@DocType, @CounterDate, 1);",
            new { DocType = docType, CounterDate = counterDate }, tx);

        return 1;
    }
}
