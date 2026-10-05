from datetime import datetime

from flask import Blueprint, current_app, jsonify, request
from flask_jwt_extended import get_jwt, get_jwt_identity, jwt_required
from sqlalchemy.exc import IntegrityError
from sqlalchemy import text
from werkzeug.security import generate_password_hash

from app import db


admin_bp = Blueprint("admin", __name__, url_prefix="/api/admin")

SYSTEM_SETTING_DEFAULTS = {
    "queue_digits": 3,
    "call_grace_period_seconds": 60,
    "enable_call_countdown": True,
    "lock_no_show_during_grace": True,
    "allow_customer_acknowledgement": True,
    "qr_expiration_hours": 24,
    "tv_voice_enabled": True,
    "mobile_call_alert": True,
    "mobile_recall_alert": True,
    "mobile_voice_announcement": True,
    "require_join_confirmation": True,
    "allow_mobile_cancellation": True,
    "show_people_ahead": True,
    "show_estimated_wait_time": False,
    "keep_alert_until_acknowledged": False,
    "mobile_alert_duration_seconds": 15,
    "show_connection_warning": True,
    "automatic_reconnection": True,
    "offline_retry_interval_seconds": 5,
    "refresh_interval": 3,
    "mobile_queue_enabled": True,
    "kiosk_queue_enabled": True
}

SYSTEM_SETTING_DESCRIPTIONS = {
    "queue_digits": "Number of digits used to pad queue numbers",
    "call_grace_period_seconds": "Seconds a called customer has before the grace period expires",
    "enable_call_countdown": "Show the call grace-period countdown to customers",
    "lock_no_show_during_grace": "Prevent staff from marking a called customer as no-show during the grace period",
    "allow_customer_acknowledgement": "Allow customers to acknowledge that they are on the way",
    "qr_expiration_hours": "QR session lifetime in hours",
    "tv_voice_enabled": "Enable voice announcements on the TV display",
    "mobile_call_alert": "Enable mobile call alerts",
    "mobile_recall_alert": "Enable mobile recall alerts",
    "mobile_voice_announcement": "Speak the queue call in the mobile app",
    "require_join_confirmation": "Require the customer to confirm before joining a queue",
    "allow_mobile_cancellation": "Allow customers to cancel while waiting",
    "show_people_ahead": "Show waiting customers ahead in the mobile app",
    "show_estimated_wait_time": "Show estimated wait time in the mobile app",
    "keep_alert_until_acknowledged": "Keep mobile alerts active until acknowledged",
    "mobile_alert_duration_seconds": "Mobile alert duration in seconds",
    "show_connection_warning": "Show mobile connection warnings",
    "automatic_reconnection": "Retry the mobile connection automatically",
    "offline_retry_interval_seconds": "Seconds between offline retry attempts",
    "refresh_interval": "TV display refresh interval in seconds",
    "mobile_queue_enabled": "Allow queue entry from the mobile app",
    "kiosk_queue_enabled": "Allow queue entry from the kiosk"
}


def _admin_id_or_response():
    if get_jwt().get("role") != "ADMIN":
        return None, (jsonify({"message": "Administrator access is required."}), 403)

    try:
        admin_id = int(get_jwt_identity())
    except (TypeError, ValueError):
        return None, (jsonify({"message": "Invalid administrator session."}), 401)

    admin = db.session.execute(
        text("""
            SELECT staff_id
            FROM staff_users
            WHERE staff_id = :staff_id
              AND role = 'ADMIN'
              AND account_status = 'ACTIVE'
            LIMIT 1
        """),
        {"staff_id": admin_id}
    ).first()
    if not admin:
        return None, (jsonify({"message": "Administrator account is not active."}), 403)

    return admin_id, None


def _department_values(data, include_window_count=True):
    name = data.get("department_name")
    prefix = data.get("queue_prefix")
    window_count = data.get("window_count")

    if not isinstance(name, str) or not name.strip() or len(name.strip()) > 100:
        return None, "Department name is required and must be at most 100 characters."
    if not isinstance(prefix, str) or not prefix.strip() or len(prefix.strip()) > 10:
        return None, "Queue prefix is required and must be at most 10 characters."
    if include_window_count and (
        isinstance(window_count, bool)
        or not isinstance(window_count, int)
        or not 1 <= window_count <= 50
    ):
        return None, "Window count must be an integer from 1 to 50."

    is_active = data.get("is_active", True)
    if not isinstance(is_active, bool):
        return None, "is_active must be true or false."

    return {
        "department_name": name.strip(),
        "queue_prefix": prefix.strip().upper(),
        "window_count": window_count,
        "is_active": is_active
    }, None


def _staff_account_values(data, include_password=False):
    full_name = data.get("full_name")
    username = data.get("username")
    department_id = data.get("department_id")
    account_status = data.get("account_status", "ACTIVE")
    password = data.get("password")

    if not isinstance(full_name, str) or not full_name.strip() or len(full_name.strip()) > 150:
        return None, "Full name is required and must be at most 150 characters."
    if not isinstance(username, str) or not username.strip() or len(username.strip()) > 100:
        return None, "Username is required and must be at most 100 characters."
    if isinstance(department_id, bool) or not isinstance(department_id, int) or department_id <= 0:
        return None, "A valid department_id is required."
    if account_status not in ("ACTIVE", "INACTIVE"):
        return None, "account_status must be ACTIVE or INACTIVE."
    if include_password and (not isinstance(password, str) or len(password) < 8):
        return None, "Password must be at least 8 characters."

    return {
        "full_name": full_name.strip(),
        "username": username.strip(),
        "department_id": department_id,
        "account_status": account_status,
        "password": password
    }, None


