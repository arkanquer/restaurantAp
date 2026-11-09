CREATE INDEX idx_customers_phone ON customers(phone);
CREATE INDEX idx_customers_email ON customers(email);
CREATE INDEX idx_reservations_table_start ON reservations(table_id, start_at);
CREATE INDEX idx_reservations_status ON reservations(status);
CREATE INDEX idx_menu_items_name_category ON menu_items(name, category_id);
CREATE INDEX idx_deliveries_supplier ON deliveries(supplier_id);
CREATE INDEX idx_deliveries_delivered_at ON deliveries(delivered_at);
CREATE INDEX idx_inventory_ingredient_time ON inventory_movements(ingredient_id, occurred_at);
CREATE INDEX idx_inventory_type ON inventory_movements(movement_type);
CREATE INDEX idx_orders_status_created ON orders(status, created_at);
CREATE INDEX idx_order_items_order ON order_items(order_id);
CREATE INDEX idx_order_items_menu_item ON order_items(menu_item_id);
CREATE INDEX idx_payments_order ON payments(order_id);
CREATE INDEX idx_payments_status ON payments(status);
CREATE INDEX idx_order_status_history_order ON order_status_history(order_id, changed_at);
CREATE INDEX idx_audit_log_table_record ON audit_log(table_name, record_id);
CREATE INDEX idx_audit_log_changed_at ON audit_log(changed_at);

/* soft delete*/
CREATE INDEX idx_active_menu_items ON menu_items(category_id) WHERE is_deleted = FALSE AND is_available = TRUE;
CREATE INDEX idx_active_employees ON employees(position) WHERE is_deleted = FALSE;
CREATE INDEX idx_active_tables ON restaurant_tables(capacity) WHERE is_deleted = FALSE AND status = 'free';

CREATE INDEX idx_audit_log_new_data_gin ON audit_log USING GIN (new_data);
CREATE INDEX idx_audit_log_old_data_gin ON audit_log USING GIN (old_data);