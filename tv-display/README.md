# PCDS Queue TV Display

The TV display is a WPF desktop application in `PCDSQueue.TVDisplay`.

## Display

- The main panel emphasizes the currently focused queue number, department,
  and service window.
- Other active calls and per-department waiting counts are populated from the
  queue display API; no department names or sample queue numbers are hard-coded.
- The footer shows the total waiting count and connection state.
- New CALL and RECALL announcement events play the existing ding and voice
  announcement. The recalled call is focused in the main panel and highlighted
  in the active-call list.
- Press `Esc` to leave full-screen display mode.

## Configuration

Set `PCDS_QUEUE_API_URL` to the Flask server base URL when it is not running at
`http://127.0.0.1:5000/`.

The display polls `GET /api/queue/display` using the refresh interval returned
by the API and reads announcement events from
`GET /api/queue/announcement-events`.

## Build

```powershell
dotnet build .\PCDSQueue.TVDisplay\PCDSQueue.TVDisplay.csproj
```
