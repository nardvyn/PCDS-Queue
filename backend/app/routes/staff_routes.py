from datetime import datetime

from flask import Blueprint, jsonify, request
from flask_jwt_extended import get_jwt, get_jwt_identity, jwt_required
from sqlalchemy import text

from app import db


staff_bp = Blueprint("staff", __name__, url_prefix="/api/staff")


def _staff_id_or_response():
    claims = get_jwt()
    if claims.get("role") != "STAFF":
        return None, (jsonify({"message": "Staff access is required."}), 403)

    try:
        return int(get_jwt_identity()), None
    except (TypeError, ValueError):
        return None, (jsonify({"message": "Invalid staff session."}), 401)


def _staff_department(staff_id):
    return db.session.execute(
        text(
            """
            SELECT
                su.staff_id,
                su.full_name,
                su.department_id,
                d.department_name
            FROM staff_users su
            JOIN departments d ON d.department_id = su.department_id
            WHERE su.staff_id = :staff_id
              AND su.account_status = 'ACTIVE'
              AND d.is_active = 1
            LIMIT 1
            """
        ),
        {"staff_id": staff_id},
    ).mappings().first()


@staff_bp.route("/windows", methods=["GET"])
@jwt_required()
def get_windows():
    staff_id, error = _staff_id_or_response()
    if error:
        return error

    staff = _staff_department(staff_id)
    if not staff:
        return jsonify(
            {"message": "Your account does not have an active department assignment."}
        ), 403

    windows = db.session.execute(
        text(
            """
            SELECT
                sw.window_id,
                sw.window_number,
                sw.window_name,
                sw.status AS window_status,
                active_shift.shift_id,
                active_staff.full_name AS assigned_staff_name
            FROM service_windows sw
            LEFT JOIN staff_shifts active_shift
                ON active_shift.window_id = sw.window_id
               AND active_shift.status = 'ACTIVE'
            LEFT JOIN staff_users active_staff
                ON active_staff.staff_id = active_shift.staff_id
            WHERE sw.department_id = :department_id
                            AND sw.status <> 'CLOSED'
            ORDER BY sw.window_number
            """
        ),
        {"department_id": staff["department_id"]},
    ).mappings()

    return jsonify(
        {
            "department": {
                "department_id": staff["department_id"],
                "department_name": staff["department_name"],
            },
            "windows": [
                {
                    "window_id": window["window_id"],
                    "window_number": window["window_number"],
                    "window_name": window["window_name"],
                    "available": (
                        window["window_status"] == "ACTIVE"
                        and window["shift_id"] is None
                    ),
                    "assigned_staff_name": window["assigned_staff_name"],
                }
                for window in windows
            ],
        }
    ), 200


@staff_bp.route("/start-shift", methods=["POST"])
@jwt_required()
def start_shift():
    staff_id, error = _staff_id_or_response()
    if error:
        return error

    data = request.get_json(silent=True) or {}
    window_id = data.get("window_id")

    if not isinstance(window_id, int) or window_id <= 0:
        return jsonify({"message": "A valid window_id is required."}), 400

    try:
        staff = _staff_department(staff_id)
        if not staff:
            return jsonify(
                {"message": "Your account does not have an active department assignment."}
            ), 403

        existing_shift = db.session.execute(
            text(
                """
                SELECT shift_id
                FROM staff_shifts
                WHERE staff_id = :staff_id
                  AND status = 'ACTIVE'
                LIMIT 1
                FOR UPDATE
                """
            ),
            {"staff_id": staff_id},
        ).mappings().first()

        if existing_shift:
            return jsonify(
                {"message": "You already have an active shift."}
            ), 409

        window = db.session.execute(
            text(
                """
                SELECT window_id, window_number, window_name, status
                FROM service_windows
                WHERE window_id = :window_id
                  AND department_id = :department_id
                LIMIT 1
                FOR UPDATE
                """
            ),
            {
                "window_id": window_id,
                "department_id": staff["department_id"],
            },
        ).mappings().first()

        if not window:
            return jsonify(
                {"message": "This window is not part of your department."}
            ), 404

        if window["status"] != "ACTIVE":
            return jsonify({"message": "This window is currently unavailable."}), 409

        window_shift = db.session.execute(
            text(
                """
                SELECT shift_id
                FROM staff_shifts
                WHERE window_id = :window_id
                  AND status = 'ACTIVE'
                LIMIT 1
                FOR UPDATE
                """
            ),
            {"window_id": window_id},
        ).mappings().first()

        if window_shift:
            return jsonify({"message": "This window is already in use."}), 409

        started_at = datetime.now()
        result = db.session.execute(
            text(
                """
                INSERT INTO staff_shifts (
                    staff_id,
                    window_id,
                    department_id,
                    status,
                    started_at
                )
                VALUES (
                    :staff_id,
                    :window_id,
                    :department_id,
                    'ACTIVE',
                    :started_at
                )
                """
            ),
            {
                "staff_id": staff_id,
                "window_id": window["window_id"],
                "department_id": staff["department_id"],
                "started_at": started_at,
            },
        )

        db.session.commit()

        return jsonify(
            {
                "message": "Shift started.",
                "shift": {
                    "shift_id": result.lastrowid,
                    "department": staff["department_name"],
                    "window_id": window["window_id"],
                    "window_number": window["window_number"],
                    "window_name": window["window_name"],
                    "status": "ACTIVE",
                    "started_at": started_at.isoformat(),
                },
            }
        ), 201
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to start the shift."}), 500


