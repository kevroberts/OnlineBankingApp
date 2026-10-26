using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace OnlineBankingAPI.Data;

public static class DbExtensions
{
    /// <summary>Runs a scalar query on the context's connection and current transaction.</summary>
    public static async Task<T> ScalarAsync<T>(this BankDbContext db, string sql, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(sql, (NpgsqlConnection)db.Database.GetDbConnection(),
                db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction);
            return (T)(await cmd.ExecuteScalarAsync(ct))!;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    public static PostgresException? AsPostgres(this Exception e) => e switch
    {
        PostgresException p => p,
        { InnerException: { } inner } => inner.AsPostgres(),
        _ => null,
    };
}
