-- Adds per-shift window assignment. Run once against an existing database.
USE pcds_queue_db;

ALTER TABLE staff_users
    ADD COLUMN department_id INT UNSIGNED NULL AFTER role;

ALTER TABLE staff_users
    ADD INDEX idx_staff_department (department_id);

ALTER TABLE staff_users
    ADD CONSTRAINT fk_staff_department
    FOREIGN KEY (department_id)
    REFERENCES departments(department_id)
    ON DELETE SET NULL
    ON UPDATE CASCADE;

CREATE TABLE staff_shifts (
    shift_id INT UNSIGNED NOT NULL AUTO_INCREMENT,
    staff_id INT UNSIGNED NOT NULL,
    window_id INT UNSIGNED NOT NULL,
    department_id INT UNSIGNED NOT NULL,
    status ENUM('ACTIVE', 'ENDED') NOT NULL DEFAULT 'ACTIVE',
    started_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ended_at DATETIME NULL,
    PRIMARY KEY (shift_id),
    KEY idx_shift_staff (staff_id),
    KEY idx_shift_window (window_id),
    KEY idx_shift_department (department_id),
    KEY idx_shift_status (status),
    CONSTRAINT fk_shift_staff
        FOREIGN KEY (staff_id) REFERENCES staff_users(staff_id)
        ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_shift_window
        FOREIGN KEY (window_id) REFERENCES service_windows(window_id)
        ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_shift_department
        FOREIGN KEY (department_id) REFERENCES departments(department_id)
        ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

UPDATE staff_users SET department_id = 1 WHERE username = 'cashier1';
UPDATE staff_users SET department_id = 2 WHERE username = 'registrar1';
UPDATE staff_users SET department_id = 3 WHERE username = 'bookstore1';
