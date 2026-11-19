using System.Data;
using System.Security.Cryptography;
using MySqlConnector;
using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;
using static TicketRegistrationDesktop.Data.Db;

namespace TicketRegistrationDesktop.Data;

/// <summary>
/// Bookings, tickets, cancellations and check-in.
/// Concurrency (FR-14): every booking runs in ONE transaction that first locks the ticket-category row with
/// SELECT ... FOR UPDATE, then re-checks capacity / seats. Two clerks booking at the same moment are queued by MySQL,
/// and the unique key uq_event_seat on tickets is the last line of defence against selling a seat twice.
/// </summary>
public class BookingRepository
{
    private const string BookingSelect = @"
        SELECT b.id, b.reference, b.user_id, u.full_name AS customer, b.event_id, e.title, e.start_date,
               tc.name AS ticket_type, b.quantity, b.status, b.subtotal, b.discount, b.service_fee,
               b.total_amount, b.created_at
        FROM bookings b
        JOIN users u ON u.id = b.user_id
        JOIN events e ON e.id = b.event_id
        JOIN ticket_categories tc ON tc.id = b.ticket_category_id";

    private static Booking MapBooking(MySqlDataReader r) => new()
    {
        Id = r.Int("id"), Reference = r.Str("reference"), UserId = r.Int("user_id"), Customer = r.Str("customer"),
        EventId = r.Int("event_id"), EventTitle = r.Str("title"), EventStart = r.Dt("start_date"),
        TicketType = r.Str("ticket_type"), Quantity = r.Int("quantity"), Status = r.EnumOf<BookingStatus>("status"),
        Subtotal = r.Dec("subtotal"), Discount = r.Dec("discount"), ServiceFee = r.Dec("service_fee"),
        Total = r.Dec("total_amount"), BookedOn = r.Dt("created_at")
    };

    public Booking? Get(int id) => Query<Booking>(BookingSelect + " WHERE b.id=@i", MapBooking, P("@i", id)).FirstOrDefault();

    /// <summary>Attendees see their own bookings; with scopeAll an organizer sees bookings for own events and an admin sees everything.</summary>
    public List<Booking> List(User actor, bool scopeAll, string? q, BookingStatus? status)
    {
        var sql = BookingSelect + " WHERE 1=1";
        var ps = new List<MySqlParameter>();
        if (!scopeAll || actor.Role == UserRole.Attendee) { sql += " AND b.user_id=@u"; ps.Add(P("@u", actor.Id)); }
        else if (actor.Role == UserRole.Organizer) { sql += " AND e.organizer_id=@u"; ps.Add(P("@u", actor.Id)); }
        if (status.HasValue) { sql += " AND b.status=@s"; ps.Add(P("@s", status.Value.ToString())); }
        if (!string.IsNullOrWhiteSpace(q))
        {
            sql += " AND (b.reference LIKE @q OR u.full_name LIKE @q OR u.email LIKE @q OR e.title LIKE @q)";
            ps.Add(P("@q", "%" + q.Trim() + "%"));
        }
        return Query<Booking>(sql + " ORDER BY b.created_at DESC LIMIT 300", MapBooking, ps.ToArray());
    }

    public List<Ticket> Tickets(int bookingId) => Query<Ticket>(
        @"SELECT t.id, t.booking_id, b.reference, e.title, e.start_date, v.name AS venue, tc.name AS ticket_type,
                 s.section, s.row_label, s.seat_number, t.status, t.checked_in_at, t.code
          FROM tickets t
          JOIN bookings b ON b.id = t.booking_id
          JOIN events e ON e.id = t.event_id
          JOIN venues v ON v.id = e.venue_id
          JOIN ticket_categories tc ON tc.id = t.ticket_category_id
          LEFT JOIN seats s ON s.id = t.seat_id
          WHERE t.booking_id=@b ORDER BY t.id",
        r => new Ticket
        {
            Id = r.Int("id"), BookingId = r.Int("booking_id"), Reference = r.Str("reference"),
            EventTitle = r.Str("title"), EventStart = r.Dt("start_date"), Venue = r.Str("venue"),
            TicketType = r.Str("ticket_type"),
            Seat = r.StrN("section") is null ? "General admission" : $"{r.Str("section")} {r.Str("row_label")}{r.Int("seat_number")}",
            Status = r.EnumOf<TicketStatus>("status"), CheckedInAt = r.DtN("checked_in_at"), Code = r.Str("code")
        }, P("@b", bookingId));

