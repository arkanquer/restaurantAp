namespace RestApp.Entities;

public record ActiveMenuItem(
    long ItemId,
    string ItemName,
    string CategoryName,
    decimal Price,
    string? Description
);

public record KitchenKdsItem(
    long OrderItemId,
    long OrderId,
    int? TableNumber,
    string DishName,
    int Quantity,
    string ItemStatus,
    string? Notes
);

public record LowStockIngredient(
    long Id,
    string Name,
    string Unit,
    decimal StockQuantity,
    decimal MinStock,
    decimal Deficit
);

public record Ingredient(
    long Id,
    string Name,
    string Unit,
    decimal StockQuantity,
    decimal MinStock
);