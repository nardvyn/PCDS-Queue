# PCDS Queue Management System

Queue management for Cashier, Registrar, and Bookstore, served by one Flask API and MySQL database.
No client (Mobile, Kiosk, Desktop, TV Display) connects directly to MySQL — all access goes through the API.

See [System Architecture](docs/01_Project_Overview/System_Architecture.md) for the system architecture and [database/README.md](database/README.md) for the schema.

## Structure

- `database/` — MySQL schema, seed, and test data
- `backend/` — Flask REST API
- `desktop/` — WPF staff/admin desktop app
- `kiosk/` — WPF kiosk app (QR + ticket printing)
- `tv-display/` — read-only WPF TV queue display
- `mobile/` — React Native mobile app
- `docs/` — project documentation

## Development order

1. Database
2. Backend (Flask API)
3. Test backend + database
4. Desktop (staff/admin)
5. Kiosk
6. TV display
7. Mobile
8. Real-time updates + notifications
9. Full system testing
