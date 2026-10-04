-- Track when a mobile device first validates a kiosk QR token.
USE pcds_queue_db;

ALTER TABLE qr_sessions
    ADD COLUMN scanned_at DATETIME NULL AFTER token_hash;