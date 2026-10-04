"""Apply the per-shift window-assignment migration to the configured database."""

from pathlib import Path

from sqlalchemy import text

from app import create_app, db


MIGRATION_PATH = (
    Path(__file__).resolve().parents[1]
    / "database"
    / "migrations"
    / "002_staff_shifts.sql"
)


def column_exists(connection, table_name, column_name):
    return connection.execute(
        text(
            """
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = :table_name
              AND column_name = :column_name
            """
        ),
        {"table_name": table_name, "column_name": column_name},
    ).scalar() is not None


def table_exists(connection, table_name):
    return connection.execute(
        text(
            """
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name = :table_name
            """
        ),
        {"table_name": table_name},
    ).scalar() is not None


def constraint_exists(connection, constraint_name):
    return connection.execute(
        text(
            """
            SELECT 1
            FROM information_schema.table_constraints
            WHERE constraint_schema = DATABASE()
              AND constraint_name = :constraint_name
            """
        ),
        {"constraint_name": constraint_name},
    ).scalar() is not None


def index_exists(connection, table_name, index_name):
    return connection.execute(
        text(
            """
            SELECT 1
            FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND table_name = :table_name
              AND index_name = :index_name
            """
        ),
        {"table_name": table_name, "index_name": index_name},
    ).scalar() is not None


def main():
    app = create_app()

    with app.app_context():
        connection = db.session

        if not column_exists(connection, "staff_users", "department_id"):
            connection.execute(
                text(
                    "ALTER TABLE staff_users "
                    "ADD COLUMN department_id INT UNSIGNED NULL AFTER role"
                )
            )

        if not index_exists(connection, "staff_users", "idx_staff_department"):
            connection.execute(
                text(
                    "ALTER TABLE staff_users "
                    "ADD INDEX idx_staff_department (department_id)"
                )
            )

        if not constraint_exists(connection, "fk_staff_department"):
            connection.execute(
                text(
                    "ALTER TABLE staff_users "
                    "ADD CONSTRAINT fk_staff_department "
                    "FOREIGN KEY (department_id) REFERENCES departments(department_id) "
                    "ON DELETE SET NULL ON UPDATE CASCADE"
                )
            )

        if not table_exists(connection, "staff_shifts"):
            connection.execute(
                text(
                    """
                    CREATE TABLE staff_shifts (
                        shift_id INT UNSIGNED NOT NULL AUTO_INCREMENT,
                        staff_id INT UNSIGNED NOT NULL,
                        window_id INT UNSIGNED NOT NULL,
                        department_id INT UNSIGNED NOT NULL,
                        status ENUM('ACTIVE', 'ENDED') NOT NULL DEFAULT 'ACTIVE',
                        started_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        ended_at DATETIME NULL,
                        PRIMARY KEY (shift_id),
                        KEY idx_shift_staff (staff_id),
                        KEY idx_shift_window (window_id),
                        KEY idx_shift_department (department_id),
                        KEY idx_shift_status (status),
                        CONSTRAINT fk_shift_staff
                            FOREIGN KEY (staff_id) REFERENCES staff_users(staff_id),
                        CONSTRAINT fk_shift_window
                            FOREIGN KEY (window_id) REFERENCES service_windows(window_id),
                        CONSTRAINT fk_shift_department
                            FOREIGN KEY (department_id) REFERENCES departments(department_id)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
                    """
                )
            )

        for username, department_id in (
            ("cashier1", 1),
            ("registrar1", 2),
            ("bookstore1", 3),
        ):
            connection.execute(
                text(
                    "UPDATE staff_users SET department_id = :department_id "
                    "WHERE username = :username"
                ),
                {"department_id": department_id, "username": username},
            )

        connection.commit()
        print("Staff shift migration completed successfully.")


if __name__ == "__main__":
    main()
