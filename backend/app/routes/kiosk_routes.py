from flask import Blueprint, jsonify
from sqlalchemy import text

from app import db


kiosk_bp = Blueprint("kiosk", __name__, url_prefix="/api/kiosk")


@kiosk_bp.get("/departments")
def get_departments():
    try:
        kiosk_setting = db.session.execute(
            text("SELECT setting_value FROM system_settings WHERE setting_key = 'kiosk_queue_enabled' LIMIT 1")
        ).scalar()
        kiosk_queue_enabled = (
            True
            if kiosk_setting is None
            else str(kiosk_setting).strip().lower() in {"true", "1", "yes", "on"}
        )
        rows = db.session.execute(
            text("""
                SELECT department_id, department_name, queue_prefix, is_active, queue_is_open
                FROM departments
                WHERE is_active = 1
                ORDER BY department_name
            """)
        ).mappings().all()

        return jsonify({
            "departments": [
                {
                    "department_id": row["department_id"],
                    "department_name": row["department_name"],
                    "queue_prefix": row["queue_prefix"],
                    "status": "ACTIVE" if row["is_active"] else "INACTIVE",
                    "queue_is_open": bool(row["queue_is_open"]),
                    "kiosk_queue_enabled": kiosk_queue_enabled
                }
                for row in rows
            ]
        }), 200
    except Exception:
        db.session.rollback()
        return jsonify({"message": "Unable to load active departments."}), 500