    // ====================================================================== create booking
    public Result<Booking> Create(int userId, int eventId, int categoryId, int quantity,
                                  IReadOnlyList<int> seatIds, string? promoCode, PaymentMethod method)
    {
        using var c = Open();
        // READ COMMITTED: each statement sees what other buyers have already committed.
        using var tx = c.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // 1) lock the category row FIRST - concurrent buyers of this category wait here
            var cat = Query<(int Id, int EventId, decimal Price, int Quantity, string? Section)>(c, tx,
                "SELECT id, event_id, price, quantity, section FROM ticket_categories WHERE id=@id FOR UPDATE",
                r => (r.Int("id"), r.Int("event_id"), r.Dec("price"), r.Int("quantity"), r.StrN("section")),
                P("@id", categoryId)).FirstOrDefault();
            if (cat.Id == 0 || cat.EventId != eventId) return Result<Booking>.Fail("Ticket category not found.");

            var ev = Query<(string Status, DateTime Start, int VenueId, int Max)>(c, tx,
                "SELECT status, start_date, venue_id, max_tickets_per_booking FROM events WHERE id=@e",
                r => (r.Str("status"), r.Dt("start_date"), r.Int("venue_id"), r.Int("max_tickets_per_booking")),
                P("@e", eventId)).FirstOrDefault();
            if (ev.Status != "Published") return Result<Booking>.Fail("This event is not open for booking.");
            if (ev.Start <= DateTime.Now) return Result<Booking>.Fail("This event has already started.");
            if (quantity < 1) return Result<Booking>.Fail("Choose at least one ticket.");
            if (quantity > ev.Max) return Result<Booking>.Fail($"You can book at most {ev.Max} tickets at a time.");

            var seats = seatIds.Distinct().ToList();
            if (cat.Section is not null)
            {
                if (seats.Count != quantity) return Result<Booking>.Fail($"Please select exactly {quantity} seat(s).");
                var idList = string.Join(",", seats.Select(s => s.ToString()));     // ints only - safe to inline
                var valid = Scalar<long>(c, tx,
                    $"SELECT COUNT(*) FROM seats WHERE id IN ({idList}) AND venue_id=@v AND section=@s",
                    P("@v", ev.VenueId), P("@s", cat.Section));
                if (valid != seats.Count) return Result<Booking>.Fail("One or more seats are not valid for this ticket type.");
                var taken = Scalar<long>(c, tx,
                    $"SELECT COUNT(*) FROM tickets WHERE event_id=@e AND seat_lock IN ({idList})", P("@e", eventId));
                if (taken > 0) return Result<Booking>.Fail("One or more of the selected seats were just taken. Please choose again.");
            }
            else
            {
                var sold = Scalar<long>(c, tx,
                    "SELECT COUNT(*) FROM tickets WHERE ticket_category_id=@c AND status<>'Cancelled'", P("@c", categoryId));
                var left = cat.Quantity - (int)sold;
                if (quantity > left)
                    return Result<Booking>.Fail(left <= 0 ? "Sorry, this ticket type is sold out." : $"Only {left} ticket(s) left of this type.");
            }

            // 2) promo code (FR-18) and totals (FR-16)
            int? promoId = null; var percent = 0;
            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                var promo = Query<(int Id, int Percent)>(c, tx,
                    "SELECT id, discount_percent FROM promo_codes WHERE code=@c AND is_active=1 AND expiry_date >= CURDATE()",
                    r => (r.Int("id"), r.Int("discount_percent")), P("@c", promoCode.Trim())).FirstOrDefault();
                if (promo.Id == 0) return Result<Booking>.Fail("This promo code is invalid or has expired.");
                promoId = promo.Id; percent = promo.Percent;
            }
            var q = Pricing.Quote(cat.Price, quantity, percent);

