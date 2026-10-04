# Project Description

## PCDS Queue Management System

The PCDS Queue Management System is a centralized queueing solution for the
Polytechnic College of Davao del Sur. It manages service queues for offices
such as Cashier, Registrar, and Bookstore through one Flask REST API and a
MySQL database.

Students can obtain queue numbers from the self-service kiosk or mobile app.
Staff members log in to the Staff Desktop, select an available service window,
start a shift, and serve customers in order. Administrators use the Admin
Desktop to manage staff accounts, departments, service windows, QR sessions,
system settings, queue history, and reports. The TV Display presents live
queue calls and announcements in waiting areas.

The solution separates each client from the database. Admin, Staff, Kiosk, TV
Display, and Mobile clients communicate only with the Flask API, which applies
authentication, authorization, queue rules, and database transactions.

## Key capabilities

- Department-based queue numbers with configurable prefixes and window counts
- JWT-authenticated Admin and Staff access
- Dynamic staff window selection and active-shift tracking
- Queue controls: next, recall, complete, and no-show
- Queue generation from Kiosk and Mobile channels
- QR sessions with configurable expiration and revocation
- Mobile and Kiosk queue availability controls
- Live staff dashboard, queue history, reports, TV display data, and voice announcement events
- Configurable API address for local or campus-LAN deployment

## Mobile app interface text

This section records the customer-facing text currently used in the PCDS Queue
mobile app. Values in square brackets are filled in at runtime from queue,
department, server, or preference data. The interface is currently in English;
the voice language setting changes the speech voice, not the written labels.

### Shared app text

- Brand: `PCDS QUEUE` / `CAMPUS SERVICES`
- Connection: `QUEUE SERVER CONNECTED` / `CONNECTING TO QUEUE SERVER`
- Footer: `POLYTECHNIC COLLEGE OF DAVAO DEL SUR` / `PCDS QUEUE · [LIVE or OFFLINE]`
- Bottom navigation: `Home` / `My Queue` / `Settings`

### Home

- Heading: `YOUR CAMPUS, IN ORDER`
- Title: `Join a queue.`
- Description: `Get your queue number without waiting in line.`
- Scan action: `SCAN DEPARTMENT QR`
- Scan description: `Department is detected automatically`
- When an active ticket exists, the disabled scan action reads `Ticket active` with `Open your current queue ticket above`.
- Services label and list: `SERVICES` / `Cashier · Registrar · Bookstore`
- Active ticket card: `ACTIVE TICKET`, `[queue number]`, `[department]`, and the current status.
- Active ticket detail: `[people ahead] people ahead`; when called, `Proceed to [window]`; when serving, `Being served at [window]` (or `Being served` if no window name is available).
- Status labels include `WAITING`, `CALLED`, `NOW SERVING`, `COMPLETED`, `CANCELLED`, and `NO SHOW`.

### QR scanner and department confirmation

- Permission prompt: `Camera access needed` / `Allow camera access to scan the department QR.` / `Allow camera`
- Scanner prompt: `Scan department QR` / `Hold the code inside the frame.`
- QR confirmation: `Back` / `QR VERIFIED` / `Confirm your service`
- Department state: `[department]` / `Queue is open` or `Queue is closed`
- Helper text: `This QR belongs to [department].` or `Queue entry is unavailable for [department].`
- Queue information labels: `CURRENT SERVING` / `WAITING` / `ACTIVE WINDOWS`
- Main action: `JOIN QUEUE`; while submitting: `Joining queue…`
- Closed queue notice: `This queue is currently closed. Please try again when it opens.`
- Disabled mobile entry notice: `Mobile queue entry is currently disabled by the administrator.`

### My Queue

- Navigation/back labels: `Home` / `YOUR QUEUE TICKET`
- Ticket details: `[department]`, `[queue number]`, `QUEUE NUMBER`, `PEOPLE AHEAD`, `EST. WAIT`, `NOW SERVING`, and `[window name]` when available.
- Unknown or unavailable numeric values are shown as `—`; estimated wait is shown as `~[minutes]m`.
- Connection state: `LIVE` / `OFFLINE`
- Call notice: `QUEUE CALL` / `[queue number]` / `Please proceed to` / `[department] • [window]` / `Your number is being called.`
- Recall notice: `QUEUE RECALL` / `[queue number]` / `Please proceed to` / `[department] • [window]` / `Your number has been recalled.`
- If a call/recall has already been announced and the ticket is now serving, the notice may show `Status: NOW SERVING`.
- Other ticket notices: `Your number is being called. Please proceed to [window].` / `You are now being served at [window].`
- Ticket actions: `Cancel queue` / `DONE`
- Cancel confirmation: `Cancel queue number?` / `[queue number] will be removed from the waiting line.` / `Keep ticket` / `Cancel ticket`

### Full-screen queue alerts

- Call alert: `YOUR NUMBER IS CALLED` / `Please proceed` / `[queue number]` / `PLEASE PROCEED TO` / `[department]` / `[window]` / `Your number is being called.`
- Recall alert: `QUEUE RECALL` / `Your number was recalled` / `[queue number]` / `PLEASE PROCEED TO` / `[department]` / `[window]` / `Your number has been recalled.`
- Test alert: `TEST ALERT` / `Check your alert settings` / `TEST` / `This is a PCDS Queue test alert.`
- Acknowledge action: `STOP RINGING · ACKNOWLEDGE`
- Spoken call: `Your queue number [queue number] is being called. Please proceed to [department and window].`
- Spoken recall: `Your queue number [queue number] is being recalled. Please proceed to [department and window].`
- Spoken test: `This is a PCDS Queue test alert.`

### Settings

- Heading: `PERSONAL PREFERENCES` / `Settings` / `Choose how this phone keeps you updated.`
- Appearance: `Appearance` / `Dark Mode` / `Use a darker color theme throughout the app`
- Notifications:
  - `Notifications`
  - `Queue Alerts` — `Show alerts when your number is called or recalled`
  - `Recall Alert` — `Repeat sound, vibration, and voice when recalled`
  - `Vibrate When Called` — `Vibrate until you acknowledge the alert`
  - `Ring When Called` — `Repeat the alert sound until acknowledged`
  - `RING VOLUME` — `[percentage]%` — `Adjusts this app only, not the system volume.`
  - `Voice Announcement` — `Speak your queue number and service window`
  - `TEST ALERT`
- Voice: `Voice` / `VOICE LANGUAGE` / `English` / `Filipino` / `VOICE VOLUME` / `[percentage]%`
- Queue: `Queue` / `Auto Refresh` / `Automatically update your queue status` / `REFRESH INTERVAL` / `3 sec` / `5 sec` / `10 sec` / `Keep Screen Awake` / `Keep the display on while waiting in the queue`
- Connection: `Connection` / `Queue Server Connected` or `Queue Server Offline` / `SERVER` / `[server address]` (fallback: `PCDS Local Network`)
- About: `About` / `PCDS Queue` / `Version 1.0.0`

### User-facing errors

- `Could not load saved settings. Default settings are in use.`
- `Could not save settings. Check your device storage and try again.`
- `Could not keep the screen awake on this device.`
- `Device setup is incomplete. Please restart the app and try again.`
- `Camera permission is needed to scan a department QR code.`
- `Something went wrong. Please try again.`
- `The queue server could not complete the request.`
- `The server took too long to respond. Check your connection and try again.`
- `Cannot connect to the PCDS Queue server. Check the API address and network.`
- When the server provides an error message, that message is displayed in the app.
