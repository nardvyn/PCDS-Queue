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

This override is for a campus LAN deployment. Without it, the desktop apps use
the Railway HTTPS API at `https://pcds-queue-production.up.railway.app/`.
The mobile app has its own Connection Settings screen where a LAN address can
be saved.

## 4. Build Windows release folders

```powershell
dotnet publish desktop\PCDSQueue.Staff\PCDSQueue.Staff\PCDSQueue.Staff.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.Staff
dotnet publish desktop\PCDSQueue.Admin\PCDSQueue.Admin\PCDSQueue.Admin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.Admin
dotnet publish kiosk\PCDSQueue.Kiosk\PCDSQueue.Kiosk\PCDSQueue.Kiosk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.Kiosk
dotnet publish tv-display\PCDSQueue.TVDisplay\PCDSQueue.TVDisplay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.TVDisplay
```

The release executables include the PCDS Queue application icon. Set
`PCDS_QUEUE_API_URL` on each client PC only when overriding the Railway default.

### Create desktop installers

After publishing all four apps, install Inno Setup 6, open each `.iss` script
below `installer`, and select **Build > Compile**.

The setup files are written to `release\Installers`:
`PCDS Queue Admin Setup.exe`, `PCDS Queue Staff Setup.exe`,
`PCDS Queue Kiosk Setup.exe`, and `PCDS Queue TV Display Setup.exe`. Each setup
installs its app under Program Files and creates a Start Menu shortcut; a
desktop shortcut is optional in the installer. The package includes the
self-contained app and its published runtime assets. The Staff installer also
packages its WPF native runtime libraries beside the single-file executable;
keep those DLLs with the app when copying a release folder manually.

For an update release, increment `MyAppVersion` in the affected installer
script and keep its existing `AppId` unchanged so Inno Setup upgrades the
installed app. Rebuild only the desktop apps affected by the code changes.

## 5. Deploy the API to Railway

For a Railway Python service, set the service Root Directory to `backend` and
the Start Command to (also declared in `backend/Procfile`):

```text
gunicorn run:app --bind 0.0.0.0:$PORT
```

`backend/run.py` exports the Flask application as `app`. Gunicorn is included
for non-Windows installs; Waitress remains the production server for the
Windows campus deployment. Set `MYSQL_URL` to Railway's `${{MySQL.MYSQL_URL}}`,
`APP_ENV=Production`, and a unique `JWT_SECRET_KEY` in Railway. The Flask config
accepts Railway's `mysql://` URL and selects the PyMySQL driver; local
`DB_HOST`/`DB_PORT`/`DB_NAME`/`DB_USER`/`DB_PASSWORD` settings remain the
fallback. `DATABASE_URL` is also accepted when `MYSQL_URL` is absent. Do not
commit the local `backend/.env` file. This Railway command is separate from
`python serve.py`, which is intended for the Windows server.

Before deploying the call grace-period backend, back up the Railway database
and apply `database/migrations/004_customer_call_grace_period.sql` in its MySQL
console. It adds the acknowledgement timestamp column and inserts default
settings without replacing values already configured by an administrator.
Confirm the migration succeeds before pushing/deploying the backend; do not
import `database/schema.sql` into an existing production database.

## 6. Build the Android APK

```powershell
cd mobile\PCDSQueueMobile
npx eas-cli build --platform android --profile production
```

The mobile API defaults to
`https://pcds-queue-production.up.railway.app`; set `EXPO_PUBLIC_API_URL` only
to override it, for example with a campus LAN address. The production EAS
profile is configured for internal Android APK distribution and automatically
increments the Android build number. For an app update, increment `expo.version`
in `app.json`, keep `android.package` unchanged, and use the existing EAS
signing credentials so Android can install it over the existing app. Download
the completed APK and save it as
`release\PCDS Queue Mobile v<version>.apk`. Install it on Android devices for
QR scanning, joining a queue, and live-status smoke tests.

The mobile app requests Android notification permission when a customer joins
the queue and stores the Expo push token on that active mobile ticket. Railway
sends CALLED and RECALL alerts through Expo Push Service; this uses the existing
`queue_numbers.notification_token` column and needs no additional database
migration. Configure Android FCM v1 credentials for the EAS project before
testing push delivery. Background delivery requires internet access and an
Android device with notifications enabled; sound and vibration remain subject
to the device's notification-channel and Do Not Disturb settings.

For the call grace-period release, deploy and verify the backend first, then
rebuild only Staff and Mobile. Existing Admin, Kiosk, TV Display installers,
and the currently deployed APK can remain installed until their replacements
are ready.

## 7. Backup and launch checks

Before launch, take a database backup and verify that it can be restored.
Schedule recurring backups to storage separate from the database PC and retain
a copy of the deployed source/build version. At the campus, smoke-test
Admin, Staff, Kiosk, TV Display, mobile queue entry, QR expiry/revocation,
ticket calling, and local-network operation with internet disconnected but
campus Wi-Fi/LAN still available.
