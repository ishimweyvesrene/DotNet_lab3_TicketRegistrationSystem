using MySqlConnector;
using TicketRegistrationDesktop.Models;
using static TicketRegistrationDesktop.Data.Db;

namespace TicketRegistrationDesktop.Data;

public class ReportRepository
{
    /// <summary>FR-40: confirmed sales by event for a date range, optionally one event / category.
    /// Organizers only see their own events.</summary>
    public List<SalesRow> Sales(User actor, DateTime? from, DateTime? to, int? eventId, int? categoryId)
    {
        var sql = @"SELECT e.title, c.name AS category, COUNT(*) AS bookings, SUM(b.quantity) AS tickets, SUM(b.total_amount) AS revenue
                    FROM bookings b
                    JOIN events e ON e.id = b.event_id
                    JOIN categories c ON c.id = e.category_id
                    WHERE b.status='Confirmed'";
        var ps = new List<MySqlParameter>();
        if (from.HasValue) { sql += " AND b.created_at >= @from"; ps.Add(P("@from", from.Value.Date)); }
        if (to.HasValue) { sql += " AND b.created_at < @to"; ps.Add(P("@to", to.Value.Date.AddDays(1))); }
        if (eventId.HasValue) { sql += " AND e.id=@e"; ps.Add(P("@e", eventId.Value)); }
        if (categoryId.HasValue) { sql += " AND e.category_id=@c"; ps.Add(P("@c", categoryId.Value)); }
        if (actor.Role == UserRole.Organizer) { sql += " AND e.organizer_id=@o"; ps.Add(P("@o", actor.Id)); }
        sql += " GROUP BY e.id, e.title, c.name ORDER BY revenue DESC";

        return Query<SalesRow>(sql, r => new SalesRow
        {
            Event = r.Str("title"), Category = r.Str("category"),
            Bookings = r.Int("bookings"), Tickets = r.Int("tickets"), Revenue = r.Dec("revenue")
        }, ps.ToArray());
    }

    /// <summary>FR-39: dashboard numbers.</summary>
    public Totals Totals(User actor)
    {
        var own = actor.Role == UserRole.Organizer;
        MySqlParameter o() => P("@o", actor.Id);   // a fresh parameter for every command
        return new Totals
        {
            Sales = Scalar<decimal>($@"SELECT COALESCE(SUM(b.total_amount),0) FROM bookings b JOIN events e ON e.id=b.event_id
                                       WHERE b.status='Confirmed' {(own ? "AND e.organizer_id=@o" : "")}", o()),
            Bookings = (int)Scalar<long>($@"SELECT COUNT(*) FROM bookings b JOIN events e ON e.id=b.event_id
                                            WHERE b.status='Confirmed' {(own ? "AND e.organizer_id=@o" : "")}", o()),
            TicketsSold = (int)Scalar<long>($@"SELECT COUNT(*) FROM tickets t JOIN events e ON e.id=t.event_id
                                               WHERE t.status<>'Cancelled' {(own ? "AND e.organizer_id=@o" : "")}", o()),
            ActiveEvents = (int)Scalar<long>($@"SELECT COUNT(*) FROM events e WHERE e.status='Published' AND e.end_date >= NOW()
                                                {(own ? "AND e.organizer_id=@o" : "")}", o()),
            Users = (int)Scalar<long>("SELECT COUNT(*) FROM users")
        };
    }
}
