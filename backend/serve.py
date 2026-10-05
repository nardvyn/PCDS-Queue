import os

from waitress import serve

from app import create_app


def _production_setting(name: str) -> str:
    value = os.getenv(name, "").strip()
    if not value:
        raise RuntimeError(f"{name} must be set for production.")
    return value


def main() -> None:
    environment = _production_setting("APP_ENV")
    if environment.casefold() != "production":
        raise RuntimeError("APP_ENV must be set to Production.")

    database_user = _production_setting("DB_USER")
    if database_user.casefold() == "root":
        raise RuntimeError("Use a dedicated MySQL application user, not root.")
    _production_setting("DB_PASSWORD")
    _production_setting("DB_NAME")
    _production_setting("DB_HOST")

    jwt_secret = _production_setting("JWT_SECRET_KEY")
    if len(jwt_secret) < 32 or jwt_secret in {
        "development-only-change-this",
        "change-this-to-a-long-random-secret-key",
    }:
        raise RuntimeError("JWT_SECRET_KEY must be a unique secret of at least 32 characters.")

    try:
        port = int(os.getenv("PORT", "5000"))
        threads = int(os.getenv("WAITRESS_THREADS", "8"))
    except ValueError as error:
        raise RuntimeError("PORT and WAITRESS_THREADS must be integers.") from error

    if not 1 <= port <= 65535:
        raise RuntimeError("PORT must be between 1 and 65535.")
    if threads < 1:
        raise RuntimeError("WAITRESS_THREADS must be at least 1.")

    app = create_app()
    serve(
        app,
        host=os.getenv("WAITRESS_HOST", "0.0.0.0"),
        port=port,
        threads=threads,
    )


if __name__ == "__main__":
    main()
