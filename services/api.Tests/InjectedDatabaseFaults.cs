using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace ogarniamy_zwierzaki_api.Tests;

// EF Core interceptor that injects transient PostgreSQL failures at the persistence boundary. Test hosts only.
public sealed class InjectedDatabaseFaults : IDbCommandInterceptor, IDbTransactionInterceptor
{
    private bool _completionWritten;

    // The next storage-receipt update fails before it reaches the database.
    public bool FailNextReceiptUpdate { get; set; }

    // The next completion commits, but the commit acknowledgement is lost.
    public bool LoseNextCompletionCommit { get; set; }

    public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var sql = command.CommandText.TrimStart();
        if (FailNextReceiptUpdate && sql.StartsWith("UPDATE document_files", StringComparison.Ordinal))
        {
            FailNextReceiptUpdate = false;
            throw Transient("receipt update");
        }

        if (LoseNextCompletionCommit && sql.StartsWith("UPDATE documents", StringComparison.Ordinal))
        {
            _completionWritten = true;
        }

        return ValueTask.FromResult(result);
    }

    public Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_completionWritten)
        {
            _completionWritten = false;
            LoseNextCompletionCommit = false;
            throw Transient("completion commit acknowledgement");
        }

        return Task.CompletedTask;
    }

    // Npgsql treats an I/O failure as transient, as it would a dropped connection.
    private static NpgsqlException Transient(string what) =>
        new($"Injected: {what} failed.", new IOException("Injected connection failure."));
}
