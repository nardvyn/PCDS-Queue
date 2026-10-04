# Database

MySQL schema, seed data, and local test data for `pcds_queue_db`.

- `schema.sql` — creates the database and all tables (departments, staff_users, service_windows, queue_numbers, queue_counters, qr_sessions, queue_history, audit_logs, system_settings). Safe to re-run (drops and recreates tables).
- `seed.sql` — default departments/windows, one placeholder admin, default system settings. Idempotent.
- `test_data.sql` — sample queue walkthrough data for local development only. Not for production.

Run order: `schema.sql` → `seed.sql` → (optional) `test_data.sql`.