@admin_bp.route("/staff", methods=["GET"])
@jwt_required()
def get_staff_accounts():
    _, error = _admin_id_or_response()
    if error:
        return error

    department_id = request.args.get("department_id", type=int)
    if request.args.get("department_id") and department_id is None:
        return jsonify({"message": "department_id must be an integer."}), 400

    account_status = request.args.get("status")
    if account_status and account_status not in ("ACTIVE", "INACTIVE", "SUSPENDED"):
        return jsonify({"message": "status must be ACTIVE, INACTIVE, or SUSPENDED."}), 400

    search = request.args.get("search", "").strip()
    filters = ["su.role = 'STAFF'"]
    params = {}
    if department_id is not None:
        filters.append("su.department_id = :department_id")
        params["department_id"] = department_id
    if account_status:
        filters.append("su.account_status = :account_status")
        params["account_status"] = account_status
    if search:
        filters.append("(su.full_name LIKE :search OR su.username LIKE :search)")
        params["search"] = f"%{search}%"

    rows = db.session.execute(
        text(f"""
            SELECT su.staff_id, su.full_name, su.username, su.department_id,
                   d.department_name, su.account_status
            FROM staff_users su
            LEFT JOIN departments d ON d.department_id = su.department_id
            WHERE {' AND '.join(filters)}
            ORDER BY su.full_name, su.username
        """),
        params
    ).mappings().all()

    return jsonify({
        "staff": [
            {
                "staff_id": row["staff_id"],
                "full_name": row["full_name"],
                "username": row["username"],
                "department_id": row["department_id"],
                "department_name": row["department_name"] or "Unassigned",
                "account_status": row["account_status"]
            }
            for row in rows
        ]
    }), 200


@admin_bp.route("/staff", methods=["POST"])
@jwt_required()
def create_staff_account():
    _, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        return jsonify({"message": "A JSON object is required."}), 400
    values, error_message = _staff_account_values(data, include_password=True)
    if error_message:
        return jsonify({"message": error_message}), 400

    try:
        department = db.session.execute(
            text("SELECT department_id FROM departments WHERE department_id = :department_id AND is_active = 1"),
            {"department_id": values["department_id"]}
        ).first()
        if not department:
            return jsonify({"message": "Select an active department."}), 400

        result = db.session.execute(
            text("""
                INSERT INTO staff_users (full_name, username, password_hash, role, department_id, account_status)
                VALUES (:full_name, :username, :password_hash, 'STAFF', :department_id, :account_status)
            """),
            {
                "full_name": values["full_name"],
                "username": values["username"],
                "password_hash": generate_password_hash(values["password"]),
                "department_id": values["department_id"],
                "account_status": values["account_status"]
            }
        )
        db.session.commit()
        return jsonify({"message": "Staff account created.", "staff_id": result.lastrowid}), 201
    except IntegrityError:
        db.session.rollback()
        return jsonify({"message": "That username is already in use."}), 409
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to create staff account."}), 500


@admin_bp.route("/staff/<int:staff_id>", methods=["PUT"])
@jwt_required()
def update_staff_account(staff_id):
    _, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        return jsonify({"message": "A JSON object is required."}), 400
    values, error_message = _staff_account_values(data)
    if error_message:
        return jsonify({"message": error_message}), 400

    try:
        staff = db.session.execute(
            text("SELECT staff_id, department_id FROM staff_users WHERE staff_id = :staff_id AND role = 'STAFF' FOR UPDATE"),
            {"staff_id": staff_id}
        ).mappings().first()
        if not staff:
            return jsonify({"message": "Staff account not found."}), 404

        department = db.session.execute(
            text("SELECT department_id FROM departments WHERE department_id = :department_id AND is_active = 1"),
            {"department_id": values["department_id"]}
        ).first()
        if not department:
            return jsonify({"message": "Select an active department."}), 400

        if staff["department_id"] != values["department_id"]:
            active_shift = db.session.execute(
                text("SELECT shift_id FROM staff_shifts WHERE staff_id = :staff_id AND status = 'ACTIVE' LIMIT 1"),
                {"staff_id": staff_id}
            ).first()
            if active_shift:
                db.session.rollback()
                return jsonify({"message": "End the staff member's active shift before changing departments."}), 409

        db.session.execute(
            text("""
                UPDATE staff_users
                SET full_name = :full_name, username = :username, department_id = :department_id
                WHERE staff_id = :staff_id AND role = 'STAFF'
            """),
            {
                "staff_id": staff_id,
                "full_name": values["full_name"],
                "username": values["username"],
                "department_id": values["department_id"]
            }
        )
        db.session.commit()
        return jsonify({"message": "Staff account updated."}), 200
    except IntegrityError:
        db.session.rollback()
        return jsonify({"message": "That username is already in use."}), 409
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to update staff account."}), 500


@admin_bp.route("/staff/<int:staff_id>/status", methods=["PATCH"])
@jwt_required()
def update_staff_account_status(staff_id):
    _, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict) or data.get("account_status") not in ("ACTIVE", "INACTIVE"):
        return jsonify({"message": "account_status must be ACTIVE or INACTIVE."}), 400

    account_status = data["account_status"]
    try:
        staff = db.session.execute(
            text("SELECT staff_id FROM staff_users WHERE staff_id = :staff_id AND role = 'STAFF' FOR UPDATE"),
            {"staff_id": staff_id}
        ).first()
        if not staff:
            return jsonify({"message": "Staff account not found."}), 404

        if account_status == "INACTIVE":
            active_shift = db.session.execute(
                text("SELECT shift_id FROM staff_shifts WHERE staff_id = :staff_id AND status = 'ACTIVE' LIMIT 1"),
                {"staff_id": staff_id}
            ).first()
            if active_shift:
                db.session.rollback()
                return jsonify({"message": "End the staff member's active shift before deactivating this account."}), 409

        db.session.execute(
            text("UPDATE staff_users SET account_status = :account_status WHERE staff_id = :staff_id AND role = 'STAFF'"),
            {"staff_id": staff_id, "account_status": account_status}
        )
        db.session.commit()
        return jsonify({"message": "Staff account status updated."}), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to update staff account status."}), 500


@admin_bp.route("/staff/<int:staff_id>/reset-password", methods=["POST"])
@jwt_required()
def reset_staff_password(staff_id):
    _, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    password = data.get("password") if isinstance(data, dict) else None
    if not isinstance(password, str) or len(password) < 8:
        return jsonify({"message": "Password must be at least 8 characters."}), 400

    try:
        staff = db.session.execute(
            text("SELECT staff_id FROM staff_users WHERE staff_id = :staff_id AND role = 'STAFF'"),
            {"staff_id": staff_id}
        ).first()
        if not staff:
            return jsonify({"message": "Staff account not found."}), 404

        db.session.execute(
            text("UPDATE staff_users SET password_hash = :password_hash WHERE staff_id = :staff_id"),
            {"staff_id": staff_id, "password_hash": generate_password_hash(password)}
        )
        db.session.commit()
        return jsonify({"message": "Staff password reset."}), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to reset staff password."}), 500


