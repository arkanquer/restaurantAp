using System.Text;
using Npgsql;
using RestApp.Infrastructure;
using RestApp.Services;

Console.OutputEncoding = Encoding.UTF8;

var cs = Environment.GetEnvironmentVariable("REST_DB_CONNECTION")
         ?? "Host=127.0.0.1;Port=5432;Database=rest;Username=postgres;Password=8856";

const long AdminUserId = 1;

Header("1. Каталог активного меню (View: v_active_menu)");
await using (var uow = await UnitOfWork.CreateAsync(cs))
{
    foreach (var item in await uow.Menu.GetActiveAsync())
    {
        Console.WriteLine($"  [{item.ItemId,2}]  {item.ItemName,-25}  Категорія: {item.CategoryName,-12}  Ціна: {item.Price,7:F2} UAH");
    }
}

Header("2. Оформлення замовлення та автосписання через Service (Checkout + Trigger)");
var service = new RestaurantService(cs);
var orderItems = new List<(long menuItemId, int qty, string? notes)>
{
    (1, 2, "Без цибулі"),
    (3, 1, null)
};

long newOrderId = await service.PlaceOrderAndPayAsync(
    tableId: 1,
    waiterId: 3,
    items: orderItems,
    paymentAmount: 630.00m,
    userId: AdminUserId
);

await using (var uow = await UnitOfWork.CreateAsync(cs))
{
    var menu = (await uow.Menu.GetActiveAsync()).ToDictionary(m => m.ItemId);
    Console.WriteLine($"  Замовлення №{newOrderId} (Столик №1, офіціант ID: 3):");
    decimal total = 0;
    foreach (var item in orderItems)
    {
        if (menu.TryGetValue(item.menuItemId, out var dish))
        {
            decimal lineTotal = dish.Price * item.qty;
            total += lineTotal;
            string noteStr = string.IsNullOrEmpty(item.notes) ? "" : $" ({item.notes})";
            Console.WriteLine($"    • {dish.ItemName,-22} {item.qty} х {dish.Price,6:F2} = {lineTotal,7:F2} UAH{noteStr}");
        }
    }
    Console.WriteLine($"  Разом сплачено: {total,7:F2} UAH");
    Console.WriteLine("  Статус: Оплачено, інгредієнти автоматично списано зі складу тригером.");
}

Header("3. Перегляд активних страв на кухні (View: v_kitchen_kds)");
await using (var uow = await UnitOfWork.CreateAsync(cs))
{
    var kds = await uow.Orders.GetKitchenKdsAsync();
    if (kds.Count == 0)
        Console.WriteLine("  На кухні немає незавершених страв.");
    else
    {
        foreach (var k in kds)
            Console.WriteLine($"  Чек №{k.OrderId}, Столик №{k.TableNumber}, Страва: {k.DishName} x{k.Quantity}");
    }
}

Header("4. Робота з кількома репозиторіями в одному UoW (Menu + Inventory)");
await using (var uow = await UnitOfWork.CreateAsync(cs))
{
    await uow.BeginAsync();
    try
    {
        string newDishName = "Піца TEST";
        string newDishDesc = "Моцарела, Дор Блю, Пармезан, Чеддер";
        decimal newDishPrice = 320.00m;
        long categoryId = 1;

        long ingredientId = 2;
        decimal stockToAdd = 10.0m;

        var ingredients = await uow.Inventory.GetAllIngredientsAsync();
        var targetIng = ingredients.FirstOrDefault(i => i.Id == ingredientId);
        string ingName = targetIng?.Name ?? $"Інгредієнт #{ingredientId}";
        string ingUnit = targetIng?.Unit ?? "кг";
        decimal stockBefore = targetIng?.StockQuantity ?? 0;

        await uow.Menu.AddMenuItemAsync(categoryId, newDishName, newDishDesc, newDishPrice, AdminUserId);
        await uow.Inventory.AddStockAsync(ingredientId, stockToAdd);

        await uow.CommitAsync();

        Console.WriteLine("  Атомарно виконано 2 операції в єдиній транзакції:");
        Console.WriteLine($"    1. [MenuRepository] Додано нову страву:");
        Console.WriteLine($"       • Назва: {newDishName}");
        Console.WriteLine($"       • Опис: {newDishDesc}");
        Console.WriteLine($"       • Ціна: {newDishPrice:F2} UAH");
        Console.WriteLine($"    2. [InventoryRepository] Поповнено складські залишки:");
        Console.WriteLine($"       • Інгредієнт: {ingName}");
        Console.WriteLine($"       • Зміна: +{stockToAdd:F2} {ingUnit} (було: {stockBefore:F2} {ingUnit} -> стало: {stockBefore + stockToAdd:F2} {ingUnit})");
        Console.WriteLine("  Статус: Транзакцію успішно підтверджено (COMMIT).");
    }
    catch
    {
        await uow.RollbackAsync();
        throw;
    }
}

Header("5. Soft Delete та відновлення страви (View: v_active_menu)");
await using (var uow = await UnitOfWork.CreateAsync(cs))
{
    var activeBefore = await uow.Menu.GetActiveAsync();
    Console.WriteLine($"  Перелік v_active_menu ДО видалення ({activeBefore.Count} страв):");
    foreach (var item in activeBefore)
    {
        Console.WriteLine($"    • [{item.ItemId,2}] {item.ItemName,-25} ({item.Price,6:F2} UAH)");
    }

    if (activeBefore.Count > 0)
    {
        var targetItem = activeBefore.Last();

        await uow.Menu.SoftDeleteAsync(targetItem.ItemId, AdminUserId);
        var activeAfterDelete = await uow.Menu.GetActiveAsync();

        Console.WriteLine($"\n  Видалено через SP: «{targetItem.ItemName}» (ID: {targetItem.ItemId})");
        Console.WriteLine($"  Перелік v_active_menu ПІСЛЯ Soft Delete ({activeAfterDelete.Count} страв):");
        foreach (var item in activeAfterDelete)
        {
            Console.WriteLine($"    • [{item.ItemId,2}] {item.ItemName,-25} ({item.Price,6:F2} UAH)");
        }

        await uow.Menu.RestoreAsync(targetItem.ItemId, AdminUserId);
        var activeAfterRestore = await uow.Menu.GetActiveAsync();

        Console.WriteLine($"\n  Відновлено через SP: «{targetItem.ItemName}» (ID: {targetItem.ItemId})");
        Console.WriteLine($"  Перелік v_active_menu ПІСЛЯ відновлення ({activeAfterRestore.Count} страв):");
        foreach (var item in activeAfterRestore)
        {
            Console.WriteLine($"    • [{item.ItemId,2}] {item.ItemName,-25} ({item.Price,6:F2} UAH)");
        }
    }
}

Header("6. Звіт: дефіцит інгредієнтів (View: v_low_stock_ingredients)");
await using (var uow = await UnitOfWork.CreateAsync(cs))
{
    var lowStock = await uow.Inventory.GetLowStockAsync();
    foreach (var ing in lowStock)
    {
        Console.WriteLine($"{ing.Name,-10}: залишилось {ing.StockQuantity} {ing.Unit} (дефіцит: {ing.Deficit})");
    }
}

static void Header(string title) => Console.WriteLine($"\n{title}");