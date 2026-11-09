CREATE OR REPLACE FUNCTION fn_get_order_total(p_order_id BIGINT)
RETURNS NUMERIC(10,2)
LANGUAGE plpgsql
AS $$
DECLARE
    v_total NUMERIC(10,2);
BEGIN
    SELECT COALESCE(SUM(quantity * unit_price), 0)
    INTO v_total
    FROM order_items
    WHERE order_id = p_order_id AND status != 'cancelled';
    
    RETURN v_total;
END;
$$;

CREATE OR REPLACE PROCEDURE sp_soft_delete_entity(
    p_table_name VARCHAR(100),
    p_record_id BIGINT,
    p_user_id BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    EXECUTE format(
        'UPDATE %I SET is_deleted = TRUE, deleted_at = NOW(), updated_at = NOW(), updated_by = $1 WHERE id = $2',
        p_table_name
    ) USING p_user_id, p_record_id;

    INSERT INTO audit_log (table_name, record_id, action, changed_by)
    VALUES (p_table_name, p_record_id, 'soft_delete', p_user_id);
END;
$$;

CREATE OR REPLACE PROCEDURE sp_restore_entity(
    p_table_name VARCHAR(100),
    p_record_id BIGINT,
    p_user_id BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    EXECUTE format(
        'UPDATE %I SET is_deleted = FALSE, deleted_at = NULL, updated_at = NOW(), updated_by = $1 WHERE id = $2',
        p_table_name
    ) USING p_user_id, p_record_id;

    INSERT INTO audit_log (table_name, record_id, action, changed_by)
    VALUES (p_table_name, p_record_id, 'restore', p_user_id);
END;
$$;

CREATE OR REPLACE PROCEDURE sp_create_order(
    p_table_id BIGINT,
    p_customer_id BIGINT,
    p_employee_id BIGINT,
    p_order_type VARCHAR(20),
    p_user_id BIGINT,
    INOUT p_order_id BIGINT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO orders (table_id, customer_id, employee_id, order_type, status, updated_by)
    VALUES (p_table_id, p_customer_id, p_employee_id, COALESCE(p_order_type, 'dine_in'), 'new', p_user_id)
    RETURNING id INTO p_order_id;

    IF p_table_id IS NOT NULL THEN
        UPDATE restaurant_tables SET status = 'occupied', updated_at = NOW(), updated_by = p_user_id WHERE id = p_table_id;
    END IF;
END;
$$;

CREATE OR REPLACE PROCEDURE sp_add_order_item(
    p_order_id BIGINT,
    p_menu_item_id BIGINT,
    p_quantity INT,
    p_notes TEXT,
    p_user_id BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_price NUMERIC(10,2);
BEGIN
    SELECT price INTO v_price FROM menu_items WHERE id = p_menu_item_id;

    INSERT INTO order_items (order_id, menu_item_id, quantity, unit_price, status, notes, updated_by)
    VALUES (p_order_id, p_menu_item_id, p_quantity, v_price, 'pending', p_notes, p_user_id);
END;
$$;

CREATE OR REPLACE PROCEDURE sp_process_payment(
    p_order_id BIGINT,
    p_payment_method_id BIGINT,
    p_amount NUMERIC(10,2),
    p_transaction_ref VARCHAR(255),
    p_user_id BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_table_id BIGINT;
BEGIN
    INSERT INTO payments (order_id, payment_method_id, amount, status, transaction_reference, paid_at, updated_by)
    VALUES (p_order_id, p_payment_method_id, p_amount, 'completed', p_transaction_ref, NOW(), p_user_id);

    SELECT table_id INTO v_table_id FROM orders WHERE id = p_order_id;

    UPDATE orders SET status = 'completed', updated_at = NOW(), updated_by = p_user_id WHERE id = p_order_id;

    IF v_table_id IS NOT NULL THEN
        UPDATE restaurant_tables SET status = 'free', updated_at = NOW(), updated_by = p_user_id WHERE id = v_table_id;
    END IF;
END;
$$;

CREATE OR REPLACE FUNCTION fn_trg_order_status_history()
RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'UPDATE' AND OLD.status IS DISTINCT FROM NEW.status) THEN
        INSERT INTO order_status_history (order_id, old_status, new_status, changed_by)
        VALUES (NEW.id, OLD.status, NEW.status, NEW.updated_by);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_order_status_history ON orders;
CREATE TRIGGER trg_order_status_history
AFTER UPDATE ON orders
FOR EACH ROW
EXECUTE FUNCTION fn_trg_order_status_history();

CREATE OR REPLACE FUNCTION fn_trg_auto_deduct_inventory()
RETURNS TRIGGER AS $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN 
        SELECT ingredient_id, quantity 
        FROM menu_item_ingredients 
        WHERE menu_item_id = NEW.menu_item_id
    LOOP
        UPDATE ingredients 
        SET stock_quantity = stock_quantity - (r.quantity * NEW.quantity),
            updated_at = NOW()
        WHERE id = r.ingredient_id;

        INSERT INTO inventory_movements (ingredient_id, order_item_id, movement_type, quantity, reason, created_by)
        VALUES (r.ingredient_id, NEW.id, 'consumption', r.quantity * NEW.quantity, 'Списання при додаванні в чек', NEW.updated_by);
    END LOOP;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_auto_deduct_inventory ON order_items;
CREATE TRIGGER trg_auto_deduct_inventory
AFTER INSERT ON order_items
FOR EACH ROW
EXECUTE FUNCTION fn_trg_auto_deduct_inventory();

CREATE OR REPLACE PROCEDURE sp_add_menu_item(
    IN p_category_id BIGINT,
    IN p_name VARCHAR,
    IN p_description TEXT,
    IN p_price NUMERIC,
    IN p_user_id BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_name IS NULL OR btrim(p_name) = '' THEN
        RAISE EXCEPTION 'Menu item name must not be empty';
    END IF;

    IF p_price IS NULL OR p_price < 0 THEN
        RAISE EXCEPTION 'Menu item price must be nonnegative';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM categories
        WHERE id = p_category_id AND is_deleted = FALSE
    ) THEN
        RAISE EXCEPTION 'Category % does not exist or is deleted', p_category_id;
    END IF;

    INSERT INTO menu_items
        (category_id, name, description, price, is_available, updated_at, updated_by)
    VALUES
        (p_category_id, p_name, p_description, p_price, TRUE, NOW(), p_user_id);
END;
$$;

CREATE OR REPLACE PROCEDURE sp_add_stock(
    IN p_ingredient_id BIGINT,
    IN p_amount NUMERIC,
    IN p_user_id BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_amount IS NULL OR p_amount <= 0 THEN
        RAISE EXCEPTION 'Stock increment must be positive';
    END IF;

    UPDATE ingredients
    SET stock_quantity = stock_quantity + p_amount,
        updated_at = NOW(),
        updated_by = p_user_id
    WHERE id = p_ingredient_id AND is_deleted = FALSE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Ingredient % does not exist or is deleted', p_ingredient_id;
    END IF;
END;
$$;