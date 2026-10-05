import hashlib
from datetime import date, datetime

from flask import Blueprint, jsonify, request
from flask_jwt_extended import get_jwt_identity, jwt_required
from sqlalchemy import text
from sqlalchemy.exc import IntegrityError

from app import db

queue_bp = Blueprint(
    "queue",
    __name__,
    url_prefix="/api/queue"
)


def _get_call_grace_period():
    value = db.session.execute(text("""
        SELECT setting_value FROM system_settings
        WHERE setting_key = 'call_grace_period_seconds' LIMIT 1
    """)).scalar()
    try:
        return max(0, int(value))
    except (TypeError, ValueError):
        return 60


def _get_queue_rule(key, default=True):
    value = db.session.execute(text("""
        SELECT setting_value FROM system_settings
        WHERE setting_key = :key LIMIT 1
    """), {"key": key}).scalar()
    if value is None:
        return default
    return str(value).strip().lower() in {"true", "1", "yes", "on"}


def _grace_remaining_seconds(status, called_at, grace_period):
    if status not in ("CALLED", "SERVING") or not called_at:
        return 0
    return max(0, grace_period - int((datetime.now() - called_at).total_seconds()))


@queue_bp.route("/generate", methods=["GET", "POST"])
def generate_queue():
    if request.method == "GET":
        return jsonify({
            "message": "Use POST to generate a queue number.",
            "example": {
                "method": "POST",
                "body": {
                    "department_id": 1,
                    "source": "KIOSK"
                }
            },
            "allowed_sources": ["MOBILE", "KIOSK"]
        }), 200

    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        data = {}

    department_id = data.get("department_id")
    requested_source = str(data.get("source", "MOBILE")).upper()
    source = "MOBILE" if requested_source == "MOBILE" else "KIOSK"
    device_identifier = data.get("device_identifier")
    notification_token = data.get("notification_token")
    qr_token = data.get("qr_token")

    if isinstance(department_id, bool) or not isinstance(department_id, int) or department_id <= 0:
        return jsonify({"message": "A valid department_id is required."}), 400

    if requested_source not in ("MOBILE", "KIOSK", "TICKET"):
        return jsonify({"message": "source must be MOBILE or KIOSK."}), 400

    if source == "MOBILE" and (
        not isinstance(qr_token, str) or not qr_token.strip() or len(qr_token) > 200
    ):
        return jsonify({
            "valid": False,
            "reason": "INVALID",
            "message": "A valid department QR code is required to join the mobile queue."
        }), 400

    if source == "MOBILE" and (
        not isinstance(device_identifier, str)
        or not device_identifier.strip()
        or len(device_identifier) > 128
    ):
        return jsonify({"message": "A valid device_identifier is required for mobile queue entry."}), 400

    database_source = "MOBILE" if source == "MOBILE" else "TICKET"

    today = date.today()

    try:
        department = _get_department(department_id)

        if not department:
            return jsonify({"message": "Department not found."}), 404
        if not department["is_active"]:
            return jsonify({"message": "Department is currently inactive."}), 400
        if not department["queue_is_open"]:
            return jsonify({"message": "Queue is currently closed."}), 400

        if source == "MOBILE":
            token_hash = hashlib.sha256(qr_token.strip().encode("utf-8")).hexdigest()
            qr_session = db.session.execute(
                text("""
                    SELECT qs.department_id, qs.status, qs.expires_at, d.is_active
                    FROM qr_sessions qs
                    JOIN departments d ON d.department_id = qs.department_id
                    WHERE qs.token_hash = :token_hash
                    LIMIT 1
                    FOR UPDATE
                """),
                {"token_hash": token_hash}
            ).mappings().first()

            if not qr_session:
                return jsonify({
                    "valid": False,
                    "reason": "INVALID",
                    "message": "This QR code is not recognized by the PCDS Queue System."
                }), 404

            if qr_session["status"] == "EXPIRED" or (
                qr_session["status"] == "ACTIVE"
                and qr_session["expires_at"] <= datetime.now()
            ):
                if qr_session["status"] == "ACTIVE":
                    db.session.execute(
                        text("""
                            UPDATE qr_sessions
                            SET status = 'EXPIRED'
                            WHERE token_hash = :token_hash AND status = 'ACTIVE'
                        """),
                        {"token_hash": token_hash}
                    )
                    db.session.commit()
                return jsonify({
                    "valid": False,
                    "reason": "EXPIRED",
                    "message": "This QR code has expired. Please scan the latest PCDS Queue QR code."
                }), 410

            if qr_session["status"] != "ACTIVE" or not qr_session["is_active"]:
                return jsonify({
                    "valid": False,
                    "reason": "INACTIVE",
                    "message": "This QR code is no longer active."
                }), 410

            if qr_session["department_id"] != department_id:
                return jsonify({
                    "valid": False,
                    "reason": "DEPARTMENT_MISMATCH",
                    "message": "This QR code does not belong to the requested department."
                }), 403

        setting_rows = db.session.execute(
            text("""
                SELECT setting_key, setting_value
                FROM system_settings
                WHERE setting_key IN (
                    'queue_digits', 'queue_number_digits',
                    'mobile_queue_enabled', 'kiosk_queue_enabled'
                )
            """)
        ).mappings().all()
        settings = {row["setting_key"]: row["setting_value"] for row in setting_rows}

        enabled_key = "mobile_queue_enabled" if source == "MOBILE" else "kiosk_queue_enabled"
        if str(settings.get(enabled_key, "true")).strip().lower() not in ("true", "1", "yes", "on"):
            channel_name = "Mobile" if source == "MOBILE" else "Kiosk"
            return jsonify({"message": f"{channel_name} queue entry is currently disabled."}), 403

        try:
            queue_digits = int(settings.get("queue_digits", settings.get("queue_number_digits", 3)))
        except (TypeError, ValueError):
            queue_digits = 3
        queue_digits = min(6, max(2, queue_digits))

        # Prevent one mobile device from holding two active tickets in the same department.
        if source == "MOBILE" and device_identifier:
            existing = db.session.execute(
                text("""
                    SELECT queue_id, queue_number, status
                    FROM queue_numbers
                    WHERE department_id = :department_id
                      AND queue_date = :queue_date
                      AND device_identifier = :device_identifier
                      AND status IN ('WAITING', 'CALLED', 'SERVING')
                    LIMIT 1
                """),
                {
                    "department_id": department_id,
                    "queue_date": today,
                    "device_identifier": device_identifier
                }
            ).mappings().first()

            if existing:
                return jsonify({
                    "message": "Device already has an active queue.",
                    "queue": {
                        "queue_id": existing["queue_id"],
                        "queue_number": existing["queue_number"],
                        "status": existing["status"]
                    }
                }), 409

        # Ensure a counter row exists for this department/day.
        db.session.execute(
            text("""
                INSERT INTO queue_counters (department_id, queue_date, last_number)
                VALUES (:department_id, :queue_date, 0)
                ON DUPLICATE KEY UPDATE department_id = department_id
            """),
            {"department_id": department_id, "queue_date": today}
        )

        # Lock the counter row so concurrent requests can't hand out the same number.
        counter = db.session.execute(
            text("""
                SELECT counter_id, last_number
                FROM queue_counters
                WHERE department_id = :department_id
                  AND queue_date = :queue_date
                FOR UPDATE
            """),
            {"department_id": department_id, "queue_date": today}
        ).mappings().first()

        next_number = counter["last_number"] + 1
        # 1 -> C001, 10 -> C010, 1000 -> C1000
        queue_number = f"{department['queue_prefix']}{next_number:0{queue_digits}d}"

        db.session.execute(
            text("""
                UPDATE queue_counters
                SET last_number = :next_number
                WHERE counter_id = :counter_id
            """),
            {"next_number": next_number, "counter_id": counter["counter_id"]}
        )

        result = db.session.execute(
            text("""
                INSERT INTO queue_numbers (
                    department_id, sequence_number, queue_number, queue_date,
                    source, status, device_identifier, notification_token
                )
                VALUES (
                    :department_id, :sequence_number, :queue_number, :queue_date,
                    :source, 'WAITING', :device_identifier, :notification_token
                )
            """),
            {
                "department_id": department_id,
                "sequence_number": next_number,
                "queue_number": queue_number,
                "queue_date": today,
                "source": database_source,
                "device_identifier": device_identifier,
                "notification_token": notification_token
            }
        )

        queue_id = result.lastrowid

        _record_event(queue_id, "CREATED", notes=f"Queue created from {source}.")

        db.session.commit()

        people_ahead = db.session.execute(
            text("""
                SELECT COUNT(*)
                FROM queue_numbers
                WHERE department_id = :department_id
                  AND queue_date = :queue_date
                  AND status = 'WAITING'
                  AND sequence_number < :sequence_number
            """),
            {
                "department_id": department_id,
                "queue_date": today,
                "sequence_number": next_number
            }
        ).scalar()

        return jsonify({
            "message": "Queue number generated.",
            "queue": {
                "queue_id": queue_id,
                "queue_number": queue_number,
                "sequence_number": next_number,
                "department": {
                    "department_id": department["department_id"],
                    "department_name": department["department_name"],
                    "queue_prefix": department["queue_prefix"]
                },
                "source": source,
                "status": "WAITING",
                "people_ahead": people_ahead
            }
        }), 201

    except IntegrityError:
        db.session.rollback()
        return jsonify({"message": "Queue conflict occurred. Please try again."}), 409

    except Exception as error:
        db.session.rollback()
        return jsonify({"message": "Unable to generate queue.", "error": str(error)}), 500


