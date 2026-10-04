-- PCDS Queue Management System — MySQL Database
-- Database: pcds_queue_db
-- This file mirrors the live database structure (verified via SHOW CREATE TABLE).
-- All client apps (Mobile, Kiosk, Cashier, Registrar, Bookstore, TV, Admin) read/write only through the Flask API, never directly.

CREATE DATABASE IF NOT EXISTS pcds_queue_db
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE pcds_queue_db;

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS `staff_shifts`;

-- ---------------------------------------------------------------------------
-- departments — configurable, not hard-coded (Cashier, Registrar, Bookstore, ...)
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS `departments`;
CREATE TABLE `departments` (
    `department_id`   INT UNSIGNED NOT NULL AUTO_INCREMENT,
    `department_name` VARCHAR(100) NOT NULL,
    `queue_prefix`    VARCHAR(10)  NOT NULL,
    `is_active`       TINYINT(1)   NOT NULL DEFAULT 1,
    `queue_is_open`   TINYINT(1)   NOT NULL DEFAULT 1,
    `created_at`      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at`      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`department_id`),
    UNIQUE KEY `uq_department_name` (`department_name`),
    UNIQUE KEY `uq_department_prefix` (`queue_prefix`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- staff_users — Admin and Staff (Cashier/Registrar/Bookstore) logins
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS `staff_users`;
CREATE TABLE `staff_users` (
    `staff_id`        INT UNSIGNED NOT NULL AUTO_INCREMENT,
    `full_name`       VARCHAR(150) NOT NULL,
    `username`        VARCHAR(100) NOT NULL,
    `password_hash`   VARCHAR(255) NOT NULL,
    `role`            ENUM('ADMIN', 'STAFF') NOT NULL DEFAULT 'STAFF',
    `department_id`   INT UNSIGNED NULL,
    `account_status`  ENUM('ACTIVE', 'INACTIVE', 'SUSPENDED') NOT NULL DEFAULT 'ACTIVE',
    `created_at`      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at`      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    `last_login_at`   DATETIME     NULL,
    PRIMARY KEY (`staff_id`),
    UNIQUE KEY `uq_staff_username` (`username`),
    KEY `idx_staff_department` (`department_id`),
    CONSTRAINT `fk_staff_department`
        FOREIGN KEY (`department_id`) REFERENCES `departments` (`department_id`)
        ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- service_windows — e.g. Cashier Window 1-4, Registrar Window 1-3, Bookstore Window 1-2
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS `service_windows`;
CREATE TABLE `service_windows` (
    `window_id`         INT UNSIGNED NOT NULL AUTO_INCREMENT,
    `department_id`     INT UNSIGNED NOT NULL,
    `window_number`     INT UNSIGNED NOT NULL,
    `window_name`       VARCHAR(100) NOT NULL,
    `assigned_staff_id` INT UNSIGNED NULL,
    `status`            ENUM('ACTIVE', 'PAUSED', 'CLOSED') NOT NULL DEFAULT 'ACTIVE',
    `created_at`        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at`        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`window_id`),
    UNIQUE KEY `uq_department_window` (`department_id`, `window_number`),
    KEY `idx_window_department` (`department_id`),
    KEY `idx_window_staff` (`assigned_staff_id`),
    KEY `idx_window_status` (`status`),
    CONSTRAINT `fk_window_department`
        FOREIGN KEY (`department_id`) REFERENCES `departments` (`department_id`)
        ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT `fk_window_staff`
        FOREIGN KEY (`assigned_staff_id`) REFERENCES `staff_users` (`staff_id`)
        ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- staff_shifts — a staff member selects an available department window per shift
-- ---------------------------------------------------------------------------
CREATE TABLE `staff_shifts` (
    `shift_id`       INT UNSIGNED NOT NULL AUTO_INCREMENT,
    `staff_id`       INT UNSIGNED NOT NULL,
    `window_id`      INT UNSIGNED NOT NULL,
    `department_id`  INT UNSIGNED NOT NULL,
    `status`         ENUM('ACTIVE', 'ENDED') NOT NULL DEFAULT 'ACTIVE',
    `started_at`     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `ended_at`       DATETIME NULL,
    PRIMARY KEY (`shift_id`),
    KEY `idx_shift_staff` (`staff_id`),
    KEY `idx_shift_window` (`window_id`),
    KEY `idx_shift_department` (`department_id`),
    KEY `idx_shift_status` (`status`),
    CONSTRAINT `fk_shift_staff`
        FOREIGN KEY (`staff_id`) REFERENCES `staff_users` (`staff_id`)
        ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT `fk_shift_window`
        FOREIGN KEY (`window_id`) REFERENCES `service_windows` (`window_id`)
        ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT `fk_shift_department`
        FOREIGN KEY (`department_id`) REFERENCES `departments` (`department_id`)
        ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- queue_numbers — the core ticket table (WAITING -> CALLED/SERVING -> COMPLETED)
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS `queue_numbers`;
CREATE TABLE `queue_numbers` (
    `queue_id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `department_id`        INT UNSIGNED NOT NULL,
    `sequence_number`      INT UNSIGNED NOT NULL,
    `queue_number`         VARCHAR(30)  NOT NULL,
    `queue_date`           DATE         NOT NULL,
    `source`               ENUM('MOBILE', 'TICKET') NOT NULL,
    `status`               ENUM('WAITING', 'CALLED', 'SERVING', 'COMPLETED', 'NO_SHOW', 'CANCELLED')
                           NOT NULL DEFAULT 'WAITING',
    `device_identifier`    VARCHAR(255) NULL,
    `notification_token`   TEXT         NULL,
    `window_id`            INT UNSIGNED NULL,
    `staff_id`             INT UNSIGNED NULL,
    `created_at`           TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `called_at`            DATETIME     NULL,
    `serving_at`           DATETIME     NULL,
    `completed_at`         DATETIME     NULL,
    `cancelled_at`         DATETIME     NULL,
    PRIMARY KEY (`queue_id`),
    UNIQUE KEY `uq_queue_department_date_sequence` (`department_id`, `queue_date`, `sequence_number`),
    UNIQUE KEY `uq_queue_display_date` (`department_id`, `queue_date`, `queue_number`),
    KEY `fk_queue_staff` (`staff_id`),
    KEY `idx_queue_department` (`department_id`),
    KEY `idx_queue_status` (`status`),
    KEY `idx_queue_date` (`queue_date`),
    KEY `idx_queue_waiting` (`department_id`, `queue_date`, `status`, `sequence_number`),
    KEY `idx_queue_window` (`window_id`, `status`),
    KEY `idx_queue_device` (`device_identifier`),
    CONSTRAINT `fk_queue_department`
        FOREIGN KEY (`department_id`) REFERENCES `departments` (`department_id`)
        ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT `fk_queue_staff`
        FOREIGN KEY (`staff_id`) REFERENCES `staff_users` (`staff_id`)
        ON DELETE SET NULL ON UPDATE CASCADE,
    CONSTRAINT `fk_queue_window`
        FOREIGN KEY (`window_id`) REFERENCES `service_windows` (`window_id`)
        ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- queue_counters — one row per department/day, locked with SELECT ... FOR UPDATE
-- to atomically hand out the next sequence number even under concurrent requests.
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS `queue_counters`;
CREATE TABLE `queue_counters` (
    `counter_id`      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `department_id`   INT UNSIGNED NOT NULL,
    `queue_date`      DATE         NOT NULL,
    `last_number`     INT UNSIGNED NOT NULL DEFAULT 0,
    `updated_at`      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`counter_id`),
    UNIQUE KEY `uq_counter_department_date` (`department_id`, `queue_date`),
    KEY `idx_counter_date` (`queue_date`),
    CONSTRAINT `fk_counter_department`
        FOREIGN KEY (`department_id`) REFERENCES `departments` (`department_id`)
        ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- qr_sessions — short-lived kiosk QR tokens used by the mobile app to claim a ticket
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS `qr_sessions`;
CREATE TABLE `qr_sessions` (
    `qr_session_id`   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `department_id`   INT UNSIGNED NOT NULL,
    `token_hash`      VARCHAR(255) NOT NULL,
    `scanned_at`      DATETIME     NULL,
    `status`          ENUM('ACTIVE', 'EXPIRED', 'REVOKED') NOT NULL DEFAULT 'ACTIVE',
    `created_at`      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `expires_at`      DATETIME     NOT NULL,
    `revoked_at`      DATETIME     NULL,
    PRIMARY KEY (`qr_session_id`),
    UNIQUE KEY `uq_qr_token` (`token_hash`),
    KEY `idx_qr_department` (`department_id`),
    KEY `idx_qr_status` (`status`),
    KEY `idx_qr_expiration` (`expires_at`),
    CONSTRAINT `fk_qr_department`
        FOREIGN KEY (`department_id`) REFERENCES `departments` (`department_id`)
        ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- queue_events — audit trail of every status change/call/recall for a ticket
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS `queue_events`;
CREATE TABLE `queue_events` (
    `event_id`        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `queue_id`        BIGINT UNSIGNED NOT NULL,
    `event_type`      ENUM('CREATED', 'CALLED', 'RECALLED', 'SERVING', 'COMPLETED', 'NO_SHOW', 'CANCELLED')
                      NOT NULL,
    `staff_id`        INT UNSIGNED NULL,
    `window_id`       INT UNSIGNED NULL,
    `notes`           VARCHAR(500) NULL,
    `created_at`      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`event_id`),
    KEY `fk_event_staff` (`staff_id`),
    KEY `fk_event_window` (`window_id`),
    KEY `idx_event_queue` (`queue_id`),
    KEY `idx_event_type` (`event_type`),
    KEY `idx_event_created` (`created_at`),
    CONSTRAINT `fk_event_queue`
        FOREIGN KEY (`queue_id`) REFERENCES `queue_numbers` (`queue_id`)
        ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT `fk_event_staff`
        FOREIGN KEY (`staff_id`) REFERENCES `staff_users` (`staff_id`)
        ON DELETE SET NULL ON UPDATE CASCADE,
    CONSTRAINT `fk_event_window`
        FOREIGN KEY (`window_id`) REFERENCES `service_windows` (`window_id`)
        ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- audit_logs — login and administrative action trail (separate from queue_events)
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS `audit_logs`;
CREATE TABLE `audit_logs` (
    `audit_id`        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `staff_id`        INT UNSIGNED NULL,
    `action`          VARCHAR(100) NOT NULL,
    `entity_type`     VARCHAR(100) NULL,
    `entity_id`       VARCHAR(100) NULL,
    `description`     VARCHAR(500) NULL,
    `ip_address`      VARCHAR(45)  NULL,
    `created_at`      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`audit_id`),
    KEY `idx_audit_staff` (`staff_id`),
    KEY `idx_audit_action` (`action`),
    KEY `idx_audit_created` (`created_at`),
    CONSTRAINT `fk_audit_staff`
        FOREIGN KEY (`staff_id`) REFERENCES `staff_users` (`staff_id`)
        ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- system_settings — key/value configuration (reset time, QR expiry, school name, ...)
-- ---------------------------------------------------------------------------
DROP TABLE IF EXISTS `system_settings`;
CREATE TABLE `system_settings` (
    `setting_id`      INT UNSIGNED NOT NULL AUTO_INCREMENT,
    `setting_key`     VARCHAR(100) NOT NULL,
    `setting_value`   TEXT         NULL,
    `description`     VARCHAR(255) NULL,
    `updated_by`      INT UNSIGNED NULL,
    `created_at`      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at`      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`setting_id`),
    UNIQUE KEY `uq_setting_key` (`setting_key`),
    KEY `fk_setting_staff` (`updated_by`),
    CONSTRAINT `fk_setting_staff`
        FOREIGN KEY (`updated_by`) REFERENCES `staff_users` (`staff_id`)
        ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET FOREIGN_KEY_CHECKS = 1;

