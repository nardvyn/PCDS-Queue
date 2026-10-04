import getpass

from sqlalchemy import text
from werkzeug.security import generate_password_hash

from app import create_app, db


app = create_app()


def create_staff():
    print("\n=== PCDS CREATE STAFF ACCOUNT ===\n")

    full_name = input("Full name: ").strip()
    username = input("Username: ").strip()
    password = getpass.getpass("Password: ")
    confirm_password = getpass.getpass("Confirm password: ")

    if not full_name or not username or not password:
        print("\nAll fields are required.")
        return

    if password != confirm_password:
        print("\nPasswords do not match.")
        return

    if len(password) < 8:
        print("\nPassword must be at least 8 characters.")
        return

    with app.app_context():
        try:
            departments = db.session.execute(
                text(
                    """
                    SELECT department_id, department_name
                    FROM departments
                    WHERE is_active = 1
                    ORDER BY department_name
                    """
                )
            ).mappings().all()

            if not departments:
                print("\nNo active departments are configured.")
                return

            print("\nAvailable departments:")
            for department in departments:
                print(
                    f"  {department['department_id']}: "
                    f"{department['department_name']}"
                )

            try:
                department_id = int(input("\nDepartment ID: "))
            except ValueError:
                print("\nDepartment ID must be a number.")
                return

            existing_user = db.session.execute(
                text(
                    """
                    SELECT staff_id
                    FROM staff_users
                    WHERE username = :username
                    LIMIT 1
                    """
                ),
                {"username": username},
            ).first()

            if existing_user:
                print("\nUsername already exists.")
                return

            department = db.session.execute(
                text(
                    """
                    SELECT department_id, department_name
                    FROM departments
                    WHERE department_id = :department_id
                      AND is_active = 1
                    LIMIT 1
                    """
                ),
                {"department_id": department_id},
            ).mappings().first()

            if not department:
                print("\nDepartment not found or inactive.")
                return

            result = db.session.execute(
                text(
                    """
                    INSERT INTO staff_users (
                        full_name,
                        username,
                        password_hash,
                        role,
                        department_id,
                        account_status
                    )
                    VALUES (
                        :full_name,
                        :username,
                        :password_hash,
                        'STAFF',
                        :department_id,
                        'ACTIVE'
                    )
                    """
                ),
                {
                    "full_name": full_name,
                    "username": username,
                    "password_hash": generate_password_hash(password),
                    "department_id": department_id,
                },
            )

            db.session.commit()

            print("\n================================")
            print("STAFF CREATED SUCCESSFULLY")
            print("================================")
            print(f"Staff ID:   {result.lastrowid}")
            print(f"Name:       {full_name}")
            print(f"Username:   {username}")
            print(f"Department: {department['department_name']}")
            print("Window:     Selected when the staff member starts a shift")
            print("================================\n")
        except Exception as error:
            db.session.rollback()
            print("\nUnable to create staff.")
            print(error)


if __name__ == "__main__":
    create_staff()
