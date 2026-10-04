-- PCDS Queue Management System — default configuration data
-- Run after schema.sql. Safe to re-run (idempotent upserts).

USE pcds_queue_db;

-- ---------------------------------------------------------------------------
-- departments
-- ---------------------------------------------------------------------------
INSERT INTO `departments` (`department_id`, `department_name`, `queue_prefix`, `is_active`, `queue_is_open`) VALUES
    (1, 'Cashier',   'C', 1, 1),
    (2, 'Registrar', 'R', 1, 1),
    (3, 'Bookstore', 'B', 1, 1)
ON DUPLICATE KEY UPDATE
    `department_name` = VALUES(`department_name`),
    `is_active` = VALUES(`is_active`),
    `queue_is_open` = VALUES(`queue_is_open`);

-- ---------------------------------------------------------------------------
-- service_windows — 4 Cashier, 3 Registrar, 2 Bookstore
-- ---------------------------------------------------------------------------
INSERT INTO `service_windows` (`department_id`, `window_number`, `window_name`) VALUES
    (1, 1, 'Cashier Window 1'),
    (1, 2, 'Cashier Window 2'),
    (1, 3, 'Cashier Window 3'),
    (1, 4, 'Cashier Window 4'),
    (2, 1, 'Registrar Window 1'),
    (2, 2, 'Registrar Window 2'),
    (2, 3, 'Registrar Window 3'),
    (3, 1, 'Bookstore Window 1'),
    (3, 2, 'Bookstore Window 2')
ON DUPLICATE KEY UPDATE
    `window_name` = VALUES(`window_name`);

-- ---------------------------------------------------------------------------
-- staff_users — one default admin account (password must be reset before production use)
-- password_hash below is a placeholder; use create_admin.py to generate a real hash.
-- ---------------------------------------------------------------------------
INSERT INTO `staff_users` (`username`, `password_hash`, `full_name`, `role`) VALUES
    ('admin', '$argon2id$CHANGE_ME_ON_FIRST_LOGIN', 'System Administrator', 'ADMIN')
ON DUPLICATE KEY UPDATE
    `full_name` = VALUES(`full_name`);

-- ---------------------------------------------------------------------------
-- system_settings
-- ---------------------------------------------------------------------------
INSERT INTO `system_settings` (`setting_key`, `setting_value`, `description`) VALUES
    ('school_name',                  'PCDS',                         'School name displayed by the queue system'),
    ('system_name',                  'PCDS Queue Management System', 'System display name'),
    ('queue_start_number',           '1',                            'First queue number generated each day'),
    ('queue_digits',                 '3',                            'Queue number padding: 1 becomes 001'),
    ('queue_number_digits',          '3',                            'Legacy queue number padding setting'),
    ('daily_reset_enabled',          'true',                         'Use a new queue sequence for each date'),
    ('qr_expiration_hours',          '24',                           'QR session lifetime in hours'),
    ('tv_voice_enabled',             'true',                         'Enable TV voice announcements'),
    ('refresh_interval',             '3',                            'Display refresh interval in seconds'),
    ('mobile_queue_enabled',         'true',                         'Allow queue entry from the mobile app'),
    ('kiosk_queue_enabled',          'true',                         'Allow queue entry from the kiosk'),
    ('mobile_notification_enabled',  'true',                         'Enable mobile queue notifications')
ON DUPLICATE KEY UPDATE
    `setting_value` = VALUES(`setting_value`),
    `description` = VALUES(`description`);