@queue_bp.route("/status/<int:department_id>", methods=["GET"])
def department_status(department_id):
    """Waiting count, next waiting ticket, per-window serving, and recent calls."""
    try:
        department = _get_department(department_id)

        if not department:
            return jsonify({"message": "Department not found."}), 404

        kiosk_setting = db.session.execute(
            text("""
                SELECT setting_value
                FROM system_settings
                WHERE setting_key = 'kiosk_queue_enabled'
                LIMIT 1
            """)
        ).scalar()
        kiosk_queue_enabled = (
            True
            if kiosk_setting is None
            else str(kiosk_setting).strip().lower() in {"true", "1", "yes", "on"}
        )

        waiting_count = _waiting_count(department_id)

        next_waiting = db.session.execute(
            text("""
                SELECT queue_id, queue_number
                FROM queue_numbers
                WHERE department_id = :department_id
                  AND queue_date = CURDATE()
                  AND status = 'WAITING'
                ORDER BY sequence_number ASC
                LIMIT 1
            """),
            {"department_id": department_id}
        ).mappings().first()

        serving = _serving_queues(department_id, include_called_at=True)
        active_window_count = db.session.execute(
            text("""
                SELECT COUNT(*)
                FROM staff_shifts
                WHERE department_id = :department_id
                  AND status = 'ACTIVE'
            """),
            {"department_id": department_id}
        ).scalar()

        recently_called_rows = db.session.execute(
            text("""
                SELECT q.queue_id, q.queue_number, q.status, q.called_at,
                       sw.window_id, sw.window_number, sw.window_name
                FROM queue_numbers q
                LEFT JOIN service_windows sw ON sw.window_id = q.window_id
                WHERE q.department_id = :department_id
                  AND q.queue_date = CURDATE()
                  AND q.called_at IS NOT NULL
                ORDER BY q.called_at DESC
                LIMIT 10
            """),
            {"department_id": department_id}
        ).mappings().all()

        return jsonify({
            "department": {
                "department_id": department["department_id"],
                "department_name": department["department_name"],
                "queue_prefix": department["queue_prefix"],
                "is_active": bool(department["is_active"]),
                "queue_is_open": bool(department["queue_is_open"]),
                "kiosk_queue_enabled": kiosk_queue_enabled
            },
            "waiting_count": waiting_count,
            "active_window_count": int(active_window_count or 0),
            "next_waiting": (
                {"queue_id": next_waiting["queue_id"], "queue_number": next_waiting["queue_number"]}
                if next_waiting else None
            ),
            "serving": serving,
            "recently_called": [
                _queue_display_row(row, include_called_at=True) for row in recently_called_rows
            ]
        }), 200

    except Exception as error:
        return jsonify({"message": "Unable to retrieve queue status.", "error": str(error)}), 500