@admin_bp.route("/departments", methods=["GET"])
@jwt_required()
def get_departments():
    _, error = _admin_id_or_response()
    if error:
        return error

    departments = db.session.execute(
        text("""
            SELECT d.department_id, d.department_name, d.queue_prefix,
                   d.is_active, COUNT(CASE WHEN sw.status = 'ACTIVE' THEN 1 END) AS window_count
            FROM departments d
            LEFT JOIN service_windows sw ON sw.department_id = d.department_id
            GROUP BY d.department_id, d.department_name, d.queue_prefix, d.is_active
            ORDER BY d.department_name
        """)
    ).mappings().all()

    return jsonify({
        "departments": [
            {
                "department_id": row["department_id"],
                "department_name": row["department_name"],
                "queue_prefix": row["queue_prefix"],
                "window_count": int(row["window_count"]),
                "is_active": bool(row["is_active"])
            }
            for row in departments
        ]
    }), 200


@admin_bp.route("/departments", methods=["POST"])
@jwt_required()
def create_department():
    _, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        return jsonify({"message": "A JSON object is required."}), 400

    values, error_message = _department_values(data)
    if error_message:
        return jsonify({"message": error_message}), 400

    try:
        result = db.session.execute(
            text("""
                INSERT INTO departments (department_name, queue_prefix, is_active)
                VALUES (:department_name, :queue_prefix, :is_active)
            """),
            {
                "department_name": values["department_name"],
                "queue_prefix": values["queue_prefix"],
                "is_active": values["is_active"]
            }
        )
        department_id = result.lastrowid

        for window_number in range(1, values["window_count"] + 1):
            db.session.execute(
                text("""
                    INSERT INTO service_windows (department_id, window_number, window_name, status)
                    VALUES (:department_id, :window_number, :window_name, 'ACTIVE')
                """),
                {
                    "department_id": department_id,
                    "window_number": window_number,
                    "window_name": f"{values['department_name']} Window {window_number}"
                }
            )

        db.session.commit()
        return jsonify({
            "message": "Department created.",
            "department_id": department_id
        }), 201
    except IntegrityError:
        db.session.rollback()
        return jsonify({"message": "Department name or queue prefix already exists."}), 409
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to create department."}), 500


@admin_bp.route("/departments/<int:department_id>", methods=["PUT"])
@jwt_required()
def update_department(department_id):
    _, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        return jsonify({"message": "A JSON object is required."}), 400

    values, error_message = _department_values(data)
    if error_message:
        return jsonify({"message": error_message}), 400

    try:
        department = db.session.execute(
            text("""
                SELECT department_id, department_name, is_active
                FROM departments
                WHERE department_id = :department_id
                FOR UPDATE
            """),
            {"department_id": department_id}
        ).mappings().first()
        if not department:
            return jsonify({"message": "Department not found."}), 404

        if department["is_active"] and not values["is_active"]:
            active_shift = db.session.execute(
                text("""
                    SELECT shift_id
                    FROM staff_shifts
                    WHERE department_id = :department_id AND status = 'ACTIVE'
                    LIMIT 1
                """),
                {"department_id": department_id}
            ).first()
            if active_shift:
                db.session.rollback()
                return jsonify({"message": "End active staff shifts before deactivating this department."}), 409

        db.session.execute(
            text("""
                UPDATE departments
                SET department_name = :department_name,
                    queue_prefix = :queue_prefix,
                    is_active = :is_active
                WHERE department_id = :department_id
            """),
            {
                "department_id": department_id,
                "department_name": values["department_name"],
                "queue_prefix": values["queue_prefix"],
                "is_active": values["is_active"]
            }
        )

        windows = db.session.execute(
            text("""
                SELECT sw.window_id, sw.window_number, sw.window_name, sw.status,
                       EXISTS (
                           SELECT 1 FROM staff_shifts ss
                           WHERE ss.window_id = sw.window_id AND ss.status = 'ACTIVE'
                       ) AS has_active_shift
                FROM service_windows sw
                WHERE sw.department_id = :department_id
                ORDER BY sw.window_number
                FOR UPDATE
            """),
            {"department_id": department_id}
        ).mappings().all()

        for window in windows:
            old_default_name = f"{department['department_name']} Window {window['window_number']}"
            if window["window_name"] == old_default_name:
                db.session.execute(
                    text("UPDATE service_windows SET window_name = :window_name WHERE window_id = :window_id"),
                    {
                        "window_id": window["window_id"],
                        "window_name": f"{values['department_name']} Window {window['window_number']}"
                    }
                )

        active_windows = [window for window in windows if window["status"] == "ACTIVE"]
        requested_count = values["window_count"]

        if len(active_windows) > requested_count:
            to_close = active_windows[requested_count:]
            if any(window["has_active_shift"] for window in to_close):
                db.session.rollback()
                return jsonify({"message": "End active staff shifts before reducing the window count."}), 409

            for window in to_close:
                db.session.execute(
                    text("UPDATE service_windows SET status = 'CLOSED' WHERE window_id = :window_id"),
                    {"window_id": window["window_id"]}
                )
        elif len(active_windows) < requested_count:
            windows_to_activate = [window for window in windows if window["status"] == "CLOSED"]
            for window in windows_to_activate[:requested_count - len(active_windows)]:
                db.session.execute(
                    text("UPDATE service_windows SET status = 'ACTIVE' WHERE window_id = :window_id"),
                    {"window_id": window["window_id"]}
                )

            active_count = len(active_windows) + min(
                len(windows_to_activate), requested_count - len(active_windows)
            )
            next_number = max((window["window_number"] for window in windows), default=0) + 1
            while active_count < requested_count:
                db.session.execute(
                    text("""
                        INSERT INTO service_windows (department_id, window_number, window_name, status)
                        VALUES (:department_id, :window_number, :window_name, 'ACTIVE')
                    """),
                    {
                        "department_id": department_id,
                        "window_number": next_number,
                        "window_name": f"{values['department_name']} Window {next_number}"
                    }
                )
                next_number += 1
                active_count += 1

        db.session.commit()
        return jsonify({"message": "Department updated."}), 200
    except IntegrityError:
        db.session.rollback()
        return jsonify({"message": "Department name or queue prefix already exists."}), 409
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to update department."}), 500


