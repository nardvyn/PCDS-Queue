from flask import Flask, jsonify
from flask_sqlalchemy import SQLAlchemy
from flask_jwt_extended import JWTManager
from flask_cors import CORS
from sqlalchemy import text

from .config import Config

db = SQLAlchemy()
jwt = JWTManager()


def create_app():
    app = Flask(__name__)

    app.config.from_object(Config)

    db.init_app(app)
    jwt.init_app(app)

    CORS(app)

    from app.routes.auth_routes import auth_bp
    from app.routes.admin_routes import admin_bp
    from app.routes.kiosk_routes import kiosk_bp
    from app.routes.queue_routes import queue_bp
    from app.routes.qr_routes import qr_bp
    from app.routes.staff_routes import staff_bp
    app.register_blueprint(auth_bp)
    app.register_blueprint(admin_bp)
    app.register_blueprint(kiosk_bp)
    app.register_blueprint(queue_bp)
    app.register_blueprint(qr_bp)
    app.register_blueprint(staff_bp)

    @app.route("/", methods=["GET"])
    def index():
        return jsonify({
            "message": "PCDS Queue Management API is running.",
            "health": "/api/health",
            "login": "/api/auth/login",
            "queue_display": "/api/queue/display"
        }), 200

    @app.route("/api/health", methods=["GET"])
    def health():
        try:
            db.session.execute(text("SELECT 1"))
        except Exception:
            db.session.rollback()
            return jsonify({
                "status": "offline",
                "service": "PCDS Queue API",
                "database": "disconnected"
            }), 503

        return jsonify({
            "status": "online",
            "service": "PCDS Queue API",
            "database": "connected"
        }), 200

    @app.route("/api/test/departments", methods=["GET"])
    def test_departments():
        try:
            result = db.session.execute(
                text("""
                    SELECT
                        department_id,
                        department_name,
                        queue_prefix,
                        is_active,
                        queue_is_open
                    FROM departments
                    ORDER BY department_id
                """)
            )

            departments = []

            for row in result.mappings():
                departments.append({
                    "department_id": row["department_id"],
                    "department_name": row["department_name"],
                    "queue_prefix": row["queue_prefix"],
                    "is_active": bool(row["is_active"]),
                    "queue_is_open": bool(row["queue_is_open"])
                })

            return jsonify(departments), 200

        except Exception as error:
            return jsonify({
                "error": str(error)
            }), 500

    return app