            // 3) booking + tickets + payment, all or nothing
            var reference = "TRS-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            var bookingId = (int)Insert(c, tx,
                @"INSERT INTO bookings (reference, user_id, event_id, ticket_category_id, quantity, status, subtotal, discount,
                                        service_fee, total_amount, promo_code_id)
                  VALUES (@r,@u,@e,@c,@q,'Confirmed',@sub,@dis,@fee,@tot,@p)",
                P("@r", reference), P("@u", userId), P("@e", eventId), P("@c", categoryId), P("@q", quantity),
                P("@sub", q.Subtotal), P("@dis", q.Discount), P("@fee", q.Fee), P("@tot", q.Total), P("@p", promoId));

            for (var i = 0; i < quantity; i++)
            {
                int? seat = cat.Section is null ? null : seats[i];
                Insert(c, tx,
                    @"INSERT INTO tickets (booking_id, event_id, ticket_category_id, seat_id, seat_lock, code, status)
                      VALUES (@b,@e,@c,@s,@s,@code,'Valid')",
                    P("@b", bookingId), P("@e", eventId), P("@c", categoryId), P("@s", seat), P("@code", TicketTokens.NewCode()));
            }

            Insert(c, tx, "INSERT INTO payments (booking_id, method, amount, status, reference) VALUES (@b,@m,@a,'Paid',@ref)",
                P("@b", bookingId), P("@m", method.ToString()), P("@a", q.Total),
                P("@ref", "PAY-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()));

            tx.Commit();
            return Result<Booking>.Success(Get(bookingId)!);
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            return Result<Booking>.Fail("Those seats were just taken by another buyer. Please choose again.");
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.LockDeadlock || ex.ErrorCode == MySqlErrorCode.LockWaitTimeout)
        {
            return Result<Booking>.Fail("The system is busy. Please try again.");
        }
    }

    // ====================================================================== cancel / refund (FR-24, FR-26, FR-34)
    /// <param name="refund">true = refund because the event was cancelled; false = normal cancellation.</param>
    public Result CancelOrRefund(int bookingId, User actor, bool refund)
    {
        using var c = Open();
        using var tx = c.BeginTransaction(IsolationLevel.ReadCommitted);

        var b = Query<(int UserId, string Status, DateTime Start, int Hours, string EventStatus)>(c, tx,
            @"SELECT b.user_id, b.status, e.start_date, e.cancellation_deadline_hours, e.status AS ev_status
              FROM bookings b JOIN events e ON e.id = b.event_id WHERE b.id=@i FOR UPDATE",
            r => (r.Int("user_id"), r.Str("status"), r.Dt("start_date"), r.Int("cancellation_deadline_hours"), r.Str("ev_status")),
            P("@i", bookingId)).FirstOrDefault();

        if (b.Status is null) return Result.Fail("Booking not found.");
        var isAdmin = actor.Role == UserRole.Admin;
        if (!isAdmin && b.UserId != actor.Id) return Result.Fail("You can only change your own bookings.");
        if (b.Status != "Confirmed") return Result.Fail("Only confirmed bookings can be cancelled or refunded.");
        if (Scalar<long>(c, tx, "SELECT COUNT(*) FROM tickets WHERE booking_id=@i AND status='Used'", P("@i", bookingId)) > 0)
            return Result.Fail("A ticket from this booking has already been used.");

        if (refund)
        {
            if (b.EventStatus != "Cancelled" && !isAdmin) return Result.Fail("Refunds are only available when the event was cancelled.");
        }
        else if (!isAdmin && DateTime.Now > b.Start.AddHours(-b.Hours))
            return Result.Fail("The cancellation deadline for this event has passed.");

        Execute(c, tx, "UPDATE tickets SET status='Cancelled', seat_lock=NULL WHERE booking_id=@i", P("@i", bookingId));   // frees the seats
        Execute(c, tx, "UPDATE bookings SET status=@s WHERE id=@i", P("@s", refund ? "Refunded" : "Cancelled"), P("@i", bookingId));
        Execute(c, tx, "UPDATE payments SET status='Refunded' WHERE booking_id=@i", P("@i", bookingId));
        tx.Commit();
        return Result.Success();
    }

    // ====================================================================== check-in (FR-25)
    public CheckInResult CheckIn(string code, User actor, int? eventId)
    {
        var t = Query<(int Id, string Status, int EventId, int OrganizerId, string Title, string EventStatus, string BookingStatus,
                       string Holder, string TicketType, string? Section, string? Row, int? Number, DateTime? At)>(
            @"SELECT t.id, t.status, t.event_id, e.organizer_id, e.title, e.status AS ev_status, b.status AS b_status,
                     u.full_name, tc.name AS tc_name, s.section, s.row_label, s.seat_number, t.checked_in_at
              FROM tickets t
              JOIN events e ON e.id = t.event_id
              JOIN bookings b ON b.id = t.booking_id
              JOIN users u ON u.id = b.user_id
              JOIN ticket_categories tc ON tc.id = t.ticket_category_id
              LEFT JOIN seats s ON s.id = t.seat_id
              WHERE t.code=@c",
            r => (r.Int("id"), r.Str("status"), r.Int("event_id"), r.Int("organizer_id"), r.Str("title"), r.Str("ev_status"),
                  r.Str("b_status"), r.Str("full_name"), r.Str("tc_name"), r.StrN("section"), r.StrN("row_label"),
                  r.IntN("seat_number"), r.DtN("checked_in_at")),
            P("@c", code)).FirstOrDefault();

        if (t.Status is null) return new CheckInResult(CheckInStatus.Invalid, "Ticket not found.");
        if (actor.Role != UserRole.Admin && t.OrganizerId != actor.Id)
            return new CheckInResult(CheckInStatus.Forbidden, "This ticket belongs to an event you do not manage.");
        if (eventId.HasValue && t.EventId != eventId.Value)
            return new CheckInResult(CheckInStatus.WrongEvent, $"This ticket is for a different event: {t.Title}.");

        var detail = t.TicketType + (t.Section is null ? "" : $" - {t.Section} {t.Row}{t.Number}");
        if (t.Status == "Cancelled" || t.BookingStatus != "Confirmed" || t.EventStatus == "Cancelled")
            return new CheckInResult(CheckInStatus.Cancelled, "This ticket was cancelled or refunded.", t.Holder, detail);
        if (t.Status == "Used")
            return new CheckInResult(CheckInStatus.AlreadyUsed, $"Already used at {t.At:HH:mm 'on' dd MMM}.", t.Holder, detail);

        // atomic: only one scan can flip Valid -> Used
        var rows = Execute("UPDATE tickets SET status='Used', checked_in_at=NOW() WHERE id=@i AND status='Valid'", P("@i", t.Id));
        return rows == 0
            ? new CheckInResult(CheckInStatus.AlreadyUsed, "Already used (scanned a moment ago).", t.Holder, detail)
            : new CheckInResult(CheckInStatus.Valid, "Valid ticket. Welcome!", t.Holder, detail);
    }
}
