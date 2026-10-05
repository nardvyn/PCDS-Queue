-- Add the server-controlled grace period used after a queue number is called.

SET @customer_ack_column_exists = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'queue_numbers'
      AND COLUMN_NAME = 'customer_acknowledged_at'
);

SET @add_customer_ack_column = IF(
    @customer_ack_column_exists = 0,
    'ALTER TABLE queue_numbers ADD COLUMN customer_acknowledged_at DATETIME NULL AFTER called_at',
    'SELECT 1'
);

PREPARE add_customer_ack_column_stmt FROM @add_customer_ack_column;
EXECUTE add_customer_ack_column_stmt;
DEALLOCATE PREPARE add_customer_ack_column_stmt;

INSERT IGNORE INTO system_settings (setting_key, setting_value, description)
VALUES
    ('call_grace_period_seconds', '60', 'Seconds a called customer has before no-show is available'),
    ('enable_call_countdown', 'true', 'Show the call grace-period countdown to customers'),
    ('lock_no_show_during_grace', 'true', 'Prevent no-show during the customer grace period'),
    ('allow_customer_acknowledgement', 'true', 'Allow customers to acknowledge they are on their way'),
    ('mobile_call_alert', 'true', 'Enable mobile call alerts'),
    ('mobile_recall_alert', 'true', 'Enable mobile recall alerts');