@queue_bp.route("/display", methods=["GET"])
def queue_display():
    """Read-only snapshot for the TV display: all active departments."""
    try:
        setting_rows = db.session.execute(
            text("""
                SELECT setting_key, setting_value
                FROM system_settings
                WHERE setting_key IN ('tv_voice_enabled', 'refresh_interval')
            """)
        ).mappings().all()
        settings = {row["setting_key"]: row["setting_value"] for row in setting_rows}
        voice_setting = settings.get("tv_voice_enabled")
        tv_voice_enabled = (
            True
            if voice_setting is None
            else str(voice_setting).strip().lower() in {"true", "1", "yes", "on"}
        )
        try:
            refresh_interval = int(settings.get("refresh_interval", 3))
        except (TypeError, ValueError):
            refresh_interval = 3
        refresh_interval = min(60, max(1, refresh_interval))

        departments = db.session.execute(
            text("""
                SELECT department_id, department_name, queue_prefix
                FROM departments
                WHERE is_active = 1
                ORDER BY department_id ASC
            """)
        ).mappings().all()

        return jsonify({
            "tv_voice_enabled": tv_voice_enabled,
            "refresh_interval": refresh_interval,
            "departments": [
                {
                    "department_id": department["department_id"],
                    "department_name": department["department_name"],
                    "queue_prefix": department["queue_prefix"],
                    "waiting_count": _waiting_count(department["department_id"]),
                    "serving": _serving_queues(department["department_id"])
                }
                for department in departments
            ]
        }), 200

    except Exception as error:
        return jsonify({"message": "Unable to retrieve display data.", "error": str(error)}), 500


