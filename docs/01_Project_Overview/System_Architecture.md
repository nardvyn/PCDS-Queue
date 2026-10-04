# System Architecture

```mermaid
flowchart TB
    DB[(MySQL Database)]
    API[Flask REST API]
    DB <--> API
    ADMIN[Admin Desktop]
    STAFF[Staff Desktop]
    KIOSK[Kiosk Desktop]
    TV[TV Display]
    MOBILE[Mobile App]
    ADMIN <--> API
    STAFF <--> API
    KIOSK <--> API
    TV <--> API
    MOBILE <--> API
```

Every client uses HTTP API endpoints. MySQL is accessible only to the Flask
backend. Staff queue actions are resolved from the authenticated user's active
shift, so the desktop client does not submit a staff ID or window ID when it
calls the next customer.
