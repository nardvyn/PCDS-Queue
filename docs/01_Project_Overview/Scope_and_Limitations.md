# Scope and Limitations

## Scope

The system includes the following applications and services:

| Component | Scope |
| --- | --- |
| Flask API | Authentication, authorization, queue rules, QR handling, staff shifts, settings, and reporting endpoints |
| MySQL | Persistent storage for departments, users, windows, shifts, queue numbers, events, QR sessions, settings, and audit records |
| Admin Desktop | Management of staff, departments, service windows, live queues, history, reports, QR sessions, and system settings |
| Staff Desktop | Login, dynamic window selection, shift control, queue actions, automatic refresh, and department queue history |
| Kiosk Desktop | Department selection, ticket generation, QR display, and thermal-printer integration |
| TV Display | Live queue display and voice announcement event consumption |
| Mobile App | QR scan, queue confirmation, ticket status, people ahead, estimated wait, cancellation, and local status alerts |

The default deployment supports Cashier, Registrar, and Bookstore, while the
Admin Desktop can add or configure departments and windows.

## Limitations

- Real-time refresh currently uses short polling intervals. It does not yet use
  WebSocket or Socket.IO push updates.
- Mobile notifications are local status alerts while the app is active; a full
  remote push-notification service is outside the current implementation.
- Thermal ticket printing depends on a compatible printer and its Windows
  driver on the kiosk computer.
- The release EXEs target Windows x64. The mobile app requires Expo/EAS account
  credentials to create a signed APK.
- Campus deployment requires a reachable Flask server, MySQL configuration,
  firewall access to the API port, and a consistent LAN or HTTPS address.
- The system manages queue operations. It does not process payments, student
  records, inventory, or other office-specific transactions.
