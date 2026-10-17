using RestApp.Repositories;

namespace RestApp.Infrastructure;

public class UnitOfWork : IAsyncDisposable
{
    private readonly DbSession _session;

    public MenuRepository Menu { get; }
    public OrderRepository Orders { get; }
    public InventoryRepository Inventory { get; }

    private UnitOfWork(DbSession session)
    {
        _session = session;
        Menu = new MenuRepository(_session);
        Orders = new OrderRepository(_session);
        Inventory = new InventoryRepository(_session);
    }

    public static async Task<UnitOfWork> CreateAsync(string connectionString)
    {
        var session = new DbSession(connectionString);
        await session.OpenAsync();
        return new UnitOfWork(session);
    }

    public async Task BeginAsync() => await _session.BeginTransactionAsync();
    public async Task CommitAsync() => await _session.CommitTransactionAsync();
    public async Task RollbackAsync() => await _session.RollbackTransactionAsync();

    public async ValueTask DisposeAsync()
    {
        await _session.DisposeAsync();
    }
}