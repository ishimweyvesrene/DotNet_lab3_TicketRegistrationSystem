using MySqlConnector;
using TicketRegistrationDesktop.Models;
using static TicketRegistrationDesktop.Data.Db;

namespace TicketRegistrationDesktop.Data;

public class EventRepository
{
    private const string Select = @"
        SELECT e.id, e.title, e.description, e.start_date, e.end_date, e.venue_id, v.name AS venue_name, v.city,
               e.category_id, c.name AS category_name, e.organizer_id, u.full_name AS organizer_name, e.status,
               e.max_tickets_per_booking, e.cancellation_deadline_hours,
               (SELECT MIN(tc.price) FROM ticket_categories tc WHERE tc.event_id = e.id) AS from_price,
               (SELECT COUNT(*) FROM tickets t WHERE t.event_id = e.id AND t.status <> 'Cancelled') AS sold
        FROM events e
        JOIN venues v ON v.id = e.venue_id
        JOIN categories c ON c.id = e.category_id
        JOIN users u ON u.id = e.organizer_id";

    private static Event Map(MySqlDataReader r) => new()
    {
        Id = r.Int("id"), Title = r.Str("title"), Description = r.Str("description"),
        StartDate = r.Dt("start_date"), EndDate = r.Dt("end_date"),
        VenueId = r.Int("venue_id"), VenueName = r.Str("venue_name"), City = r.Str("city"),
        CategoryId = r.Int("category_id"), CategoryName = r.Str("category_name"),
        OrganizerId = r.Int("organizer_id"), OrganizerName = r.Str("organizer_name"),
        Status = r.EnumOf<EventStatus>("status"),
        MaxTicketsPerBooking = r.Int("max_tickets_per_booking"),
        CancellationDeadlineHours = r.Int("cancellation_deadline_hours"),
        FromPrice = r.DecN("from_price"), Sold = r.Int("sold")
    };

    /// <summary>FR-6..9: published, upcoming events with search, filter and sort.</summary>
    public List<Event> Browse(string? q, int? categoryId, string? city, DateTime? from, DateTime? to,
                              decimal? minPrice, decimal? maxPrice, string sort)
    {
        var sql = Select + " WHERE e.status='Published' AND e.end_date >= NOW()";
        var ps = new List<MySqlParameter>();
        if (!string.IsNullOrWhiteSpace(q))
        {
            sql += " AND (e.title LIKE @q OR e.description LIKE @q OR v.name LIKE @q OR c.name LIKE @q)";
            ps.Add(P("@q", "%" + q.Trim() + "%"));
        }
        if (categoryId.HasValue) { sql += " AND e.category_id=@cat"; ps.Add(P("@cat", categoryId.Value)); }
        if (!string.IsNullOrWhiteSpace(city)) { sql += " AND v.city=@city"; ps.Add(P("@city", city)); }
        if (from.HasValue) { sql += " AND e.start_date >= @from"; ps.Add(P("@from", from.Value.Date)); }
        if (to.HasValue) { sql += " AND e.start_date < @to"; ps.Add(P("@to", to.Value.Date.AddDays(1))); }
        if (minPrice.HasValue) { sql += " AND EXISTS (SELECT 1 FROM ticket_categories x WHERE x.event_id=e.id AND x.price >= @minp)"; ps.Add(P("@minp", minPrice.Value)); }
        if (maxPrice.HasValue) { sql += " AND EXISTS (SELECT 1 FROM ticket_categories x WHERE x.event_id=e.id AND x.price <= @maxp)"; ps.Add(P("@maxp", maxPrice.Value)); }
        sql += sort switch
        {
            "price" => " ORDER BY from_price IS NULL, from_price, e.start_date",
            "popular" => " ORDER BY sold DESC, e.start_date",
            _ => " ORDER BY e.start_date"
        };
        return Query<Event>(sql, Map, ps.ToArray());
    }

    public List<string> Cities() => Query<string>("SELECT DISTINCT city FROM venues ORDER BY city", r => r.Str("city"));

    /// <summary>Organizers see their own events, admins see all (FR-28, FR-33).</summary>
    public List<Event> ForManagement(User actor) => actor.Role == UserRole.Admin
        ? Query<Event>(Select + " ORDER BY e.start_date DESC", Map)
        : Query<Event>(Select + " WHERE e.organizer_id=@o ORDER BY e.start_date DESC", Map, P("@o", actor.Id));

    public Event? Get(int id) => Query<Event>(Select + " WHERE e.id=@i", Map, P("@i", id)).FirstOrDefault();