@admin_bp.route("/departments/<int:department_id>/status", methods=["PATCH"])
@jwt_required()
def update_department_status(department_id):
    _, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        return jsonify({"message": "A JSON object is required."}), 400
    is_active = data.get("is_active")
    if not isinstance(is_active, bool):
        return jsonify({"message": "is_active must be true or false."}), 400

    try:
        department = db.session.execute(
            text("SELECT department_id FROM departments WHERE department_id = :department_id FOR UPDATE"),
            {"department_id": department_id}
        ).first()
        if not department:
            return jsonify({"message": "Department not found."}), 404

        if not is_active:
            active_shift = db.session.execute(
                text("""
                    SELECT ss.shift_id
                    FROM staff_shifts ss
                    WHERE ss.department_id = :department_id AND ss.status = 'ACTIVE'
                    LIMIT 1
                """),
                {"department_id": department_id}
            ).first()
            if active_shift:
                db.session.rollback()
                return jsonify({"message": "End active staff shifts before deactivating this department."}), 409

        db.session.execute(
            text("UPDATE departments SET is_active = :is_active WHERE department_id = :department_id"),
            {"department_id": department_id, "is_active": is_active}
        )
        db.session.commit()
        return jsonify({"message": "Department status updated."}), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to update department status."}), 500


@admin_bp.route("/windows", methods=["GET"])
@jwt_required()
def get_service_windows():
    _, error = _admin_id_or_response()
    if error:
        return error

    department_id = request.args.get("department_id", type=int)
    if request.args.get("department_id") and department_id is None:
        return jsonify({"message": "department_id must be an integer."}), 400

    params = {}
    department_filter = ""
    if department_id is not None:
        department_filter = "WHERE d.department_id = :department_id"
        params["department_id"] = department_id

    rows = db.session.execute(
        text(f"""
            SELECT
                sw.window_id,
                sw.department_id,
                d.department_name,
                sw.window_number,
                sw.window_name,
                sw.status,
                EXISTS (
                    SELECT 1 FROM staff_shifts ss
                    WHERE ss.window_id = sw.window_id AND ss.status = 'ACTIVE'
                ) AS is_in_use,
                (
                    SELECT su.full_name
                    FROM staff_shifts ss
                    JOIN staff_users su ON su.staff_id = ss.staff_id
                    WHERE ss.window_id = sw.window_id AND ss.status = 'ACTIVE'
                    ORDER BY ss.shift_id DESC
                    LIMIT 1
                ) AS current_staff_name
            FROM service_windows sw
            JOIN departments d ON d.department_id = sw.department_id
            {department_filter}
            ORDER BY d.department_name, sw.window_number
        """),
        params
    ).mappings().all()

    return jsonify({
        "windows": [
            {
                "window_id": row["window_id"],
                "department_id": row["department_id"],
                "department_name": row["department_name"],
                "window_number": row["window_number"],
                "window_name": row["window_name"],
                "is_active": row["status"] == "ACTIVE",
                "is_in_use": bool(row["is_in_use"]),
                "current_staff_name": row["current_staff_name"]
            }
            for row in rows
        ]
    }), 200


@admin_bp.route("/windows", methods=["POST"])
@jwt_required()
def create_service_window():
    _, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        return jsonify({"message": "A JSON object is required."}), 400

    department_id = data.get("department_id")
    if isinstance(department_id, bool) or not isinstance(department_id, int) or department_id <= 0:
        return jsonify({"message": "A valid department_id is required."}), 400

    window_name = data.get("window_name")
    if window_name is not None and (
        not isinstance(window_name, str) or not window_name.strip() or len(window_name.strip()) > 100
    ):
        return jsonify({"message": "Window name must be between 1 and 100 characters."}), 400

    try:
        department = db.session.execute(
            text("""
                SELECT department_id, department_name
                FROM departments
                WHERE department_id = :department_id AND is_active = 1
                FOR UPDATE
            """),
            {"department_id": department_id}
        ).mappings().first()
        if not department:
            return jsonify({"message": "Active department not found."}), 404

        window_number = db.session.execute(
            text("""
                SELECT COALESCE(MAX(window_number), 0) + 1
                FROM service_windows
                WHERE department_id = :department_id
            """),
            {"department_id": department_id}
        ).scalar_one()
        name = window_name.strip() if window_name else f"{department['department_name']} Window {window_number}"

        result = db.session.execute(
            text("""
                INSERT INTO service_windows (department_id, window_number, window_name, status)
                VALUES (:department_id, :window_number, :window_name, 'ACTIVE')
            """),
            {
                "department_id": department_id,
                "window_number": window_number,
                "window_name": name
            }
        )
        db.session.commit()
        return jsonify({
            "message": "Service window created.",
            "window_id": result.lastrowid
        }), 201
    except IntegrityError:
        db.session.rollback()
        return jsonify({"message": "Unable to create a unique service window."}), 409
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to create service window."}), 500


@admin_bp.route("/windows/<int:window_id>", methods=["PUT"])
@jwt_required()
def update_service_window(window_id):
    _, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        return jsonify({"message": "A JSON object is required."}), 400

    window_name = data.get("window_name")
    if not isinstance(window_name, str) or not window_name.strip() or len(window_name.strip()) > 100:
        return jsonify({"message": "Window name is required and must be at most 100 characters."}), 400

    try:
        window = db.session.execute(
            text("SELECT window_id FROM service_windows WHERE window_id = :window_id FOR UPDATE"),
            {"window_id": window_id}
        ).first()
        if not window:
            return jsonify({"message": "Service window not found."}), 404

        db.session.execute(
            text("UPDATE service_windows SET window_name = :window_name WHERE window_id = :window_id"),
            {"window_id": window_id, "window_name": window_name.strip()}
        )
        db.session.commit()
        return jsonify({"message": "Service window updated."}), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to update service window."}), 500


@admin_bp.route("/windows/<int:window_id>/status", methods=["PATCH"])
@jwt_required()
def update_service_window_status(window_id):
    _, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict) or not isinstance(data.get("is_active"), bool):
        return jsonify({"message": "is_active must be true or false."}), 400

    is_active = data["is_active"]
    try:
        window = db.session.execute(
            text("SELECT window_id FROM service_windows WHERE window_id = :window_id FOR UPDATE"),
            {"window_id": window_id}
        ).first()
        if not window:
            return jsonify({"message": "Service window not found."}), 404

        if not is_active:
            active_shift = db.session.execute(
                text("""
                    SELECT shift_id FROM staff_shifts
                    WHERE window_id = :window_id AND status = 'ACTIVE'
                    LIMIT 1
                """),
                {"window_id": window_id}
            ).first()
            if active_shift:
                db.session.rollback()
                return jsonify({"message": "End the active staff shift before deactivating this window."}), 409

        db.session.execute(
            text("UPDATE service_windows SET status = :status WHERE window_id = :window_id"),
            {"window_id": window_id, "status": "ACTIVE" if is_active else "CLOSED"}
        )
        db.session.commit()
        return jsonify({"message": "Service window status updated."}), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to update service window status."}), 500


