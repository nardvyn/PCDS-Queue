import hashlib
import secrets
from datetime import datetime, timedelta

from flask import Blueprint, jsonify, request
from flask_jwt_extended import get_jwt, get_jwt_identity, jwt_required
from sqlalchemy import text

from app import db


qr_bp = Blueprint("qr", __name__, url_prefix="/api")


def _admin_error_response():
    if get_jwt().get("role") != "ADMIN":
        return jsonify({"message": "Administrator access is required."}), 403

    try:
        admin_id = int(get_jwt_identity())
    except (TypeError, ValueError):
        return jsonify({"message": "Invalid administrator session."}), 401

    admin = db.session.execute(
        text("""
            SELECT staff_id FROM staff_users
            WHERE staff_id = :staff_id AND role = 'ADMIN' AND account_status = 'ACTIVE'
            LIMIT 1
        """),
        {"staff_id": admin_id}
    ).first()
    if not admin:
        return jsonify({"message": "Administrator account is not active."}), 403
    return None


@qr_bp.route("/admin/qr", methods=["GET"])
@jwt_required()
def get_qr_sessions():
    error = _admin_error_response()
    if error:
        return error

    raw_department_id = request.args.get("department_id")
    department_id = request.args.get("department_id", type=int)
    if raw_department_id and department_id is None:
        return jsonify({"message": "department_id must be an integer."}), 400

    filters = []
    params = {}
    if department_id is not None:
        filters.append("qs.department_id = :department_id")
        params["department_id"] = department_id
    where_clause = f"WHERE {' AND '.join(filters)}" if filters else ""

    rows = db.session.execute(
        text(f"""
            SELECT qs.qr_session_id, qs.department_id, d.department_name,
                   qs.status, qs.created_at, qs.expires_at, qs.revoked_at,
                   CASE
                       WHEN qs.status = 'ACTIVE' AND qs.expires_at <= NOW() THEN 'EXPIRED'
                       ELSE qs.status
                   END AS display_status
            FROM qr_sessions qs
            JOIN departments d ON d.department_id = qs.department_id
            {where_clause}
            ORDER BY qs.created_at DESC, qs.qr_session_id DESC
            LIMIT 500
        """),
        params
    ).mappings().all()

    return jsonify({
        "sessions": [
            {
                "qr_session_id": row["qr_session_id"],
                "department_id": row["department_id"],
                "department_name": row["department_name"],
                "status": row["display_status"],
                "created_at": row["created_at"].isoformat() if row["created_at"] else None,
                "expires_at": row["expires_at"].isoformat() if row["expires_at"] else None,
                "revoked_at": row["revoked_at"].isoformat() if row["revoked_at"] else None
            }
            for row in rows
        ]
    }), 200


@qr_bp.route("/admin/qr/generate", methods=["POST"])
@jwt_required()
def generate_qr_session():
    error = _admin_error_response()
    if error:
        return error

    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        return jsonify({"message": "A JSON object is required."}), 400
    department_id = data.get("department_id")
    if isinstance(department_id, bool) or not isinstance(department_id, int) or department_id <= 0:
        return jsonify({"message": "A valid department_id is required."}), 400

    token = secrets.token_urlsafe(32)
    token_hash = hashlib.sha256(token.encode("utf-8")).hexdigest()

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
        if not department or not department["is_active"]:
            return jsonify({"message": "Select an active department."}), 404

        configured_hours = db.session.execute(
            text("SELECT setting_value FROM system_settings WHERE setting_key = 'qr_expiration_hours'")
        ).scalar()
        try:
            expiration_hours = int(configured_hours) if configured_hours is not None else 24
        except (TypeError, ValueError):
            expiration_hours = 24
        expiration_hours = min(168, max(1, expiration_hours))
        now = datetime.now()
        expires_at = now + timedelta(hours=expiration_hours)

        db.session.execute(
            text("""
                UPDATE qr_sessions
                SET status = 'REVOKED', revoked_at = :revoked_at
                WHERE department_id = :department_id AND status = 'ACTIVE'
            """),
            {"department_id": department_id, "revoked_at": now}
        )
        result = db.session.execute(
            text("""
                INSERT INTO qr_sessions (department_id, token_hash, status, expires_at)
                VALUES (:department_id, :token_hash, 'ACTIVE', :expires_at)
            """),
            {
                "department_id": department_id,
                "token_hash": token_hash,
                "expires_at": expires_at
            }
        )
        db.session.commit()
        return jsonify({
            "message": "QR session generated. Save the QR now; its secret cannot be retrieved later.",
            "session": {
                "qr_session_id": result.lastrowid,
                "department_id": department_id,
                "department_name": department["department_name"],
                "token": token,
                "status": "ACTIVE",
                "created_at": now.isoformat(),
                "expires_at": expires_at.isoformat()
            }
        }), 201
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to generate QR session."}), 500


@qr_bp.route("/admin/qr/<int:session_id>/deactivate", methods=["POST"])
@jwt_required()
def deactivate_qr_session(session_id):
    error = _admin_error_response()
    if error:
        return error

    try:
        result = db.session.execute(
            text("""
                UPDATE qr_sessions
                SET status = 'REVOKED', revoked_at = :revoked_at
                WHERE qr_session_id = :session_id AND status = 'ACTIVE'
            """),
            {"session_id": session_id, "revoked_at": datetime.now()}
        )
        db.session.commit()
        if result.rowcount == 0:
            return jsonify({"message": "Active QR session not found."}), 404
        return jsonify({"message": "QR session deactivated."}), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to deactivate QR session."}), 500


