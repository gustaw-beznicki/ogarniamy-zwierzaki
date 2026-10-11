using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ogarniamy_zwierzaki_api.Tests;

// EF Core interceptor that parks the first INSERT INTO documents until the test releases it. By then the capture
// transaction already holds its animal row lock, so a test can start a competing inactivation deterministically.
// Test hosts only.
public sealed class DocumentInsertGate : DbCommandInterceptor
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Completes once the insert is parked.
    public Task Reached => _reached.Task;

    public void Release() => _release.TrySetResult();

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await WaitIfDocumentInsertAsync(command, cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await WaitIfDocumentInsertAsync(command, cancellationToken);
        return result;
    }

    private async Task WaitIfDocumentInsertAsync(DbCommand command, CancellationToken cancellationToken)
    {
        if (command.CommandText.Contains("INSERT INTO documents ", StringComparison.Ordinal) && !_reached.Task.IsCompleted)
        {
            _reached.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }
    }
}