@queue_bp.route("/announcement-events", methods=["GET"])
@queue_bp.route("/announcements", methods=["GET"])
def queue_announcements():
    raw_after_event_id = request.args.get("after_event_id", "0")
    try:
        after_event_id = int(raw_after_event_id)
    except (TypeError, ValueError):
        return jsonify({"message": "after_event_id must be a non-negative integer."}), 400

    if after_event_id < 0:
        return jsonify({"message": "after_event_id must be a non-negative integer."}), 400

    queue_id = request.args.get("queue_id", type=int)
    device_identifier = request.args.get("device_identifier", type=str)
    if (queue_id is None) != (device_identifier is None):
        return jsonify({"message": "queue_id and device_identifier must be provided together."}), 400

    try:
        if queue_id is not None:
            owned_queue = db.session.execute(
                text("""
                    SELECT queue_id FROM queue_numbers
                    WHERE queue_id = :queue_id
                      AND device_identifier = :device_identifier
                      AND source = 'MOBILE'
                """),
                {"queue_id": queue_id, "device_identifier": device_identifier}
            ).scalar()
            if owned_queue is None:
                return jsonify({"message": "Queue ticket not found."}), 404

        event_rows = db.session.execute(
            text("""
                SELECT e.event_id, e.queue_id, e.event_type, e.created_at,
                       q.queue_number, d.department_name,
                       sw.window_number, sw.window_name
                FROM queue_events e
                JOIN queue_numbers q ON q.queue_id = e.queue_id
                JOIN departments d ON d.department_id = q.department_id
                LEFT JOIN service_windows sw
                    ON sw.window_id = COALESCE(e.window_id, q.window_id)
                WHERE e.event_id > :after_event_id
                  AND e.event_type IN ('CALLED', 'RECALLED')
                  AND (:queue_id IS NULL OR e.queue_id = :queue_id)
                ORDER BY e.event_id ASC
                LIMIT 100
            """),
            {"after_event_id": after_event_id, "queue_id": queue_id}
        ).mappings().all()

        latest_event_id = int(db.session.execute(
            text("SELECT COALESCE(MAX(event_id), 0) FROM queue_events")
        ).scalar() or 0)
        next_after_event_id = (
            int(event_rows[-1]["event_id"])
            if len(event_rows) == 100
            else latest_event_id
        )

        return jsonify({
            "events": [
                {
                    "event_id": row["event_id"],
                    "queue_id": row["queue_id"],
                    "event_type": row["event_type"],
                    "queue_number": row["queue_number"],
                    "department_name": row["department_name"],
                    "window_number": row["window_number"],
                    "window_name": row["window_name"],
                    "created_at": _isoformat(row["created_at"])
                }
                for row in event_rows
            ],
            "latest_event_id": latest_event_id,
            "next_after_event_id": next_after_event_id
        }), 200
    except Exception as error:
        db.session.rollback()
        return jsonify({"message": "Unable to retrieve queue announcements.", "error": str(error)}), 500


@queue_bp.route("/dashboard", methods=["GET"])
@jwt_required()
def queue_dashboard():
    """Dashboard snapshot scoped to the caller's active staff shift."""
    try:
        staff_id = int(get_jwt_identity())
        shift = db.session.execute(
            text("""
                SELECT ss.department_id, ss.window_id,
                       d.department_name,
                       sw.window_number, sw.window_name
                FROM staff_shifts ss
                JOIN departments d ON d.department_id = ss.department_id
                JOIN service_windows sw ON sw.window_id = ss.window_id
                WHERE ss.staff_id = :staff_id
                  AND ss.status = 'ACTIVE'
                ORDER BY ss.shift_id DESC
                LIMIT 1
            """),
            {"staff_id": staff_id}
        ).mappings().first()

        if not shift:
            return jsonify({"message": "No active shift."}), 403

        current = db.session.execute(
            text("""
                SELECT queue_id, queue_number, status, called_at, customer_acknowledged_at
                FROM queue_numbers
                WHERE department_id = :department_id
                  AND window_id = :window_id
                  AND staff_id = :staff_id
                  AND queue_date = CURDATE()
                  AND status IN ('CALLED', 'SERVING')
                ORDER BY queue_id DESC
                LIMIT 1
            """),
            {
                "department_id": shift["department_id"],
                "window_id": shift["window_id"],
                "staff_id": staff_id
            }
        ).mappings().first()

        waiting_count = _waiting_count(shift["department_id"])
        next_queue = db.session.execute(
            text("""
                SELECT queue_id, queue_number
                FROM queue_numbers
                WHERE department_id = :department_id
                  AND queue_date = CURDATE()
                  AND status = 'WAITING'
                ORDER BY sequence_number ASC
                LIMIT 1
            """),
            {"department_id": shift["department_id"]}
        ).mappings().first()

        grace_period = _get_call_grace_period()
        lock_no_show = _get_queue_rule("lock_no_show_during_grace")
        current_remaining = _grace_remaining_seconds(
            current["status"], current["called_at"], grace_period
        ) if current else 0

        return jsonify({
            "department": shift["department_name"],
            "window": {
                "window_id": shift["window_id"],
                "window_number": shift["window_number"],
                "window_name": shift["window_name"]
            },
            "current": (
                {
                    "queue_id": current["queue_id"],
                    "queue_number": current["queue_number"],
                    "status": current["status"],
                    "grace_remaining_seconds": current_remaining,
                    "can_no_show": not lock_no_show or current_remaining <= 0,
                    "customer_acknowledged": current["customer_acknowledged_at"] is not None
                }
                if current else None
            ),
            "waiting_count": waiting_count,
            "next": (
                {
                    "queue_id": next_queue["queue_id"],
                    "queue_number": next_queue["queue_number"]
                }
                if next_queue else None
            )
        }), 200
    except (TypeError, ValueError):
        return jsonify({"message": "Invalid staff session."}), 401
    except Exception:
        return jsonify({"message": "Unable to load queue dashboard."}), 500


