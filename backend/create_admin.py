import getpass

from werkzeug.security import generate_password_hash
from sqlalchemy import text

from app import create_app, db

app = create_app()


def create_admin():
    print("\n=== PCDS CREATE ADMIN ===\n")

    full_name = input("Full name: ").strip()
    username = input("Username: ").strip()
    password = getpass.getpass("Password: ")
    confirm_password = getpass.getpass("Confirm password: ")

    if not full_name or not username or not password:
        print("All fields are required.")
        return

    if password != confirm_password:
        print("Passwords do not match.")
        return

    if len(password) < 8:
        print("Password must contain at least 8 characters.")
        return

    with app.app_context():
        existing = db.session.execute(
            text("""
                SELECT staff_id
                FROM staff_users
                WHERE username = :username
                LIMIT 1
            """),
            {"username": username}
        ).first()

        if existing:
            print("Username already exists.")
            return

        password_hash = generate_password_hash(password)

        db.session.execute(
            text("""
                INSERT INTO staff_users (
                    full_name,
                    username,
                    password_hash,
                    role,
                    account_status
                )
                VALUES (
                    :full_name,
                    :username,
                    :password_hash,
                    'ADMIN',
                    'ACTIVE'
                )
            """),
            {
                "full_name": full_name,
                "username": username,
                "password_hash": password_hash
            }
        )

        db.session.commit()

        print("\nAdmin successfully created.")


if __name__ == "__main__":
    create_admin()
