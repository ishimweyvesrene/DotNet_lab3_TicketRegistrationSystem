-- Ticket Registration System (desktop) - MySQL 8 schema.
-- The application runs this script automatically on start-up. You can also run it in MySQL Workbench.

CREATE DATABASE IF NOT EXISTS ticket_registration CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE ticket_registration;

CREATE TABLE IF NOT EXISTS users (
  id            INT AUTO_INCREMENT PRIMARY KEY,
  full_name     VARCHAR(120) NOT NULL,
  email         VARCHAR(190) NOT NULL,
  phone         VARCHAR(30)  NOT NULL,
  password_hash VARCHAR(100) NOT NULL,
  role          ENUM('Admin','Organizer','Attendee') NOT NULL DEFAULT 'Attendee',
  is_active     TINYINT(1)   NOT NULL DEFAULT 1,
  created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_users_email (email)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS venues (
  id       INT AUTO_INCREMENT PRIMARY KEY,
  name     VARCHAR(150) NOT NULL,
  city     VARCHAR(100) NOT NULL,
  address  VARCHAR(250) NULL,
  capacity INT NOT NULL
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS seats (
  id          INT AUTO_INCREMENT PRIMARY KEY,
  venue_id    INT NOT NULL,
  section     VARCHAR(50) NOT NULL,
  row_label   VARCHAR(10) NOT NULL,
  seat_number INT NOT NULL,
  UNIQUE KEY uq_seat (venue_id, section, row_label, seat_number),
  CONSTRAINT fk_seats_venue FOREIGN KEY (venue_id) REFERENCES venues(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS categories (
  id   INT AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(80) NOT NULL,
  UNIQUE KEY uq_categories_name (name)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS events (
  id                           INT AUTO_INCREMENT PRIMARY KEY,
  title                        VARCHAR(200) NOT NULL,
  description                  TEXT NOT NULL,
  start_date                   DATETIME NOT NULL,
  end_date                     DATETIME NOT NULL,
  venue_id                     INT NOT NULL,
  category_id                  INT NOT NULL,
  organizer_id                 INT NOT NULL,
  status                       ENUM('Pending','Published','Cancelled') NOT NULL DEFAULT 'Pending',
  max_tickets_per_booking      INT NOT NULL DEFAULT 6,
  cancellation_deadline_hours  INT NOT NULL DEFAULT 24,
  created_at                   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_events_venue     FOREIGN KEY (venue_id)     REFERENCES venues(id),
  CONSTRAINT fk_events_category  FOREIGN KEY (category_id)  REFERENCES categories(id),
  CONSTRAINT fk_events_organizer FOREIGN KEY (organizer_id) REFERENCES users(id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS ticket_categories (
  id       INT AUTO_INCREMENT PRIMARY KEY,
  event_id INT NOT NULL,
  name     VARCHAR(80) NOT NULL,
  price    DECIMAL(12,2) NOT NULL,
  quantity INT NOT NULL,
  section  VARCHAR(50) NULL,           -- NULL = general admission, otherwise numbered seats of that venue section
  CONSTRAINT fk_tc_event FOREIGN KEY (event_id) REFERENCES events(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS promo_codes (
  id               INT AUTO_INCREMENT PRIMARY KEY,
  code             VARCHAR(40) NOT NULL,
  discount_percent INT NOT NULL,
  expiry_date      DATE NOT NULL,
  is_active        TINYINT(1) NOT NULL DEFAULT 1,
  UNIQUE KEY uq_promo_code (code)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS bookings (
  id                 INT AUTO_INCREMENT PRIMARY KEY,
  reference          VARCHAR(20) NOT NULL,
  user_id            INT NOT NULL,
  event_id           INT NOT NULL,
  ticket_category_id INT NOT NULL,
  quantity           INT NOT NULL,
  status             ENUM('Confirmed','Cancelled','Refunded') NOT NULL DEFAULT 'Confirmed',
  subtotal           DECIMAL(12,2) NOT NULL,
  discount           DECIMAL(12,2) NOT NULL DEFAULT 0,
  service_fee        DECIMAL(12,2) NOT NULL DEFAULT 0,
  total_amount       DECIMAL(12,2) NOT NULL,
  promo_code_id      INT NULL,
  created_at         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_booking_reference (reference),
  CONSTRAINT fk_b_user  FOREIGN KEY (user_id)            REFERENCES users(id),
  CONSTRAINT fk_b_event FOREIGN KEY (event_id)           REFERENCES events(id),
  CONSTRAINT fk_b_tc    FOREIGN KEY (ticket_category_id) REFERENCES ticket_categories(id),
  CONSTRAINT fk_b_promo FOREIGN KEY (promo_code_id)      REFERENCES promo_codes(id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS tickets (
  id                 INT AUTO_INCREMENT PRIMARY KEY,
  booking_id         INT NOT NULL,
  event_id           INT NOT NULL,
  ticket_category_id INT NOT NULL,
  seat_id            INT NULL,
  seat_lock          INT NULL,          -- = seat_id while the ticket is active, NULL once cancelled
  code               VARCHAR(40) NOT NULL,
  status             ENUM('Valid','Used','Cancelled') NOT NULL DEFAULT 'Valid',
  checked_in_at      DATETIME NULL,
  UNIQUE KEY uq_ticket_code (code),
  -- a seat can have only ONE active ticket per event: the database itself refuses a double sale
  UNIQUE KEY uq_event_seat (event_id, seat_lock),
  CONSTRAINT fk_t_booking FOREIGN KEY (booking_id)         REFERENCES bookings(id) ON DELETE CASCADE,
  CONSTRAINT fk_t_event   FOREIGN KEY (event_id)           REFERENCES events(id),
  CONSTRAINT fk_t_tc      FOREIGN KEY (ticket_category_id) REFERENCES ticket_categories(id),
  CONSTRAINT fk_t_seat    FOREIGN KEY (seat_id)            REFERENCES seats(id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS payments (
  id         INT AUTO_INCREMENT PRIMARY KEY,
  booking_id INT NOT NULL,
  method     ENUM('Cash','Card','MobileMoney') NOT NULL,
  amount     DECIMAL(12,2) NOT NULL,
  status     ENUM('Paid','Refunded') NOT NULL DEFAULT 'Paid',
  reference  VARCHAR(40) NOT NULL,
  paid_on    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_payment_ref (reference),
  CONSTRAINT fk_p_booking FOREIGN KEY (booking_id) REFERENCES bookings(id) ON DELETE CASCADE
) ENGINE=InnoDB;
