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
        await using var cmd = CreateCommand("SELECT id, name, unit, stock_quantity, min_stock FROM v_active_ingredients ORDER BY id;");
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

    public async Task AddStockAsync(long ingredientId, decimal amount, long userId)
    {
        await using var cmd = CreateCommand("sp_add_stock", isStoredProcedure: true);
        cmd.Parameters.AddWithValue("p_amount", amount);
        cmd.Parameters.AddWithValue("p_ingredient_id", ingredientId);
        cmd.Parameters.AddWithValue("p_user_id", userId);
        await cmd.ExecuteNonQueryAsync();
    }
}