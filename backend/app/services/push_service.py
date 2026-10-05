import json
import urllib.error
import urllib.request


EXPO_PUSH_URL = "https://exp.host/--/api/v2/push/send"


def send_queue_push(push_token, title, body, data):
    if not isinstance(push_token, str) or not push_token.startswith(
        ("ExponentPushToken[", "ExpoPushToken[")
    ):
        raise ValueError("A valid Expo push token is required.")

    payload = {
        "to": push_token,
        "title": title,
        "body": body,
        "sound": "notification.wav",
        "priority": "high",
        "ttl": 60,
        "channelId": "queue-calls",
        "data": data,
    }
    request = urllib.request.Request(
        EXPO_PUSH_URL,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Accept": "application/json",
            "Content-Type": "application/json",
        },
        method="POST",
    )

    try:
        with urllib.request.urlopen(request, timeout=5) as response:
            result = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        raise RuntimeError(f"Expo Push Service returned HTTP {error.code}.") from error
    except (urllib.error.URLError, TimeoutError) as error:
        raise RuntimeError("Could not connect to the Expo Push Service.") from error
    except json.JSONDecodeError as error:
        raise RuntimeError("Expo Push Service returned an invalid response.") from error

    if not isinstance(result, dict):
        raise RuntimeError("Expo Push Service returned an invalid response.")

    ticket = result.get("data")
    if isinstance(ticket, list):
        ticket = ticket[0] if ticket else None
    if not isinstance(ticket, dict):
        raise RuntimeError("Expo Push Service returned no delivery ticket.")
    return ticket
