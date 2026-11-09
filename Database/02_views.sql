CREATE OR REPLACE VIEW v_active_menu AS
SELECT 
    m.id AS item_id,
    m.name AS item_name,
    c.name AS category_name,
    m.price,
    m.description
FROM menu_items m
JOIN categories c ON m.category_id = c.id
WHERE m.is_deleted = FALSE 
  AND c.is_deleted = FALSE 
  AND m.is_available = TRUE;

CREATE OR REPLACE VIEW v_kitchen_kds AS
SELECT 
    oi.id AS order_item_id,
    o.id AS order_id,
    rt.table_number,
    mi.name AS dish_name,
    oi.quantity,
    oi.status AS item_status,
    oi.notes,
    oi.created_at
FROM order_items oi
JOIN orders o ON oi.order_id = o.id
LEFT JOIN restaurant_tables rt ON o.table_id = rt.id
JOIN menu_items mi ON oi.menu_item_id = mi.id
WHERE oi.status IN ('pending', 'preparing')
ORDER BY oi.created_at ASC;

CREATE OR REPLACE VIEW v_low_stock_ingredients AS
SELECT 
    id,
    name,
    unit,
    stock_quantity,
    min_stock,
    (min_stock - stock_quantity) AS deficit
FROM ingredients
WHERE stock_quantity <= min_stock AND is_deleted = FALSE;

CREATE OR REPLACE VIEW v_active_ingredients AS
SELECT id, name, unit, stock_quantity, min_stock
FROM ingredients
WHERE is_deleted = FALSE;