@admin_bp.route("/dashboard", methods=["GET"])
@jwt_required()
def dashboard():
    if get_jwt().get("role") != "ADMIN":
        return jsonify({"message": "Administrator access is required."}), 403

    try:
        staff_id = int(get_jwt_identity())
    except (TypeError, ValueError):
        return jsonify({"message": "Invalid administrator session."}), 401

    try:
        admin = db.session.execute(
            text("""
                SELECT staff_id
                FROM staff_users
                WHERE staff_id = :staff_id
                  AND role = 'ADMIN'
                  AND account_status = 'ACTIVE'
                LIMIT 1
            """),
            {"staff_id": staff_id}
        ).mappings().first()

        if not admin:
            return jsonify({"message": "Administrator account is not active."}), 403

        stats = db.session.execute(
            text("""
                SELECT
                    (SELECT COUNT(*)
                     FROM queue_numbers
                     WHERE queue_date = CURDATE() AND status = 'WAITING') AS waiting_today,
                    (SELECT COUNT(*)
                     FROM queue_numbers
                     WHERE queue_date = CURDATE() AND status = 'SERVING') AS currently_serving,
                    (SELECT COUNT(*)
                     FROM service_windows
                     WHERE status = 'ACTIVE') AS active_windows,
                    (SELECT COUNT(*)
                     FROM departments
                     WHERE is_active = 1) AS departments
            """)
        ).mappings().one()

        departments = db.session.execute(
            text("""
                SELECT
                    d.department_id,
                    d.department_name,
                    (SELECT COUNT(*)
                     FROM service_windows sw
                     WHERE sw.department_id = d.department_id
                       AND sw.status = 'ACTIVE') AS active_windows,
                    (SELECT COUNT(*)
                     FROM queue_numbers q
                     WHERE q.department_id = d.department_id
                       AND q.queue_date = CURDATE()
                       AND q.status = 'WAITING') AS waiting_count
                FROM departments d
                WHERE d.is_active = 1
                ORDER BY d.department_name
            """)
        ).mappings().all()

        return jsonify({
            "stats": {
                "waiting_today": int(stats["waiting_today"]),
                "currently_serving": int(stats["currently_serving"]),
                "active_windows": int(stats["active_windows"]),
                "departments": int(stats["departments"])
            },
            "departments": [
                {
                    "department_id": row["department_id"],
                    "department_name": row["department_name"],
                    "active_windows": int(row["active_windows"]),
                    "waiting_count": int(row["waiting_count"])
                }
                for row in departments
            ]
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to load the admin dashboard."}), 500


@admin_bp.route("/live-queue", methods=["GET"])
@jwt_required()
def live_queue():
    _, error = _admin_id_or_response()
    if error:
        return error

    raw_department_id = request.args.get("department_id")
    department_id = request.args.get("department_id", type=int)
    if raw_department_id and department_id is None:
        return jsonify({"message": "department_id must be an integer."}), 400

    filters = ["d.is_active = 1"]
    params = {}
    if department_id is not None:
        filters.append("d.department_id = :department_id")
        params["department_id"] = department_id
    department_filter = " AND ".join(filters)

    try:
        summary = db.session.execute(
            text(f"""
                SELECT
                    (SELECT COUNT(*)
                     FROM queue_numbers q
                     JOIN departments d ON d.department_id = q.department_id
                     WHERE q.queue_date = CURDATE()
                       AND q.status = 'WAITING'
                       AND {department_filter}) AS waiting,
                    (SELECT COUNT(*)
                     FROM queue_numbers q
                     JOIN departments d ON d.department_id = q.department_id
                     WHERE q.queue_date = CURDATE()
                       AND q.status = 'SERVING'
                       AND {department_filter}) AS serving,
                    (SELECT COUNT(DISTINCT ss.window_id)
                     FROM staff_shifts ss
                     JOIN departments d ON d.department_id = ss.department_id
                     WHERE ss.status = 'ACTIVE'
                       AND {department_filter}) AS windows_in_use
            """),
            params
        ).mappings().one()

        serving_rows = db.session.execute(
            text(f"""
                SELECT q.queue_id, q.queue_number, d.department_name,
                       sw.window_id, sw.window_number, sw.window_name,
                       su.full_name AS staff_name, q.status, q.serving_at
                FROM queue_numbers q
                JOIN departments d ON d.department_id = q.department_id
                LEFT JOIN service_windows sw ON sw.window_id = q.window_id
                LEFT JOIN staff_users su ON su.staff_id = q.staff_id
                WHERE q.queue_date = CURDATE()
                  AND q.status = 'SERVING'
                  AND {department_filter}
                ORDER BY d.department_name, sw.window_number, q.serving_at
            """),
            params
        ).mappings().all()

        waiting_rows = db.session.execute(
            text(f"""
                SELECT q.queue_id, q.queue_number, d.department_name,
                       q.source, q.created_at
                FROM queue_numbers q
                JOIN departments d ON d.department_id = q.department_id
                WHERE q.queue_date = CURDATE()
                  AND q.status = 'WAITING'
                  AND {department_filter}
                ORDER BY q.sequence_number, q.created_at
            """),
            params
        ).mappings().all()

        return jsonify({
            "summary": {
                "waiting": int(summary["waiting"]),
                "serving": int(summary["serving"]),
                "windows_in_use": int(summary["windows_in_use"])
            },
            "serving": [
                {
                    "queue_id": row["queue_id"],
                    "queue_number": row["queue_number"],
                    "department_name": row["department_name"],
                    "window_id": row["window_id"],
                    "window_number": row["window_number"],
                    "window_name": row["window_name"],
                    "staff_name": row["staff_name"],
                    "status": row["status"],
                    "serving_at": row["serving_at"].isoformat() if row["serving_at"] else None
                }
                for row in serving_rows
            ],
            "waiting": [
                {
                    "queue_id": row["queue_id"],
                    "queue_number": row["queue_number"],
                    "department_name": row["department_name"],
                    "source": row["source"],
                    "created_at": row["created_at"].isoformat() if row["created_at"] else None
                }
                for row in waiting_rows
            ]
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to load the live queue."}), 500


def _queue_history_filters():
    raw_department_id = request.args.get("department_id")
    department_id = request.args.get("department_id", type=int)
    if raw_department_id and department_id is None:
        return None, "department_id must be an integer."

    status = request.args.get("status", "").strip().upper()
    allowed_statuses = {"WAITING", "CALLED", "SERVING", "COMPLETED", "NO_SHOW", "CANCELLED"}
    if status and status not in allowed_statuses:
        return None, "Invalid queue status filter."

    source = request.args.get("source", "").strip().upper()
    if source and source not in {"KIOSK", "MOBILE"}:
        return None, "source must be KIOSK or MOBILE."

    date_from = request.args.get("date_from", "").strip()
    date_to = request.args.get("date_to", "").strip()
    try:
        date_from = datetime.strptime(date_from, "%Y-%m-%d").date() if date_from else None
        date_to = datetime.strptime(date_to, "%Y-%m-%d").date() if date_to else None
    except ValueError:
        return None, "Dates must use YYYY-MM-DD format."
    if date_from and date_to and date_from > date_to:
        return None, "date_from must be on or before date_to."

    filters = ["1 = 1"]
    params = {}
    if department_id is not None:
        filters.append("q.department_id = :department_id")
        params["department_id"] = department_id
    if status:
        filters.append("q.status = :status")
        params["status"] = status
    if source:
        filters.append("q.source = :source")
        params["source"] = "TICKET" if source == "KIOSK" else "MOBILE"
    if date_from:
        filters.append("q.queue_date >= :date_from")
        params["date_from"] = date_from
    if date_to:
        filters.append("q.queue_date <= :date_to")
        params["date_to"] = date_to

    return {"where": " AND ".join(filters), "params": params}, None


@admin_bp.route("/queue-history", methods=["GET"])
@jwt_required()
def get_admin_queue_history():
    _, error = _admin_id_or_response()
    if error:
        return error

    filters, error_message = _queue_history_filters()
    if error_message:
        return jsonify({"message": error_message}), 400

    try:
        rows = db.session.execute(
            text(f"""
                SELECT q.queue_id, q.queue_number, q.queue_date, q.status, q.source,
                       q.created_at, q.called_at, q.serving_at, q.completed_at,
                       d.department_id, d.department_name,
                       sw.window_id, sw.window_number, sw.window_name,
                       su.staff_id, su.full_name AS staff_name
                FROM queue_numbers q
                JOIN departments d ON d.department_id = q.department_id
                LEFT JOIN service_windows sw ON sw.window_id = q.window_id
                LEFT JOIN staff_users su ON su.staff_id = q.staff_id
                WHERE {filters['where']}
                ORDER BY q.created_at DESC, q.queue_id DESC
                LIMIT 1000
            """),
            filters["params"]
        ).mappings().all()

        return jsonify({
            "history": [
                {
                    "queue_id": row["queue_id"],
                    "queue_number": row["queue_number"],
                    "queue_date": row["queue_date"].isoformat(),
                    "department_id": row["department_id"],
                    "department_name": row["department_name"],
                    "window_id": row["window_id"],
                    "window_number": row["window_number"],
                    "window_name": row["window_name"],
                    "staff_id": row["staff_id"],
                    "staff_name": row["staff_name"],
                    "source": "KIOSK" if row["source"] == "TICKET" else row["source"],
                    "status": row["status"],
                    "created_at": row["created_at"].isoformat() if row["created_at"] else None,
                    "called_at": row["called_at"].isoformat() if row["called_at"] else None,
                    "serving_at": row["serving_at"].isoformat() if row["serving_at"] else None,
                    "completed_at": row["completed_at"].isoformat() if row["completed_at"] else None
                }
                for row in rows
            ]
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to load queue history."}), 500


@admin_bp.route("/queue-history/<int:queue_id>", methods=["GET"])
@jwt_required()
def get_admin_queue_history_detail(queue_id):
    _, error = _admin_id_or_response()
    if error:
        return error

    try:
        row = db.session.execute(
            text("""
                SELECT q.queue_id, q.queue_number, q.queue_date, q.status, q.source,
                       q.created_at, q.called_at, q.serving_at, q.completed_at,
                       d.department_id, d.department_name,
                       sw.window_id, sw.window_number, sw.window_name,
                       su.staff_id, su.full_name AS staff_name,
                       (SELECT MAX(e.created_at)
                        FROM queue_events e
                        WHERE e.queue_id = q.queue_id
                          AND e.event_type IN ('COMPLETED', 'NO_SHOW', 'CANCELLED')) AS terminal_at
                FROM queue_numbers q
                JOIN departments d ON d.department_id = q.department_id
                LEFT JOIN service_windows sw ON sw.window_id = q.window_id
                LEFT JOIN staff_users su ON su.staff_id = q.staff_id
                WHERE q.queue_id = :queue_id
                LIMIT 1
            """),
            {"queue_id": queue_id}
        ).mappings().first()
        if not row:
            return jsonify({"message": "Queue transaction not found."}), 404

        events = db.session.execute(
            text("""
                SELECT e.event_id, e.event_type, e.created_at, e.notes,
                       su.full_name AS staff_name, sw.window_name
                FROM queue_events e
                LEFT JOIN staff_users su ON su.staff_id = e.staff_id
                LEFT JOIN service_windows sw ON sw.window_id = e.window_id
                WHERE e.queue_id = :queue_id
                ORDER BY e.created_at, e.event_id
            """),
            {"queue_id": queue_id}
        ).mappings().all()

        waiting_seconds = (
            int((row["called_at"] - row["created_at"]).total_seconds())
            if row["called_at"] and row["created_at"] else None
        )
        service_end = row["completed_at"] or row["terminal_at"]
        service_seconds = (
            int((service_end - row["serving_at"]).total_seconds())
            if service_end and row["serving_at"] else None
        )

        return jsonify({
            "transaction": {
                "queue_id": row["queue_id"],
                "queue_number": row["queue_number"],
                "queue_date": row["queue_date"].isoformat(),
                "department_id": row["department_id"],
                "department_name": row["department_name"],
                "window_id": row["window_id"],
                "window_number": row["window_number"],
                "window_name": row["window_name"],
                "staff_id": row["staff_id"],
                "staff_name": row["staff_name"],
                "source": "KIOSK" if row["source"] == "TICKET" else row["source"],
                "status": row["status"],
                "created_at": row["created_at"].isoformat() if row["created_at"] else None,
                "called_at": row["called_at"].isoformat() if row["called_at"] else None,
                "serving_at": row["serving_at"].isoformat() if row["serving_at"] else None,
                "completed_at": row["completed_at"].isoformat() if row["completed_at"] else None,
                "waiting_seconds": waiting_seconds,
                "service_seconds": service_seconds
            },
            "events": [
                {
                    "event_id": event["event_id"],
                    "event_type": event["event_type"],
                    "created_at": event["created_at"].isoformat() if event["created_at"] else None,
                    "staff_name": event["staff_name"],
                    "window_name": event["window_name"],
                    "notes": event["notes"]
                }
                for event in events
            ]
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to load queue transaction details."}), 500


@admin_bp.route("/reports", methods=["GET"])
@jwt_required()
def get_admin_reports():
    _, error = _admin_id_or_response()
    if error:
        return error

    today = datetime.now().date()
    month_start = today.replace(day=1)
    raw_from = request.args.get("from", "").strip()
    raw_to = request.args.get("to", "").strip()
    try:
        date_from = datetime.strptime(raw_from, "%Y-%m-%d").date() if raw_from else month_start
        date_to = datetime.strptime(raw_to, "%Y-%m-%d").date() if raw_to else today
    except ValueError:
        return jsonify({"message": "Dates must use YYYY-MM-DD format."}), 400
    if date_from > date_to:
        return jsonify({"message": "from must be on or before to."}), 400

    raw_department_id = request.args.get("department_id")
    department_id = request.args.get("department_id", type=int)
    if raw_department_id and department_id is None:
        return jsonify({"message": "department_id must be an integer."}), 400

    params = {"date_from": date_from, "date_to": date_to}
    queue_filter = "q.queue_date BETWEEN :date_from AND :date_to"
    department_filter = "1 = 1"
    if department_id is not None:
        queue_filter += " AND q.department_id = :department_id"
        department_filter = "d.department_id = :department_id"
        params["department_id"] = department_id

    try:
        summary = db.session.execute(
            text(f"""
                SELECT
                    COUNT(*) AS total_queues,
                    COALESCE(SUM(CASE WHEN q.status = 'COMPLETED' THEN 1 ELSE 0 END), 0) AS completed,
                    COALESCE(SUM(CASE WHEN q.status = 'NO_SHOW' THEN 1 ELSE 0 END), 0) AS no_show,
                    COALESCE(AVG(CASE
                        WHEN q.called_at IS NOT NULL
                        THEN TIMESTAMPDIFF(SECOND, q.created_at, q.called_at)
                    END), 0) AS average_waiting_seconds,
                    COALESCE(AVG(CASE
                        WHEN q.status = 'COMPLETED'
                         AND q.completed_at IS NOT NULL
                         AND q.called_at IS NOT NULL
                        THEN TIMESTAMPDIFF(SECOND, q.called_at, q.completed_at)
                    END), 0) AS average_service_seconds
                FROM queue_numbers q
                WHERE {queue_filter}
            """),
            params
        ).mappings().one()

        departments = db.session.execute(
            text(f"""
                SELECT d.department_id, d.department_name,
                       COUNT(q.queue_id) AS total_queues,
                       COALESCE(SUM(CASE WHEN q.status = 'COMPLETED' THEN 1 ELSE 0 END), 0) AS completed,
                       COALESCE(SUM(CASE WHEN q.status = 'NO_SHOW' THEN 1 ELSE 0 END), 0) AS no_show,
                       COALESCE(AVG(CASE
                           WHEN q.called_at IS NOT NULL
                           THEN TIMESTAMPDIFF(SECOND, q.created_at, q.called_at)
                       END), 0) AS average_waiting_seconds
                FROM departments d
                LEFT JOIN queue_numbers q
                  ON q.department_id = d.department_id
                 AND q.queue_date BETWEEN :date_from AND :date_to
                WHERE {department_filter}
                GROUP BY d.department_id, d.department_name
                ORDER BY d.department_name
            """),
            params
        ).mappings().all()

        return jsonify({
            "date_from": date_from.isoformat(),
            "date_to": date_to.isoformat(),
            "summary": {
                "total_queues": int(summary["total_queues"]),
                "completed": int(summary["completed"]),
                "no_show": int(summary["no_show"]),
                "average_waiting_seconds": int(round(float(summary["average_waiting_seconds"]))),
                "average_service_seconds": int(round(float(summary["average_service_seconds"])))
            },
            "departments": [
                {
                    "department_id": row["department_id"],
                    "department_name": row["department_name"],
                    "total_queues": int(row["total_queues"]),
                    "completed": int(row["completed"]),
                    "no_show": int(row["no_show"]),
                    "average_waiting_seconds": int(round(float(row["average_waiting_seconds"])))
                }
                for row in departments
            ]
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to generate queue reports."}), 500


def _read_system_settings():
    rows = db.session.execute(
        text("SELECT setting_key, setting_value FROM system_settings")
    ).mappings().all()
    stored = {row["setting_key"]: row["setting_value"] for row in rows}

    try:
        queue_digits = int(stored.get("queue_digits", stored.get("queue_number_digits", 3)))
    except (TypeError, ValueError):
        queue_digits = 3
    try:
        qr_expiration_hours = int(stored.get("qr_expiration_hours", 24))
    except (TypeError, ValueError):
        qr_expiration_hours = 24
    try:
        refresh_interval = int(stored.get("refresh_interval", 3))
    except (TypeError, ValueError):
        refresh_interval = 3
    try:
        call_grace_period_seconds = int(stored.get("call_grace_period_seconds", 60))
    except (TypeError, ValueError):
        call_grace_period_seconds = 60
    def read_int(key, default, minimum, maximum):
        try:
            return min(maximum, max(minimum, int(stored.get(key, default))))
        except (TypeError, ValueError):
            return default

    def read_bool(key, default):
        value = stored.get(key)
        if value is None:
            return default
        return str(value).strip().lower() in {"true", "1", "yes", "on"}

    return {
        "queue_digits": min(6, max(2, queue_digits)),
        "call_grace_period_seconds": min(300, max(10, call_grace_period_seconds)),
        "enable_call_countdown": read_bool("enable_call_countdown", True),
        "lock_no_show_during_grace": read_bool("lock_no_show_during_grace", True),
        "allow_customer_acknowledgement": read_bool("allow_customer_acknowledgement", True),
        "qr_expiration_hours": min(168, max(1, qr_expiration_hours)),
        "tv_voice_enabled": read_bool("tv_voice_enabled", True),
        "mobile_call_alert": read_bool("mobile_call_alert", True),
        "mobile_recall_alert": read_bool("mobile_recall_alert", True),
        "mobile_voice_announcement": read_bool("mobile_voice_announcement", True),
        "require_join_confirmation": read_bool("require_join_confirmation", True),
        "allow_mobile_cancellation": read_bool("allow_mobile_cancellation", True),
        "show_people_ahead": read_bool("show_people_ahead", True),
        "show_estimated_wait_time": read_bool("show_estimated_wait_time", False),
        "keep_alert_until_acknowledged": read_bool("keep_alert_until_acknowledged", False),
        "mobile_alert_duration_seconds": read_int("mobile_alert_duration_seconds", 15, 1, 120),
        "show_connection_warning": read_bool("show_connection_warning", True),
        "automatic_reconnection": read_bool("automatic_reconnection", True),
        "offline_retry_interval_seconds": read_int("offline_retry_interval_seconds", 5, 1, 60),
        "refresh_interval": min(60, max(1, refresh_interval)),
        "mobile_queue_enabled": read_bool("mobile_queue_enabled", True),
        "kiosk_queue_enabled": read_bool("kiosk_queue_enabled", True)
    }


def _system_information():
    return {
        "api_server": "Online",
        "database": "Connected",
        "application": current_app.config.get("APPLICATION_NAME", "PCDS Queue Management System"),
        "environment": current_app.config.get("ENVIRONMENT", "Development")
    }


@admin_bp.route("/settings", methods=["GET"])
@jwt_required()
def get_admin_settings():
    _, error = _admin_id_or_response()
    if error:
        return error

    try:
        return jsonify({
            "settings": _read_system_settings(),
            "system_info": _system_information()
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to load system settings."}), 500


@admin_bp.route("/settings", methods=["PUT"])
@jwt_required()
def update_admin_settings():
    admin_id, error = _admin_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        return jsonify({"message": "A JSON object is required."}), 400
    settings = data.get("settings", data)
    if not isinstance(settings, dict):
        return jsonify({"message": "settings must be a JSON object."}), 400

    current = _read_system_settings()
    values = dict(current)
    for key in SYSTEM_SETTING_DEFAULTS:
        if key in settings:
            values[key] = settings[key]

    if isinstance(values["queue_digits"], bool) or not isinstance(values["queue_digits"], int) or not 2 <= values["queue_digits"] <= 6:
        return jsonify({"message": "queue_digits must be an integer from 2 to 6."}), 400
    if isinstance(values["call_grace_period_seconds"], bool) or not isinstance(values["call_grace_period_seconds"], int) or not 10 <= values["call_grace_period_seconds"] <= 300:
        return jsonify({"message": "call_grace_period_seconds must be an integer from 10 to 300."}), 400
    if isinstance(values["mobile_alert_duration_seconds"], bool) or not isinstance(values["mobile_alert_duration_seconds"], int) or not 1 <= values["mobile_alert_duration_seconds"] <= 120:
        return jsonify({"message": "mobile_alert_duration_seconds must be an integer from 1 to 120."}), 400
    if isinstance(values["offline_retry_interval_seconds"], bool) or not isinstance(values["offline_retry_interval_seconds"], int) or not 1 <= values["offline_retry_interval_seconds"] <= 60:
        return jsonify({"message": "offline_retry_interval_seconds must be an integer from 1 to 60."}), 400
    if isinstance(values["qr_expiration_hours"], bool) or not isinstance(values["qr_expiration_hours"], int) or not 1 <= values["qr_expiration_hours"] <= 168:
        return jsonify({"message": "qr_expiration_hours must be an integer from 1 to 168."}), 400
    if isinstance(values["refresh_interval"], bool) or not isinstance(values["refresh_interval"], int) or not 1 <= values["refresh_interval"] <= 60:
        return jsonify({"message": "refresh_interval must be an integer from 1 to 60."}), 400
    for key in (
        "enable_call_countdown", "lock_no_show_during_grace",
        "allow_customer_acknowledgement", "tv_voice_enabled", "mobile_call_alert",
        "mobile_recall_alert", "mobile_voice_announcement", "require_join_confirmation",
        "allow_mobile_cancellation", "show_people_ahead", "show_estimated_wait_time",
        "keep_alert_until_acknowledged", "show_connection_warning", "automatic_reconnection",
        "mobile_queue_enabled", "kiosk_queue_enabled"
    ):
        if not isinstance(values[key], bool):
            return jsonify({"message": f"{key} must be true or false."}), 400

    try:
        for key, value in values.items():
            db.session.execute(
                text("""
                    INSERT INTO system_settings (setting_key, setting_value)
                    VALUES (:setting_key, :setting_value)
                    ON DUPLICATE KEY UPDATE
                        setting_value = VALUES(setting_value)
                """),
                {
                    "setting_key": key,
                    "setting_value": str(value).lower() if isinstance(value, bool) else str(value)
                }
            )
        db.session.commit()
        saved_settings = _read_system_settings()
        return jsonify({
            "message": "System settings saved.",
            "settings": saved_settings,
            "system_info": _system_information()
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to save system settings."}), 500


@admin_bp.route("/queue/reset-today", methods=["POST"])
@jwt_required()
def reset_today_queue_numbers():
    admin_id, error = _admin_id_or_response()
    if error:
        return error

    try:
        active_count = db.session.execute(text("""
            SELECT COUNT(*) FROM queue_numbers
            WHERE queue_date = CURDATE()
              AND status IN ('WAITING', 'CALLED', 'SERVING')
            FOR UPDATE
        """)).scalar()
        if active_count:
            db.session.rollback()
            return jsonify({
                "message": "Queue numbers cannot be reset while there are active queues. Complete, cancel, or mark them no-show first."
            }), 409

        deleted = db.session.execute(text("""
            DELETE FROM queue_numbers
            WHERE queue_date = CURDATE()
        """)).rowcount
        db.session.execute(text("""
            DELETE FROM queue_counters
            WHERE queue_date = CURDATE()
        """))
        db.session.execute(text("""
            INSERT INTO audit_logs (staff_id, action, entity_type, description)
            VALUES (:staff_id, 'RESET_QUEUE_NUMBERS', 'QUEUE', :description)
        """), {
            "staff_id": admin_id,
            "description": f"Reset today's queue numbering and removed {deleted} terminal queue record(s)."
        })
        db.session.commit()
        return jsonify({
            "message": "Today's queue numbers have been reset.",
            "deleted_queues": deleted
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to reset today's queue numbers."}), 500
