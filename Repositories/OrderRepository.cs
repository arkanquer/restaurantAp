using System.Data;
using Npgsql;
using RestApp.Entities;
using RestApp.Infrastructure;

namespace RestApp.Repositories;

public class OrderRepository : RepositoryBase
{
    public OrderRepository(DbSession session) : base(session) { }

    public async Task<long> CreateOrderAsync(long? tableId, long? customerId, long employeeId, string orderType, long userId)
    {
        await using var cmd = CreateCommand("sp_create_order", isStoredProcedure: true);
        cmd.Parameters.AddWithValue("p_table_id", (object?)tableId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("p_customer_id", (object?)customerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("p_employee_id", employeeId);
        cmd.Parameters.AddWithValue("p_order_type", orderType);
        cmd.Parameters.AddWithValue("p_user_id", userId);

        var outParam = new NpgsqlParameter("p_order_id", DbType.Int64)
        {
            Direction = ParameterDirection.InputOutput,
            Value = DBNull.Value
        };
        cmd.Parameters.Add(outParam);

        await cmd.ExecuteNonQueryAsync();
        return Convert.ToInt64(outParam.Value);
    }

    public async Task AddOrderItemAsync(long orderId, long menuItemId, int quantity, string? notes, long userId)
    {
        await using var cmd = CreateCommand("sp_add_order_item", isStoredProcedure: true);
        cmd.Parameters.AddWithValue("p_order_id", orderId);
        cmd.Parameters.AddWithValue("p_menu_item_id", menuItemId);
        cmd.Parameters.AddWithValue("p_quantity", quantity);
        cmd.Parameters.AddWithValue("p_notes", (object?)notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("p_user_id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ProcessPaymentAsync(long orderId, long paymentMethodId, decimal amount, string transactionRef, long userId)
    {
        await using var cmd = CreateCommand("sp_process_payment", isStoredProcedure: true);
        cmd.Parameters.AddWithValue("p_order_id", orderId);
        cmd.Parameters.AddWithValue("p_payment_method_id", paymentMethodId);
        cmd.Parameters.AddWithValue("p_amount", amount);
        cmd.Parameters.AddWithValue("p_transaction_ref", transactionRef);
        cmd.Parameters.AddWithValue("p_user_id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<KitchenKdsItem>> GetKitchenKdsAsync()
    {
        var list = new List<KitchenKdsItem>();
        await using var cmd = CreateCommand("SELECT order_item_id, order_id, table_number, dish_name, quantity, item_status, notes FROM v_kitchen_kds;");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new KitchenKdsItem(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)
            ));
        }
        return list;
    }
}