@queue_bp.route("/history", methods=["GET"])
@jwt_required()
def queue_history():
    """Latest queue transactions in the authenticated staff member's department."""
    try:
        staff_id = int(get_jwt_identity())
        staff = db.session.execute(
            text("""
                SELECT department_id
                FROM staff_users
                WHERE staff_id = :staff_id
                  AND role = 'STAFF'
                  AND account_status = 'ACTIVE'
                LIMIT 1
            """),
            {"staff_id": staff_id}
        ).mappings().first()

        if not staff or not staff["department_id"]:
            return jsonify({"message": "Staff department not found."}), 404

        rows = db.session.execute(
            text("""
                SELECT q.queue_id, q.queue_number, q.status,
                       q.created_at, q.called_at, q.completed_at,
                       sw.window_number, sw.window_name,
                       su.full_name AS staff_name
                FROM queue_numbers q
                LEFT JOIN service_windows sw ON sw.window_id = q.window_id
                LEFT JOIN staff_users su ON su.staff_id = q.staff_id
                WHERE q.department_id = :department_id
                ORDER BY q.queue_id DESC
                LIMIT 100
            """),
            {"department_id": staff["department_id"]}
        ).mappings().all()

        return jsonify({
            "history": [
                {
                    "queue_id": row["queue_id"],
                    "queue_number": row["queue_number"],
                    "status": row["status"],
                    "window_number": row["window_number"],
                    "window_name": row["window_name"],
                    "staff_name": row["staff_name"],
                    "created_at": _isoformat(row["created_at"]),
                    "called_at": _isoformat(row["called_at"]),
                    "completed_at": _isoformat(row["completed_at"])
                }
                for row in rows
            ]
        }), 200
    except (TypeError, ValueError):
        return jsonify({"message": "Invalid staff session."}), 401
    except Exception:
        return jsonify({"message": "Unable to load queue history."}), 500


@queue_bp.route("/<int:queue_id>/status", methods=["GET"])
def customer_queue_status(queue_id):
    """Single-ticket status for the mobile app (no auth — scoped only to this queue_id)."""
    try:
        queue = db.session.execute(
            text("""
                SELECT q.queue_id, q.department_id, q.sequence_number,
                       q.queue_number, q.status, q.source, q.created_at, q.called_at, q.customer_acknowledged_at,
                       d.department_name,
                       sw.window_number, sw.window_name
                FROM queue_numbers q
                JOIN departments d ON d.department_id = q.department_id
                LEFT JOIN service_windows sw ON sw.window_id = q.window_id
                WHERE q.queue_id = :queue_id
                LIMIT 1
            """),
            {"queue_id": queue_id}
        ).mappings().first()

        if not queue:
            return jsonify({"message": "Queue not found."}), 404

        people_ahead = 0
        if queue["status"] == "WAITING":
            people_ahead = db.session.execute(
                text("""
                    SELECT COUNT(*)
                    FROM queue_numbers
                    WHERE department_id = :department_id
                      AND queue_date = CURDATE()
                      AND status = 'WAITING'
                      AND sequence_number < :sequence_number
                """),
                {
                    "department_id": queue["department_id"],
                    "sequence_number": queue["sequence_number"]
                }
            ).scalar()

        active_count = db.session.execute(
            text("""
                SELECT COUNT(*)
                FROM queue_numbers
                WHERE department_id = :department_id
                  AND queue_date = CURDATE()
                  AND status IN ('CALLED', 'SERVING')
            """),
            {"department_id": queue["department_id"]}
        ).scalar()
        average_service_seconds = db.session.execute(
            text("""
                SELECT AVG(TIMESTAMPDIFF(SECOND, serving_at, completed_at))
                FROM queue_numbers
                WHERE department_id = :department_id
                  AND queue_date = CURDATE()
                  AND status = 'COMPLETED'
                  AND serving_at IS NOT NULL
                  AND completed_at > serving_at
            """),
            {"department_id": queue["department_id"]}
        ).scalar()
        average_service_seconds = max(60, int(average_service_seconds or 300))
        estimated_wait_minutes = (
            int((people_ahead + active_count) * average_service_seconds / 60 + 0.999)
            if queue["status"] == "WAITING"
            else 0
        )
        current_serving = db.session.execute(
            text("""
                SELECT queue_number
                FROM queue_numbers
                WHERE department_id = :department_id
                  AND queue_date = CURDATE()
                  AND status IN ('CALLED', 'SERVING')
                ORDER BY called_at DESC, sequence_number DESC
                LIMIT 1
            """),
            {"department_id": queue["department_id"]}
        ).scalar()

        grace_period = _get_call_grace_period()
        countdown_enabled = _get_queue_rule("enable_call_countdown")
        lock_no_show = _get_queue_rule("lock_no_show_during_grace")
        acknowledgement_enabled = _get_queue_rule("allow_customer_acknowledgement")
        show_people_ahead = _get_queue_rule("show_people_ahead")
        show_estimated_wait = _get_queue_rule("show_estimated_wait_time", False)
        remaining = _grace_remaining_seconds(queue["status"], queue["called_at"], grace_period)

        return jsonify({
            "queue_id": queue["queue_id"],
            "queue_number": queue["queue_number"],
            "department": queue["department_name"],
            "status": queue["status"],
            "people_ahead": people_ahead if show_people_ahead else 0,
            "estimated_wait_minutes": estimated_wait_minutes if show_estimated_wait else 0,
            "current_serving": current_serving,
            "window_number": queue["window_number"],
            "window_name": queue["window_name"],
            "called_at": _isoformat(queue["called_at"]),
            "grace_period_seconds": grace_period,
            "grace_remaining_seconds": remaining,
            "call_countdown_enabled": countdown_enabled,
            "customer_acknowledgement_enabled": acknowledgement_enabled,
            "can_no_show": queue["status"] in ("CALLED", "SERVING") and (not lock_no_show or remaining <= 0),
            "customer_acknowledged": queue["customer_acknowledged_at"] is not None
        }), 200

    except Exception as error:
        return jsonify({"message": "Unable to retrieve queue.", "error": str(error)}), 500


