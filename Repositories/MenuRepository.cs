using RestApp.Entities;
using RestApp.Infrastructure;

namespace RestApp.Repositories;

public class MenuRepository : RepositoryBase
{
    public MenuRepository(DbSession session) : base(session) { }

    public async Task<List<ActiveMenuItem>> GetActiveAsync()
    {
        var list = new List<ActiveMenuItem>();
        await using var cmd = CreateCommand("SELECT item_id, item_name, category_name, price, description FROM v_active_menu;");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ActiveMenuItem(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)
            ));
        }
        return list;
    }

    public async Task AddMenuItemAsync(long categoryId, string name, string description, decimal price, long userId)
    {
        await using var cmd = CreateCommand("INSERT INTO menu_items (category_id, name, description, price, is_available, updated_by) VALUES (@cat, @name, @desc, @price, TRUE, @user);");
        cmd.Parameters.AddWithValue("cat", categoryId);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("desc", description);
        cmd.Parameters.AddWithValue("price", price);
        cmd.Parameters.AddWithValue("user", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SoftDeleteAsync(long itemId, long userId)
    {
        await using var cmd = CreateCommand("sp_soft_delete_entity", isStoredProcedure: true);
        cmd.Parameters.AddWithValue("p_table_name", "menu_items");
        cmd.Parameters.AddWithValue("p_record_id", itemId);
        cmd.Parameters.AddWithValue("p_user_id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RestoreAsync(long itemId, long userId)
{
    await using var cmd = CreateCommand("sp_restore_entity", isStoredProcedure: true);
    cmd.Parameters.AddWithValue("p_table_name", "menu_items");
    cmd.Parameters.AddWithValue("p_record_id", itemId);
    cmd.Parameters.AddWithValue("p_user_id", userId);
    await cmd.ExecuteNonQueryAsync();
}
}