@qr_bp.route("/kiosk/departments/<int:department_id>/qr-session", methods=["POST"])
def create_kiosk_qr_session(department_id):
    token = secrets.token_urlsafe(32)
    token_hash = hashlib.sha256(token.encode("utf-8")).hexdigest()

    try:
        department = db.session.execute(
            text("""
                SELECT department_id, department_name, queue_prefix, is_active, queue_is_open
                FROM departments
                WHERE department_id = :department_id
                LIMIT 1
            """),
            {"department_id": department_id}
        ).mappings().first()
        if not department or not department["is_active"]:
            return jsonify({"message": "Department is not available."}), 404

        kiosk_enabled = db.session.execute(
            text("SELECT setting_value FROM system_settings WHERE setting_key = 'kiosk_queue_enabled'")
        ).scalar()
        if kiosk_enabled is not None and str(kiosk_enabled).strip().lower() not in {"true", "1", "yes", "on"}:
            return jsonify({"message": "Kiosk queue entry is currently disabled."}), 403

        expiration_setting = db.session.execute(
            text("SELECT setting_value FROM system_settings WHERE setting_key = 'qr_expiration_hours'")
        ).scalar()
        try:
            expiration_hours = int(expiration_setting) if expiration_setting is not None else 24
        except (TypeError, ValueError):
            expiration_hours = 24
        expiration_hours = min(168, max(1, expiration_hours))

        now = datetime.now()
        expires_at = now + timedelta(hours=expiration_hours)
        result = db.session.execute(
            text("""
                INSERT INTO qr_sessions (department_id, token_hash, status, expires_at)
                VALUES (:department_id, :token_hash, 'ACTIVE', :expires_at)
            """),
            {
                "department_id": department_id,
                "token_hash": token_hash,
                "expires_at": expires_at
            }
        )
        db.session.commit()
        return jsonify({
            "session_id": result.lastrowid,
            "token": token,
            "expires_at": expires_at.isoformat(),
            "department": {
                "department_id": department["department_id"],
                "department_name": department["department_name"],
                "queue_prefix": department["queue_prefix"],
                "queue_is_open": bool(department["queue_is_open"])
            }
        }), 201
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to create QR session."}), 500


@qr_bp.route("/qr/validate/<string:token>", methods=["GET"])
def validate_qr_token(token):
    if not token or len(token) > 200:
        return jsonify({"message": "Invalid QR token."}), 400

    token_hash = hashlib.sha256(token.encode("utf-8")).hexdigest()
    try:
        session = db.session.execute(
            text("""
                SELECT qs.qr_session_id, qs.department_id, qs.status, qs.expires_at,
                       qs.scanned_at, d.department_name, d.queue_prefix,
                       d.is_active, d.queue_is_open
                FROM qr_sessions qs
                JOIN departments d ON d.department_id = qs.department_id
                WHERE qs.token_hash = :token_hash
                LIMIT 1
            """),
            {"token_hash": token_hash}
        ).mappings().first()

        if not session:
            return jsonify({"valid": False, "message": "QR token not found."}), 404

        now = datetime.now()
        if session["status"] != "ACTIVE" or session["expires_at"] <= now or not session["is_active"]:
            if session["status"] == "ACTIVE" and session["expires_at"] <= now:
                db.session.execute(
                    text("UPDATE qr_sessions SET status = 'EXPIRED' WHERE qr_session_id = :session_id"),
                    {"session_id": session["qr_session_id"]}
                )
                db.session.commit()
            return jsonify({"valid": False, "message": "QR token is expired or inactive."}), 410

        mobile_enabled = db.session.execute(
            text("SELECT setting_value FROM system_settings WHERE setting_key = 'mobile_queue_enabled'")
        ).scalar()
        mobile_queue_enabled = (
            True
            if mobile_enabled is None
            else str(mobile_enabled).strip().lower() in {"true", "1", "yes", "on"}
        )

        db.session.execute(
            text("""
                UPDATE qr_sessions
                SET scanned_at = COALESCE(scanned_at, :scanned_at)
                WHERE qr_session_id = :session_id
            """),
            {
                "scanned_at": now,
                "session_id": session["qr_session_id"]
            }
        )
        db.session.commit()

        return jsonify({
            "valid": True,
            "session_id": session["qr_session_id"],
            "department": {
                "department_id": session["department_id"],
                "department_name": session["department_name"],
                "queue_prefix": session["queue_prefix"],
                "queue_is_open": bool(session["queue_is_open"])
            },
            "expires_at": session["expires_at"].isoformat(),
            "mobile_queue_enabled": mobile_queue_enabled
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to validate QR token."}), 500


@qr_bp.get("/kiosk/qr-session/<string:token>/status")
def get_kiosk_qr_session_status(token):
    if not token or len(token) > 200:
        return jsonify({"message": "Invalid QR token."}), 400

    token_hash = hashlib.sha256(token.encode("utf-8")).hexdigest()
    try:
        session = db.session.execute(
            text("""
                SELECT scanned_at
                FROM qr_sessions
                WHERE token_hash = :token_hash
                  AND status = 'ACTIVE'
                  AND expires_at > NOW()
                LIMIT 1
            """),
            {"token_hash": token_hash}
        ).mappings().first()

        if not session:
            return jsonify({"scanned": False, "valid": False}), 200

        return jsonify({
            "scanned": session["scanned_at"] is not None,
            "valid": True
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to check QR scan status."}), 500