@queue_bp.route("/<int:queue_id>/acknowledge", methods=["POST"])
def acknowledge_queue(queue_id):
    if not _get_queue_rule("allow_customer_acknowledgement"):
        return jsonify({"message": "Customer acknowledgement is disabled."}), 403
    try:
        queue = db.session.execute(text("""
            SELECT queue_id, status FROM queue_numbers
            WHERE queue_id = :queue_id LIMIT 1
        """), {"queue_id": queue_id}).mappings().first()
        if not queue:
            return jsonify({"message": "Queue not found."}), 404
        if queue["status"] not in ("CALLED", "SERVING"):
            return jsonify({"message": "Queue is not currently being called."}), 400
        db.session.execute(text("""
            UPDATE queue_numbers
            SET customer_acknowledged_at = COALESCE(customer_acknowledged_at, NOW())
            WHERE queue_id = :queue_id
        """), {"queue_id": queue_id})
        db.session.commit()
        return jsonify({"message": "Acknowledged.", "customer_acknowledged": True}), 200
    except Exception as error:
        db.session.rollback()
        return jsonify({"message": "Unable to acknowledge queue.", "error": str(error)}), 500


@queue_bp.route("/<int:queue_id>/cancel", methods=["POST"])
def cancel_customer_queue(queue_id):
    if not _get_queue_rule("allow_mobile_cancellation"):
        return jsonify({"message": "Mobile queue cancellation is disabled."}), 403
    data = request.get_json(silent=True)
    device_identifier = data.get("device_identifier") if isinstance(data, dict) else None
    if not isinstance(device_identifier, str) or not device_identifier.strip():
        return jsonify({"message": "device_identifier is required."}), 400

    try:
        queue = db.session.execute(
            text("""
                SELECT queue_id, queue_number, source, status, device_identifier
                FROM queue_numbers
                WHERE queue_id = :queue_id
                LIMIT 1
                FOR UPDATE
            """),
            {"queue_id": queue_id}
        ).mappings().first()

        if not queue:
            return jsonify({"message": "Queue not found."}), 404
        if queue["source"] != "MOBILE" or queue["device_identifier"] != device_identifier.strip():
            db.session.rollback()
            return jsonify({"message": "This queue does not belong to this device."}), 403
        if queue["status"] != "WAITING":
            db.session.rollback()
            return jsonify({"message": "Only a waiting queue can be cancelled."}), 409

        db.session.execute(
            text("""
                UPDATE queue_numbers
                SET status = 'CANCELLED', cancelled_at = :cancelled_at
                WHERE queue_id = :queue_id
            """),
            {"cancelled_at": datetime.now(), "queue_id": queue_id}
        )
        _record_event(queue_id, "CANCELLED", notes="Queue cancelled by its mobile device.")
        db.session.commit()

        return jsonify({
            "message": "Queue cancelled.",
            "queue_id": queue_id,
            "queue_number": queue["queue_number"],
            "status": "CANCELLED"
        }), 200
    except Exception as error:
        db.session.rollback()
        return jsonify({"message": "Unable to cancel queue.", "error": str(error)}), 500


@queue_bp.route("/next", methods=["GET"])
def next_number_help():
    return jsonify({
        "message": "Use POST with a Bearer access token to call the next queue.",
        "example": {
            "method": "POST",
            "headers": {"Authorization": "Bearer YOUR_ACCESS_TOKEN"}
        }
    }), 200


