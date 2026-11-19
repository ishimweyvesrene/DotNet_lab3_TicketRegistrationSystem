# Ticket Registration System - Windows Forms + MySQL

Desktop version of the Ticket Registration System (SRS: `SRS_Ticket_Registration_System.pdf`).
C# / .NET 8 Windows Forms, MySQL 8, **MVC architecture**. Everything is done through the GUI; no SQL needed.

## Run it (Windows)
1. Install the **.NET 8 SDK** and make sure the **MySQL** service is running.
2. Open `TicketRegistrationDesktop.sln` in Visual Studio 2022 and press **F5** (or `dotnet run`).
3. `appsettings.json` already holds your connection settings (`Host=localhost`, `Port=3306`, `User=root`,
   your password). Change `User` if you use another MySQL account.
4. On first start the app **creates the `ticket_registration` database and all tables itself**
   (`Database/schema.sql`) and seeds an admin account. Nothing to import by hand.

| Role      | Email               | Password        |
|-----------|---------------------|-----------------|
| Admin     | admin@trs.local     | Admin@12345     |
| Organizer | organizer@trs.local | Organizer@12345 |

The organizer, a demo venue with 24 VIP seats, the event "Kigali Jazz Night" and the promo code `WELCOME10` are
created only when `SeedDemoData` is `true`. New people register themselves as Attendees; an admin promotes them.

> `appsettings.json` contains your database password, so it is listed in `.gitignore`. Commit
> `appsettings.example.json` instead and never push the real file to GitHub.

## MVC structure
```
Models/        entities and results (User, Event, Booking, Ticket, ...)           <- M
Data/          MySQL access: Db helper, repositories, schema initialiser            <- M (persistence)
Controllers/   Auth, Event, Booking, CheckIn, Admin, Report: validation, rules,
               permissions; the only layer the views talk to                        <- C
Views/         Windows Forms screens (Login, Events, Booking, MyBookings, ...)      <- V
Services/      settings, session, password hashing, signed QR tokens, pricing
```
Flow: **View -> Controller -> Repository -> MySQL**, and results come back as models. Views contain no SQL and no
business rules; controllers contain no controls.

## Screens (tabs by role)
| Tab | Who | What |
|-----|-----|------|
| Events | all | search, filter by category / city / price, sort, details and ticket types |
| Book tickets | all | pick event + ticket type, quantity or numbered seats, promo code, payment method, live total |
| My bookings | all | history, tickets with QR (save as PNG), cancel before the deadline, refund for cancelled events |
| Manage events | organizer, admin | create / edit / cancel events, define ticket types, admin approves or removes |
| Check-in | organizer, admin | scan or type a ticket code; valid / already used / cancelled / wrong event |
| Reports | organizer, admin | totals, sales by event and category, export CSV (opens in Excel) |
| Venues & seats | admin | venues, numbered seat sections |
| Categories & promos | admin | event categories, promo codes |
| Users | admin | roles, activate / deactivate |

## Database and double-booking protection
Tables: `users, venues, seats, categories, events, ticket_categories, promo_codes, bookings, tickets, payments`
(InnoDB, foreign keys, utf8mb4). Booking one or many tickets is **one transaction**:
1. `SELECT ... FOR UPDATE` locks the ticket-type row, so two clerks booking the same type are queued by MySQL;
2. capacity or the chosen seats are re-checked under that lock;
3. booking, tickets and payment are inserted together, or nothing is;
4. the unique key `uq_event_seat (event_id, seat_lock)` makes MySQL itself refuse a second ticket for the same seat
   (`seat_lock` is the seat id while a ticket is active and NULL once cancelled).
Check-in flips `Valid -> Used` with one conditional UPDATE, so two scanners cannot both succeed.
Passwords are stored as BCrypt hashes, all SQL is parameterised, QR codes carry an HMAC signature.

## Differences from the web version
* No separate "hold" step: a desktop clerk books and pays in one atomic action.
* Payment is recorded (Cash / Card / Mobile money) with no online gateway.
* No emails; reports export to CSV instead of PDF / Excel; the QR can be saved as an image.
* Check-in uses typed or USB/Bluetooth-scanner input (the scanner types the code and presses Enter); no webcam scanning.

## Status
Written without a Windows machine or NuGet access, so it has **not been compiled or run yet**. Expect to fix a few
small compile errors on the first build.
