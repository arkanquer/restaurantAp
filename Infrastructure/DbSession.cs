using System.Data;
using Npgsql;

namespace RestApp.Infrastructure;

public class DbSession : IAsyncDisposable
{
    public NpgsqlConnection Connection { get; }
    public NpgsqlTransaction? Transaction { get; private set; }

    public DbSession(string connectionString)
    {
        Connection = new NpgsqlConnection(connectionString);
    }

    public async Task OpenAsync()
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync();
    }

    public async Task BeginTransactionAsync()
    {
        await OpenAsync();
        Transaction = await Connection.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (Transaction != null)
        {
            await Transaction.CommitAsync();
            await Transaction.DisposeAsync();
            Transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (Transaction != null)
        {
            await Transaction.RollbackAsync();
            await Transaction.DisposeAsync();
            Transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Transaction != null)
            await Transaction.DisposeAsync();

        await Connection.DisposeAsync();
    }
}