@queue_bp.route("/next", methods=["POST"])
@jwt_required()
def next_number():
    """Assign the oldest waiting ticket to the caller's active-shift window."""
    staff_id = int(get_jwt_identity())

    try:
        active_shift = db.session.execute(
            text("""
                SELECT shift_id, window_id, department_id
                FROM staff_shifts
                WHERE staff_id = :staff_id
                  AND status = 'ACTIVE'
                ORDER BY shift_id DESC
                LIMIT 1
                FOR UPDATE
            """),
            {"staff_id": staff_id}
        ).mappings().first()

        if not active_shift:
            return jsonify({
                "message": "Start an active shift before calling the next customer."
            }), 403

        # Locking the active-shift window serializes NEXT requests for that window.
        window = db.session.execute(
            text("""
                SELECT sw.window_id, sw.department_id, sw.window_number,
                       sw.window_name, sw.status,
                       d.department_name
                FROM service_windows sw
                JOIN departments d ON d.department_id = sw.department_id
                WHERE sw.window_id = :window_id
                  AND sw.department_id = :department_id
                LIMIT 1
                FOR UPDATE
            """),
            {
                "window_id": active_shift["window_id"],
                "department_id": active_shift["department_id"]
            }
        ).mappings().first()

        if not window:
            return jsonify({"message": "Service window not found."}), 404
        if window["status"] != "ACTIVE":
            return jsonify({"message": "Service window is not active."}), 400
        current = db.session.execute(
            text("""
                SELECT queue_id, queue_number
                FROM queue_numbers
                WHERE window_id = :window_id
                  AND queue_date = CURDATE()
                  AND status IN ('CALLED', 'SERVING')
                LIMIT 1
            """),
            {"window_id": window["window_id"]}
        ).mappings().first()

        if current:
            db.session.rollback()
            return jsonify({
                "message": "Complete or mark the current queue as no-show first.",
                "current_queue": current["queue_number"]
            }), 409

        queue = db.session.execute(
            text("""
                SELECT queue_id, queue_number, sequence_number
                FROM queue_numbers
                WHERE department_id = :department_id
                  AND queue_date = CURDATE()
                  AND status = 'WAITING'
                ORDER BY sequence_number ASC
                LIMIT 1
                FOR UPDATE
            """),
            {"department_id": window["department_id"]}
        ).mappings().first()

        if not queue:
            db.session.rollback()
            return jsonify({"message": "No customers are waiting."}), 404

        now = datetime.now()

        db.session.execute(
            text("""
                UPDATE queue_numbers
                SET status = 'CALLED', window_id = :window_id, staff_id = :staff_id,
                    called_at = :called_at, serving_at = NULL, customer_acknowledged_at = NULL
                WHERE queue_id = :queue_id
            """),
            {
                "window_id": window["window_id"],
                "staff_id": staff_id,
                "called_at": now,
                "serving_at": now,
                "queue_id": queue["queue_id"]
            }
        )

        _record_event(queue["queue_id"], "CALLED", staff_id=staff_id, window_id=window["window_id"],
                      notes="Queue called by staff.")
        _record_audit(staff_id, "NEXT_NUMBER", entity_type="QUEUE", entity_id=str(queue["queue_id"]),
                     description=f"Called {queue['queue_number']} at {window['window_name']}.")

        db.session.commit()

        return jsonify({
            "message": "Next queue called.",
            "queue": {
                "queue_id": queue["queue_id"],
                "queue_number": queue["queue_number"],
                "status": "CALLED",
                "department": window["department_name"],
                "window_id": window["window_id"],
                "window_number": window["window_number"],
                "window_name": window["window_name"]
            }
        }), 200

    except Exception as error:
        db.session.rollback()
        return jsonify({"message": "Unable to call next queue.", "error": str(error)}), 500


@queue_bp.route("/<int:queue_id>/complete", methods=["GET"])
def complete_queue_help(queue_id):
    return _staff_action_help(queue_id, "complete")


@queue_bp.route("/<int:queue_id>/complete", methods=["POST"])
@jwt_required()
def complete_queue(queue_id):
    return _finish_active_queue(
        queue_id, "COMPLETED", "COMPLETED",
        "Queue transaction completed.", "Queue completed."
    )


@queue_bp.route("/<int:queue_id>/no-show", methods=["GET"])
def no_show_queue_help(queue_id):
    return _staff_action_help(queue_id, "no-show")


@queue_bp.route("/<int:queue_id>/no-show", methods=["POST"])
@jwt_required()
def no_show_queue(queue_id):
    queue = _get_active_queue(queue_id, lock=True)
    if not queue:
        return jsonify({"message": "Queue not found."}), 404
    grace_period = _get_call_grace_period()
    lock_no_show = _get_queue_rule("lock_no_show_during_grace")
    remaining = _grace_remaining_seconds(queue["status"], queue["called_at"], grace_period)
    if lock_no_show and queue["status"] in ("CALLED", "SERVING") and remaining > 0:
        db.session.rollback()
        return jsonify({
            "message": "Customer grace period is still active.",
            "remaining_seconds": remaining
        }), 409
    db.session.rollback()
    return _finish_active_queue(
        queue_id, "NO_SHOW", "NO_SHOW",
        "Customer marked as no-show.", "Queue marked as no-show."
    )


@queue_bp.route("/<int:queue_id>/recall", methods=["GET"])
def recall_queue_help(queue_id):
    return _staff_action_help(queue_id, "recall")


@queue_bp.route("/<int:queue_id>/recall", methods=["POST"])
@jwt_required()
def recall_queue(queue_id):
    staff_id = int(get_jwt_identity())

    try:
        queue = _get_active_queue(queue_id, lock=True)

        if not queue:
            return jsonify({"message": "Queue not found."}), 404
        if queue["status"] not in ("CALLED", "SERVING"):
            db.session.rollback()
            return jsonify({"message": "Only the current queue can be recalled."}), 409
        if queue["staff_id"] != staff_id:
            db.session.rollback()
            return jsonify({"message": "This queue belongs to another staff member."}), 403

        _record_event(queue_id, "RECALLED", staff_id=staff_id, window_id=queue["window_id"],
                      notes="Queue number recalled.")

        db.session.commit()

        return jsonify({
            "message": "Queue recalled.",
            "queue_number": queue["queue_number"],
            "window_id": queue["window_id"]
        }), 200

    except Exception as error:
        db.session.rollback()
        return jsonify({"message": "Unable to recall queue.", "error": str(error)}), 500