@staff_bp.route("/current-shift", methods=["GET"])
@jwt_required()
def current_shift():
    staff_id, error = _staff_id_or_response()
    if error:
        return error

    shift = db.session.execute(
        text(
            """
            SELECT
                ss.shift_id,
                ss.status,
                ss.started_at,
                sw.window_id,
                sw.window_number,
                sw.window_name,
                d.department_id,
                d.department_name
            FROM staff_shifts ss
            JOIN service_windows sw ON sw.window_id = ss.window_id
            JOIN departments d ON d.department_id = ss.department_id
            WHERE ss.staff_id = :staff_id
              AND ss.status = 'ACTIVE'
            ORDER BY ss.shift_id DESC
            LIMIT 1
            """
        ),
        {"staff_id": staff_id},
    ).mappings().first()

    if not shift:
        return jsonify({"has_active_shift": False, "shift": None}), 200

    return jsonify(
        {
            "has_active_shift": True,
            "shift": {
                "shift_id": shift["shift_id"],
                "status": shift["status"],
                "department_id": shift["department_id"],
                "department_name": shift["department_name"],
                "window_id": shift["window_id"],
                "window_number": shift["window_number"],
                "window_name": shift["window_name"],
                "started_at": shift["started_at"].isoformat(),
            },
        }
    ), 200


@staff_bp.route("/end-shift", methods=["POST"])
@jwt_required()
def end_shift():
    staff_id, error = _staff_id_or_response()
    if error:
        return error

    try:
        shift = db.session.execute(
            text(
                """
                SELECT
                    ss.shift_id,
                    ss.window_id,
                    ss.department_id,
                    sw.window_number,
                    sw.window_name,
                    d.department_name
                FROM staff_shifts ss
                JOIN service_windows sw ON sw.window_id = ss.window_id
                JOIN departments d ON d.department_id = ss.department_id
                WHERE ss.staff_id = :staff_id
                  AND ss.status = 'ACTIVE'
                LIMIT 1
                FOR UPDATE
                """
            ),
            {"staff_id": staff_id},
        ).mappings().first()

        if not shift:
            return jsonify({"message": "No active shift was found."}), 404

        db.session.execute(
            text(
                """
                UPDATE staff_shifts
                SET status = 'ENDED',
                    ended_at = :ended_at
                WHERE shift_id = :shift_id
                """
            ),
            {"shift_id": shift["shift_id"], "ended_at": datetime.now()},
        )
        db.session.commit()

        return jsonify(
            {
                "message": "Shift ended.",
                "shift": {
                    "shift_id": shift["shift_id"],
                    "department": shift["department_name"],
                    "window_id": shift["window_id"],
                    "window_number": shift["window_number"],
                    "window_name": shift["window_name"],
                    "status": "ENDED",
                },
            }
        ), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to end the shift."}), 500
