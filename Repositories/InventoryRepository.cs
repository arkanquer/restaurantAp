using RestApp.Entities;
using RestApp.Infrastructure;

namespace RestApp.Repositories;

public class InventoryRepository : RepositoryBase
{
    public InventoryRepository(DbSession session) : base(session) { }

    public async Task<List<LowStockIngredient>> GetLowStockAsync()
    {
        var list = new List<LowStockIngredient>();
        await using var cmd = CreateCommand("SELECT id, name, unit, stock_quantity, min_stock, deficit FROM v_low_stock_ingredients;");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new LowStockIngredient(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5)
            ));
        }
        return list;
    }

    public async Task<List<Ingredient>> GetAllIngredientsAsync()
    {
        var list = new List<Ingredient>();
        await using var cmd = CreateCommand("SELECT id, name, unit, stock_quantity, min_stock FROM ingredients WHERE is_deleted = FALSE ORDER BY id;");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Ingredient(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4)
            ));
        }
        return list;
    }

    public async Task AddStockAsync(long ingredientId, decimal amount)
    {
        await using var cmd = CreateCommand("UPDATE ingredients SET stock_quantity = stock_quantity + @amount, updated_at = NOW() WHERE id = @id;");
        cmd.Parameters.AddWithValue("amount", amount);
        cmd.Parameters.AddWithValue("id", ingredientId);
        await cmd.ExecuteNonQueryAsync();
    }
}