import os
from pathlib import Path
from urllib.parse import quote_plus
from dotenv import load_dotenv
from sqlalchemy.engine import make_url

load_dotenv(Path(__file__).resolve().parents[1] / ".env")


class Config:
    APPLICATION_NAME = "PCDS Queue Management System"
    ENVIRONMENT = os.getenv("APP_ENV", "Development")

    DATABASE_URL = os.getenv("MYSQL_URL") or os.getenv("DATABASE_URL")
    if DATABASE_URL:
        parsed_database_url = make_url(DATABASE_URL)
        if parsed_database_url.drivername == "mysql":
            parsed_database_url = parsed_database_url.set(drivername="mysql+pymysql")
        elif parsed_database_url.drivername != "mysql+pymysql":
            raise ValueError("MYSQL_URL must use the mysql:// or mysql+pymysql:// scheme.")
        SQLALCHEMY_DATABASE_URI = parsed_database_url.render_as_string(hide_password=False)
    else:
        DB_HOST = os.getenv("DB_HOST", "localhost")
        DB_PORT = os.getenv("DB_PORT", "3306")
        DB_NAME = os.getenv("DB_NAME", "pcds_queue_db")
        DB_USER = os.getenv("DB_USER", "root")
        DB_PASSWORD = os.getenv("DB_PASSWORD", "")

        SQLALCHEMY_DATABASE_URI = (
            f"mysql+pymysql://"
            f"{quote_plus(DB_USER)}:"
            f"{quote_plus(DB_PASSWORD)}@"
            f"{DB_HOST}:"
            f"{DB_PORT}/"
            f"{DB_NAME}"
            f"?charset=utf8mb4"
        )

    SQLALCHEMY_TRACK_MODIFICATIONS = False

    JWT_SECRET_KEY = os.getenv(
        "JWT_SECRET_KEY",
        "development-only-change-this"
    )
