using RestApp.Infrastructure;

namespace RestApp.Services;

public class RestaurantService
{
    private readonly string _connectionString;

    public RestaurantService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<long> PlaceOrderAndPayAsync(long tableId, long waiterId, List<(long menuItemId, int qty, string? notes)> items, decimal paymentAmount, long userId)
    {
        await using var uow = await UnitOfWork.CreateAsync(_connectionString);
        await uow.BeginAsync();
        try
        {
            long orderId = await uow.Orders.CreateOrderAsync(tableId, null, waiterId, "dine_in", userId);

            foreach (var item in items)
            {
                await uow.Orders.AddOrderItemAsync(orderId, item.menuItemId, item.qty, item.notes, userId);
            }

            await uow.Orders.ProcessPaymentAsync(orderId, 1, paymentAmount, $"TXN-CS-{orderId}", userId);

            await uow.CommitAsync();
            return orderId;
        }
        catch
        {
            await uow.RollbackAsync();
            throw;
        }
    }
}