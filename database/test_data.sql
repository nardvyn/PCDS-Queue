-- Sample/demo data for local testing of the queue lifecycle described in ARCHITECTURE.md.
-- Run after schema.sql and seed.sql. Not for production use.

USE pcds_queue_db;

-- A few extra staff accounts (Cashier Window 3 operator used in the walkthrough example)
INSERT INTO `staff_users` (`username`, `password_hash`, `full_name`, `role`) VALUES
    ('cashier1',   '$argon2id$CHANGE_ME', 'Cashier Window 3 Staff', 'STAFF'),
    ('registrar1', '$argon2id$CHANGE_ME', 'Registrar Window 1 Staff', 'STAFF'),
    ('bookstore1', '$argon2id$CHANGE_ME', 'Bookstore Window 1 Staff', 'STAFF')
ON DUPLICATE KEY UPDATE `full_name` = VALUES(`full_name`);

-- Assign the cashier account to Cashier Window 3
UPDATE `service_windows`
SET `assigned_staff_id` = (SELECT `staff_id` FROM `staff_users` WHERE `username` = 'cashier1')
WHERE `department_id` = 1 AND `window_number` = 3;

-- Cashier queue today: C001..C025, most already completed, C025 is currently WAITING
INSERT INTO `queue_numbers`
    (`department_id`, `queue_date`, `sequence_number`, `queue_number`, `source`, `status`, `created_at`)
SELECT
    1, CURDATE(), seq,
    CONCAT('C', LPAD(seq, 3, '0')),
    'TICKET', 'COMPLETED',
    DATE_ADD(CURDATE(), INTERVAL (8 * 60 + seq) MINUTE)
FROM (
    SELECT 1 AS seq UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5
    UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9 UNION SELECT 10
    UNION SELECT 11 UNION SELECT 12 UNION SELECT 13 UNION SELECT 14 UNION SELECT 15
    UNION SELECT 16 UNION SELECT 17 UNION SELECT 18 UNION SELECT 19 UNION SELECT 20
    UNION SELECT 21 UNION SELECT 22 UNION SELECT 23 UNION SELECT 24
) AS seq_list;

-- The example ticket from the walkthrough: C025, MOBILE source, still WAITING
INSERT INTO `queue_numbers`
    (`queue_id`, `department_id`, `queue_date`, `sequence_number`, `queue_number`, `source`, `status`, `created_at`)
VALUES
    (105, 1, CURDATE(), 25, 'C025', 'MOBILE', 'WAITING', '2026-09-28 09:30:00');

INSERT INTO `queue_events` (`queue_id`, `event_type`, `notes`)
VALUES (105, 'CREATED', 'Ticket claimed via mobile QR scan');

-- Simulate Cashier Window 3 pressing NEXT: C025 becomes SERVING at Window 3
UPDATE `queue_numbers` q
JOIN `service_windows` w ON w.`department_id` = q.`department_id` AND w.`window_number` = 3
SET
    q.`status` = 'SERVING',
    q.`window_id` = w.`window_id`,
    q.`staff_id` = w.`assigned_staff_id`,
    q.`called_at` = '2026-09-28 09:42:00',
    q.`serving_at` = '2026-09-28 09:42:00'
WHERE q.`queue_id` = 105;

INSERT INTO `queue_events` (`queue_id`, `event_type`, `window_id`, `staff_id`, `notes`)
SELECT 105, 'CALLED', q.`window_id`, q.`staff_id`, 'Called to Cashier Window 3'
FROM `queue_numbers` q WHERE q.`queue_id` = 105;

-- A couple of upcoming waiting tickets behind C025
INSERT INTO `queue_numbers` (`department_id`, `queue_date`, `sequence_number`, `queue_number`, `source`, `status`, `created_at`) VALUES
    (1, CURDATE(), 26, 'C026', 'TICKET', 'WAITING', '2026-09-28 09:35:00'),
    (1, CURDATE(), 27, 'C027', 'MOBILE', 'WAITING', '2026-09-28 09:38:00');

-- An active QR session example for the Cashier kiosk
INSERT INTO `qr_sessions` (`department_id`, `token_hash`, `status`, `expires_at`) VALUES
    (1, SHA2(CONCAT('demo-token-', UUID()), 256), 'ACTIVE', DATE_ADD(NOW(), INTERVAL 90 SECOND));