def _finish_active_queue(queue_id, new_status, event_type, notes, success_message):
    staff_id = int(get_jwt_identity())

    try:
        queue = _get_active_queue(queue_id, lock=True)

        if not queue:
            return jsonify({"message": "Queue not found."}), 404
        if queue["status"] not in ("CALLED", "SERVING"):
            db.session.rollback()
            return jsonify({"message": f"Queue cannot be marked as {new_status.lower()}."}), 409
        if queue["staff_id"] != staff_id:
            db.session.rollback()
            return jsonify({"message": "This queue belongs to another staff member."}), 403

        # completed_at only applies to a true COMPLETED outcome; NO_SHOW has no matching timestamp column.
        if new_status == "COMPLETED":
            db.session.execute(
                text("""
                    UPDATE queue_numbers
                    SET status = :status, completed_at = :completed_at
                    WHERE queue_id = :queue_id
                """),
                {"status": new_status, "completed_at": datetime.now(), "queue_id": queue_id}
            )
        else:
            db.session.execute(
                text("UPDATE queue_numbers SET status = :status WHERE queue_id = :queue_id"),
                {"status": new_status, "queue_id": queue_id}
            )

        _record_event(queue_id, event_type, staff_id=staff_id, window_id=queue["window_id"], notes=notes)

        db.session.commit()

        return jsonify({
            "message": success_message,
            "queue_number": queue["queue_number"],
            "status": new_status
        }), 200

    except Exception as error:
        db.session.rollback()
        return jsonify({"message": "Unable to update queue.", "error": str(error)}), 500


def _staff_action_help(queue_id, action):
    return jsonify({
        "message": f"Use POST with a Bearer access token to {action} this queue.",
        "queue_id": queue_id,
        "example": {
            "method": "POST",
            "headers": {"Authorization": "Bearer YOUR_ACCESS_TOKEN"},
            "body": {}
        }
    }), 200


def _get_department(department_id):
    return db.session.execute(
        text("""
            SELECT department_id, department_name, queue_prefix, is_active, queue_is_open
            FROM departments
            WHERE department_id = :department_id
            LIMIT 1
        """),
        {"department_id": department_id}
    ).mappings().first()


def _waiting_count(department_id):
    return db.session.execute(
        text("""
            SELECT COUNT(*)
            FROM queue_numbers
            WHERE department_id = :department_id
              AND queue_date = CURDATE()
              AND status = 'WAITING'
        """),
        {"department_id": department_id}
    ).scalar()


def _serving_queues(department_id, include_called_at=False):
    rows = db.session.execute(
        text("""
            SELECT q.queue_id, q.queue_number, q.status, q.called_at,
                   sw.window_id, sw.window_number, sw.window_name
            FROM queue_numbers q
            JOIN service_windows sw ON sw.window_id = q.window_id
            WHERE q.department_id = :department_id
              AND q.queue_date = CURDATE()
              AND q.status IN ('CALLED', 'SERVING')
            ORDER BY sw.window_number ASC
        """),
        {"department_id": department_id}
    ).mappings().all()
    return [_queue_display_row(row, include_called_at) for row in rows]


def _queue_display_row(row, include_called_at=False):
    result = {
        "queue_id": row["queue_id"],
        "queue_number": row["queue_number"],
        "status": row["status"],
        "window_id": row["window_id"],
        "window_number": row["window_number"],
        "window_name": row["window_name"]
    }
    if include_called_at:
        result["called_at"] = _isoformat(row["called_at"])
    return result


def _get_active_queue(queue_id, lock=False):
    lock_clause = " FOR UPDATE" if lock else ""
    return db.session.execute(
        text(
            "SELECT queue_id, queue_number, window_id, staff_id, status, called_at "
            "FROM queue_numbers WHERE queue_id = :queue_id" + lock_clause
        ),
        {"queue_id": queue_id}
    ).mappings().first()


def _record_event(queue_id, event_type, staff_id=None, window_id=None, notes=None):
    db.session.execute(
        text("""
            INSERT INTO queue_events (queue_id, event_type, staff_id, window_id, notes)
            VALUES (:queue_id, :event_type, :staff_id, :window_id, :notes)
        """),
        {
            "queue_id": queue_id,
            "event_type": event_type,
            "staff_id": staff_id,
            "window_id": window_id,
            "notes": notes
        }
    )


def _record_audit(staff_id, action, entity_type=None, entity_id=None, description=None):
    db.session.execute(
        text("""
            INSERT INTO audit_logs (staff_id, action, entity_type, entity_id, description)
            VALUES (:staff_id, :action, :entity_type, :entity_id, :description)
        """),
        {
            "staff_id": staff_id,
            "action": action,
            "entity_type": entity_type,
            "entity_id": entity_id,
            "description": description
        }
    )


def _isoformat(value):
    return value.isoformat() if value else None
