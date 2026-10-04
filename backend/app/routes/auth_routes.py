from datetime import datetime

from flask import Blueprint, jsonify, request
from flask_jwt_extended import (
    create_access_token,
    get_jwt_identity,
    jwt_required
)
from sqlalchemy import text
from werkzeug.security import check_password_hash

from app import db

auth_bp = Blueprint(
    "auth",
    __name__,
    url_prefix="/api/auth"
)


@auth_bp.route("/login", methods=["GET", "POST"])
def login():
    if request.method == "GET":
        return jsonify({
            "message": "Use POST to log in.",
            "example": {
                "method": "POST",
                "body": {
                    "username": "cashier1",
                    "password": "cashier1"
                }
            }
        }), 200

    data = request.get_json(silent=True) or {}

    username = str(data.get("username", "")).strip()
    password = str(data.get("password", ""))

    if not username or not password:
        return jsonify({
            "message": "Username and password are required."
        }), 400

    user = db.session.execute(
        text("""
            SELECT
                staff_id,
                full_name,
                username,
                password_hash,
                role,
                account_status
            FROM staff_users
            WHERE username = :username
            LIMIT 1
        """),
        {"username": username}
    ).mappings().first()

    # Generic error message so we don't reveal whether the username exists.
    if not user or not check_password_hash(user["password_hash"], password):
        return jsonify({
            "message": "Invalid username or password."
        }), 401

    if user["account_status"] != "ACTIVE":
        return jsonify({
            "message": "Account is not active."
        }), 403

    # JWT identity must be a string.
    access_token = create_access_token(
        identity=str(user["staff_id"]),
        additional_claims={"role": user["role"]}
    )

    db.session.execute(
        text("""
            UPDATE staff_users
            SET last_login_at = :last_login
            WHERE staff_id = :staff_id
        """),
        {"last_login": datetime.now(), "staff_id": user["staff_id"]}
    )

    db.session.execute(
        text("""
            INSERT INTO audit_logs (staff_id, action, entity_type, entity_id, description)
            VALUES (:staff_id, 'LOGIN', 'AUTH', :staff_id, 'Staff successfully logged in.')
        """),
        {"staff_id": user["staff_id"]}
    )

    db.session.commit()

    return jsonify({
        "message": "Login successful.",
        "access_token": access_token,
        "user": {
            "staff_id": user["staff_id"],
            "full_name": user["full_name"],
            "username": user["username"],
            "role": user["role"]
        }
    }), 200


@auth_bp.route("/me", methods=["GET"])
@jwt_required()
def me():
    staff_id = get_jwt_identity()

    user = db.session.execute(
        text("""
            SELECT
                su.staff_id,
                su.full_name,
                su.username,
                su.role,
                su.account_status,
                su.last_login_at,

                sw.window_id,
                sw.window_number,
                sw.window_name,

                d.department_id,
                d.department_name,
                d.queue_prefix

            FROM staff_users su

            LEFT JOIN service_windows sw
                ON sw.assigned_staff_id = su.staff_id

            LEFT JOIN departments d
                ON d.department_id = sw.department_id

            WHERE su.staff_id = :staff_id
            LIMIT 1
        """),
        {"staff_id": staff_id}
    ).mappings().first()

    if not user:
        return jsonify({"message": "User not found."}), 404

    return jsonify({
        "staff_id": user["staff_id"],
        "full_name": user["full_name"],
        "username": user["username"],
        "role": user["role"],
        "account_status": user["account_status"],
        "last_login_at": (
            user["last_login_at"].isoformat()
            if user["last_login_at"]
            else None
        ),
        "assignment": {
            "department_id": user["department_id"],
            "department_name": user["department_name"],
            "queue_prefix": user["queue_prefix"],
            "window_id": user["window_id"],
            "window_number": user["window_number"],
            "window_name": user["window_name"]
        }
    }), 200

