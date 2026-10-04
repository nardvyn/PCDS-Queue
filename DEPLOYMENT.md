# PCDS Queue deployment

## 1. Run the server on the campus LAN

On the computer hosting MySQL and Flask, configure `backend/.env`, activate the
virtual environment, then run the API:

```powershell
cd backend
.\venv\Scripts\Activate.ps1
python run.py
```

The server listens on port `5000`. Allow inbound TCP port `5000` in Windows
Firewall and use the server's LAN address, for example
`http://192.168.1.20:5000`.

## 2. Point the desktop apps to the server

Before starting Staff, Admin, Kiosk, or TV Display on a client computer, set:

```powershell
$env:PCDS_QUEUE_API_URL = 'http://192.168.1.20:5000'
```

If the variable is absent, each desktop app uses `http://127.0.0.1:5000` for a
single-machine installation. The mobile app has its own Connection Settings
screen where the same LAN address is saved.

## 3. Build Windows release folders

```powershell
dotnet publish desktop\PCDSQueue.Staff\PCDSQueue.Staff\PCDSQueue.Staff.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.Staff
dotnet publish desktop\PCDSQueue.Admin\PCDSQueue.Admin\PCDSQueue.Admin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.Admin
dotnet publish kiosk\PCDSQueue.Kiosk\PCDSQueue.Kiosk\PCDSQueue.Kiosk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.Kiosk
dotnet publish tv-display\PCDSQueue.TVDisplay\PCDSQueue.TVDisplay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release\PCDSQueue.TVDisplay
```

## 4. Build the Android APK

```powershell
cd mobile\PCDSQueueMobile
npx eas-cli build --platform android --profile production
```

EAS requires the project's Expo account credentials. Set `EXPO_PUBLIC_API_URL`
to the HTTPS production server before the build, or enter the LAN address in
the app after installation for campus testing.
