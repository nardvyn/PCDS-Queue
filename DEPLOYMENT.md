# PCDS Queue deployment

## 1. Prepare the production server

Use a dedicated, always-on campus PC with a reserved/static LAN IP. Install
MySQL and Python there, then configure `backend/.env` with production-only
credentials:

```powershell
if (-not (Test-Path backend\.env)) {
    Copy-Item backend\.env.example backend\.env
}
notepad backend\.env
```

Set `APP_ENV=Production`, the server's MySQL host/name, a dedicated non-root
`DB_USER`, its `DB_PASSWORD`, and a unique `JWT_SECRET_KEY` of at least 32
characters. Keep `.env` on the server only; it is ignored by Git.

Create the production database and import `database\schema.sql` only for a
brand-new installation. That schema drops and recreates tables, so do not run it
against an existing production database. Apply each pending migration once,
after taking a backup. Create the application MySQL account separately and
grant only the permissions required by the deployed API; run schema migrations
with a separate administrator account.

## 2. Run Flask behind Waitress

From the repository root on the server:

```powershell
cd backend
py -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python serve.py
```

`serve.py` refuses to start unless production mode, database credentials, and
a non-development JWT secret are configured. It listens on all LAN interfaces
on TCP port 5000 using Waitress, not Flask's debug development server. To
verify locally, open:

```text
http://127.0.0.1:5000/api/health
```

The health endpoint checks MySQL and should return `status: online` and
`database: connected`. From a second campus device, test the same endpoint
using the server's reserved IP, for example
`http://192.168.1.20:5000/api/health`.

Allow inbound TCP 5000 only on the Windows Private network profile and, where
possible, limit the rule to the campus LAN. Do not expose this HTTP service to
the public internet. Configure MySQL and the Waitress process to start
automatically using the campus-approved Windows service or Task Scheduler
setup; no service or firewall changes are made by this repository.

## 3. Point the desktop apps to the server

Before starting Staff, Admin, Kiosk, or TV Display on a client computer, set:

```powershell
$env:PCDS_QUEUE_API_URL = 'http://192.168.1.20:5000'
```

If the variable is absent, each desktop app uses `http://127.0.0.1:5000` for a
single-machine installation. The mobile app has its own Connection Settings
screen where the same LAN address is saved.

## 4. Build Windows release folders

```powershell
dotnet publish desktop\PCDSQueue.Staff\PCDSQueue.Staff\PCDSQueue.Staff.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.Staff
dotnet publish desktop\PCDSQueue.Admin\PCDSQueue.Admin\PCDSQueue.Admin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.Admin
dotnet publish kiosk\PCDSQueue.Kiosk\PCDSQueue.Kiosk\PCDSQueue.Kiosk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.Kiosk
dotnet publish tv-display\PCDSQueue.TVDisplay\PCDSQueue.TVDisplay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.TVDisplay
```

The release executables include the PCDS Queue application icon. Configure
`PCDS_QUEUE_API_URL` on each client PC before launching the app.

## 5. Deploy the API to Railway

For a Railway Python service, set the service Root Directory to `backend` and
the Start Command to (also declared in `backend/Procfile`):

```text
gunicorn run:app --bind 0.0.0.0:$PORT
```

`backend/run.py` exports the Flask application as `app`. Gunicorn is included
for non-Windows installs; Waitress remains the production server for the
Windows campus deployment. Set the required Flask/JWT and database environment
variables in Railway before enabling API routes that need MySQL. Do not commit
the local `backend/.env` file. This Railway command is separate from
`python serve.py`, which is intended for the Windows server.

## 6. Build the Android APK

```powershell
cd mobile\PCDSQueueMobile
npx eas-cli build --platform android --profile production
```

EAS requires the project's Expo account credentials. Set `EXPO_PUBLIC_API_URL`
to the HTTPS production server before the build, or enter the LAN address in
the app after installation for campus testing.

## 7. Backup and launch checks

Before launch, take a database backup and verify that it can be restored.
Schedule recurring backups to storage separate from the database PC and retain
a copy of the deployed source/build version. At the campus, smoke-test
Admin, Staff, Kiosk, TV Display, mobile queue entry, QR expiry/revocation,
ticket calling, and local-network operation with internet disconnected but
campus Wi-Fi/LAN still available.
