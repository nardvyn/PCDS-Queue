# PCDS Queue Mobile

React Native / Expo app for scanning a department QR, joining the mobile queue,
tracking the ticket, and cancelling a waiting ticket from the same device.

## Run

```powershell
cd mobile/PCDSQueueMobile
npm install
npx expo start
```

The Android emulator defaults to `http://10.0.2.2:5000`. For a physical phone,
open Connection Settings in the app and save the Flask server's LAN address,
for example `http://192.168.1.20:5000`. The phone and server must be on the same
network, and the server must allow inbound connections on port 5000.

## Android APK

Install and sign in to EAS CLI, then run:

```powershell
npx eas-cli build --platform android --profile production
```

The production profile creates an installable APK. Use HTTPS for a production
API; cleartext HTTP is enabled here only for local development and campus LAN
testing.