    public int Save(Event e)
    {
        if (e.Id == 0)
        {
            e.Id = (int)Insert(
                @"INSERT INTO events (title, description, start_date, end_date, venue_id, category_id, organizer_id, status,
                                      max_tickets_per_booking, cancellation_deadline_hours)
                  VALUES (@t,@d,@s,@e,@v,@c,@o,@st,@m,@h)",
                P("@t", e.Title), P("@d", e.Description), P("@s", e.StartDate), P("@e", e.EndDate),
                P("@v", e.VenueId), P("@c", e.CategoryId), P("@o", e.OrganizerId), P("@st", e.Status.ToString()),
                P("@m", e.MaxTicketsPerBooking), P("@h", e.CancellationDeadlineHours));
        }
        else
        {
            Execute(
                @"UPDATE events SET title=@t, description=@d, start_date=@s, end_date=@e, venue_id=@v, category_id=@c,
                                    max_tickets_per_booking=@m, cancellation_deadline_hours=@h
                  WHERE id=@i",
                P("@t", e.Title), P("@d", e.Description), P("@s", e.StartDate), P("@e", e.EndDate),
                P("@v", e.VenueId), P("@c", e.CategoryId), P("@m", e.MaxTicketsPerBooking),
                P("@h", e.CancellationDeadlineHours), P("@i", e.Id));
        }
        return e.Id;
    }

    public void SetStatus(int id, EventStatus status) =>
        Execute("UPDATE events SET status=@s WHERE id=@i", P("@s", status.ToString()), P("@i", id));

    public bool HasBookings(int eventId) =>
        Scalar<long>("SELECT COUNT(*) FROM bookings WHERE event_id=@e", P("@e", eventId)) > 0;

    public void Delete(int eventId) => Execute("DELETE FROM events WHERE id=@e", P("@e", eventId));

    // ------------------------------------------------------------ ticket categories (FR-29)
    public List<TicketCategory> TicketCategories(int eventId) => Query<TicketCategory>(
        @"SELECT tc.id, tc.event_id, tc.name, tc.price, tc.quantity, tc.section,
                 (SELECT COUNT(*) FROM tickets t WHERE t.ticket_category_id = tc.id AND t.status <> 'Cancelled') AS sold
          FROM ticket_categories tc WHERE tc.event_id=@e ORDER BY tc.price",
        r => new TicketCategory
        {
            Id = r.Int("id"), EventId = r.Int("event_id"), Name = r.Str("name"), Price = r.Dec("price"),
            Quantity = r.Int("quantity"), Section = r.StrN("section"), Sold = r.Int("sold")
        }, P("@e", eventId));

    public int SeatCount(int venueId, string section) =>
        (int)Scalar<long>("SELECT COUNT(*) FROM seats WHERE venue_id=@v AND section=@s", P("@v", venueId), P("@s", section));

    public bool SectionUsed(int eventId, string section, int excludeId) =>
        Scalar<long>("SELECT COUNT(*) FROM ticket_categories WHERE event_id=@e AND section=@s AND id<>@x",
            P("@e", eventId), P("@s", section), P("@x", excludeId)) > 0;

    public int OtherQuantity(int eventId, int excludeId) =>
        (int)(Scalar<decimal>("SELECT COALESCE(SUM(quantity),0) FROM ticket_categories WHERE event_id=@e AND id<>@x",
            P("@e", eventId), P("@x", excludeId)));

    public bool CategoryHasBookings(int categoryId) =>
        Scalar<long>("SELECT COUNT(*) FROM bookings WHERE ticket_category_id=@c", P("@c", categoryId)) > 0;

    public void SaveTicketCategory(TicketCategory t)
    {
        if (t.Id == 0)
            t.Id = (int)Insert("INSERT INTO ticket_categories (event_id, name, price, quantity, section) VALUES (@e,@n,@p,@q,@s)",
                P("@e", t.EventId), P("@n", t.Name), P("@p", t.Price), P("@q", t.Quantity), P("@s", t.Section));
        else
            Execute("UPDATE ticket_categories SET name=@n, price=@p, quantity=@q, section=@s WHERE id=@i",
                P("@n", t.Name), P("@p", t.Price), P("@q", t.Quantity), P("@s", t.Section), P("@i", t.Id));
    }

    public void DeleteTicketCategory(int id) => Execute("DELETE FROM ticket_categories WHERE id=@i", P("@i", id));

    /// <summary>Seats of the category's section that have no active ticket for this event.</summary>
    public List<Seat> AvailableSeats(int eventId, int ticketCategoryId) => Query<Seat>(
        @"SELECT s.id, s.venue_id, s.section, s.row_label, s.seat_number
          FROM seats s
          JOIN events e ON e.venue_id = s.venue_id AND e.id = @e
          JOIN ticket_categories tc ON tc.id = @tc AND tc.event_id = e.id AND tc.section = s.section
          WHERE s.id NOT IN (SELECT t.seat_lock FROM tickets t WHERE t.event_id = @e AND t.seat_lock IS NOT NULL)
          ORDER BY LENGTH(s.row_label), s.row_label, s.seat_number",
        r => new Seat { Id = r.Int("id"), VenueId = r.Int("venue_id"), Section = r.Str("section"),
                        RowLabel = r.Str("row_label"), SeatNumber = r.Int("seat_number") },
        P("@e", eventId), P("@tc", ticketCategoryId));
}
