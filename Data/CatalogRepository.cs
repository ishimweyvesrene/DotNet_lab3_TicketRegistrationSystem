using MySqlConnector;
using TicketRegistrationDesktop.Models;
using static TicketRegistrationDesktop.Data.Db;

namespace TicketRegistrationDesktop.Data;

/// <summary>Venues + seat layouts, event categories and promo codes.</summary>
public class CatalogRepository
{
    // ------------------------------------------------------------ venues
    public List<Venue> Venues() => Query<Venue>(
        @"SELECT v.id, v.name, v.city, v.address, v.capacity,
                 (SELECT COUNT(*) FROM seats s WHERE s.venue_id = v.id) AS seat_count
          FROM venues v ORDER BY v.name",
        r => new Venue { Id = r.Int("id"), Name = r.Str("name"), City = r.Str("city"), Address = r.StrN("address"),
                         Capacity = r.Int("capacity"), SeatCount = r.Int("seat_count") });

    public void SaveVenue(Venue v)
    {
        if (v.Id == 0)
            v.Id = (int)Insert("INSERT INTO venues (name, city, address, capacity) VALUES (@n,@c,@a,@cap)",
                P("@n", v.Name), P("@c", v.City), P("@a", v.Address), P("@cap", v.Capacity));
        else
            Execute("UPDATE venues SET name=@n, city=@c, address=@a, capacity=@cap WHERE id=@i",
                P("@n", v.Name), P("@c", v.City), P("@a", v.Address), P("@cap", v.Capacity), P("@i", v.Id));
    }

    public Result DeleteVenue(int id)
    {
        if (Scalar<long>("SELECT COUNT(*) FROM events WHERE venue_id=@v", P("@v", id)) > 0)
            return Result.Fail("Events use this venue, so it cannot be deleted.");
        Execute("DELETE FROM venues WHERE id=@v", P("@v", id));
        return Result.Success();
    }

    public List<SectionInfo> Sections(int venueId) => Query<SectionInfo>(
        @"SELECT section, COUNT(DISTINCT row_label) AS rws, COUNT(*) AS seats
          FROM seats WHERE venue_id=@v GROUP BY section ORDER BY section",
        r => new SectionInfo { Section = r.Str("section"), Rows = r.Int("rws"), Seats = r.Int("seats") }, P("@v", venueId));

    public int GenerateSeats(int venueId, string section, int rows, int seatsPerRow)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        var added = 0;
        for (var r = 0; r < rows; r++)
            for (var n = 1; n <= seatsPerRow; n++)
                added += Execute(c, tx,
                    "INSERT IGNORE INTO seats (venue_id, section, row_label, seat_number) VALUES (@v,@s,@r,@n)",
                    P("@v", venueId), P("@s", section), P("@r", RowName(r)), P("@n", n));
        tx.Commit();
        return added;
    }

    public Result RemoveSection(int venueId, string section)
    {
        if (Scalar<long>(@"SELECT COUNT(*) FROM ticket_categories tc JOIN events e ON e.id=tc.event_id
                           WHERE e.venue_id=@v AND tc.section=@s", P("@v", venueId), P("@s", section)) > 0)
            return Result.Fail("A ticket category uses this section, so it cannot be removed.");
        if (Scalar<long>(@"SELECT COUNT(*) FROM tickets t JOIN seats s ON s.id=t.seat_id
                           WHERE s.venue_id=@v AND s.section=@s", P("@v", venueId), P("@s", section)) > 0)
            return Result.Fail("Tickets exist for seats in this section.");
        Execute("DELETE FROM seats WHERE venue_id=@v AND section=@s", P("@v", venueId), P("@s", section));
        return Result.Success();
    }

    public static string RowName(int i) => i < 26 ? ((char)('A' + i)).ToString() : RowName(i / 26 - 1) + (char)('A' + i % 26);

    // ------------------------------------------------------------ event categories
    public List<EventCategory> Categories() => Query<EventCategory>(
        "SELECT id, name FROM categories ORDER BY name", r => new EventCategory { Id = r.Int("id"), Name = r.Str("name") });

    public Result SaveCategory(EventCategory c)
    {
        var dup = Scalar<long>("SELECT COUNT(*) FROM categories WHERE name=@n AND id<>@i", P("@n", c.Name), P("@i", c.Id));
        if (dup > 0) return Result.Fail("That category already exists.");
        if (c.Id == 0) c.Id = (int)Insert("INSERT INTO categories (name) VALUES (@n)", P("@n", c.Name));
        else Execute("UPDATE categories SET name=@n WHERE id=@i", P("@n", c.Name), P("@i", c.Id));
        return Result.Success();
    }

    public Result DeleteCategory(int id)
    {
        if (Scalar<long>("SELECT COUNT(*) FROM events WHERE category_id=@c", P("@c", id)) > 0)
            return Result.Fail("Events use this category, so it cannot be deleted.");
        Execute("DELETE FROM categories WHERE id=@c", P("@c", id));
        return Result.Success();
    }

    // ------------------------------------------------------------ promo codes
    private static PromoCode MapPromo(MySqlDataReader r) => new()
    {
        Id = r.Int("id"), Code = r.Str("code"), DiscountPercent = r.Int("discount_percent"),
        ExpiryDate = r.Dt("expiry_date"), IsActive = r.Bool("is_active")
    };

    public List<PromoCode> Promos() =>
        Query<PromoCode>("SELECT id, code, discount_percent, expiry_date, is_active FROM promo_codes ORDER BY expiry_date DESC", MapPromo);

    public PromoCode? FindActivePromo(string code) =>
        Query<PromoCode>(@"SELECT id, code, discount_percent, expiry_date, is_active FROM promo_codes
                           WHERE code=@c AND is_active=1 AND expiry_date >= CURDATE()", MapPromo, P("@c", code)).FirstOrDefault();

    public Result SavePromo(PromoCode p)
    {
        if (Scalar<long>("SELECT COUNT(*) FROM promo_codes WHERE code=@c AND id<>@i", P("@c", p.Code), P("@i", p.Id)) > 0)
            return Result.Fail("That code already exists.");
        if (p.Id == 0)
            p.Id = (int)Insert("INSERT INTO promo_codes (code, discount_percent, expiry_date, is_active) VALUES (@c,@d,@x,@a)",
                P("@c", p.Code), P("@d", p.DiscountPercent), P("@x", p.ExpiryDate.Date), P("@a", p.IsActive ? 1 : 0));
        else
            Execute("UPDATE promo_codes SET code=@c, discount_percent=@d, expiry_date=@x, is_active=@a WHERE id=@i",
                P("@c", p.Code), P("@d", p.DiscountPercent), P("@x", p.ExpiryDate.Date), P("@a", p.IsActive ? 1 : 0), P("@i", p.Id));
        return Result.Success();
    }

    /// <summary>Deletes an unused code; a code that was used in bookings is only deactivated.</summary>
    public string DeletePromo(int id)
    {
        if (Scalar<long>("SELECT COUNT(*) FROM bookings WHERE promo_code_id=@i", P("@i", id)) > 0)
        {
            Execute("UPDATE promo_codes SET is_active=0 WHERE id=@i", P("@i", id));
            return "The code was used before, so it was deactivated instead of deleted.";
        }
        Execute("DELETE FROM promo_codes WHERE id=@i", P("@i", id));
        return "Promo code deleted.";
    }
}
