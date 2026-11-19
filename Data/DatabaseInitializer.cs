using MySqlConnector;
using System.Text;
using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Data;

/// <summary>Creates the database and tables (Database/schema.sql) and seeds the first admin account.</summary>
public static class DatabaseInitializer
{
    public static void EnsureReady()
    {
        var script = Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql");
        if (!File.Exists(script)) throw new FileNotFoundException("Database/schema.sql is missing.", script);

        using (var server = Db.Open(withDatabase: false))
        {
            var sql = new StringBuilder();
            foreach (var line in File.ReadLines(script))
            {
                var commentStart = line.IndexOf("--", StringComparison.Ordinal);
                sql.AppendLine(commentStart >= 0 ? line[..commentStart] : line);
            }

            foreach (var statement in sql.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                using var command = new MySqlCommand(statement, server);
                command.ExecuteNonQuery();
            }
        }
        Seed();
    }

    private static void Seed()
    {
        var cfg = AppSettings.Current;
        using var c = Db.Open();

        if (Db.Scalar<long>(c, null, "SELECT COUNT(*) FROM users") == 0)
        {
            Db.Insert(c, null,
                "INSERT INTO users (full_name, email, phone, password_hash, role) VALUES (@n,@e,@p,@h,'Admin')",
                Db.P("@n", "System Admin"), Db.P("@e", cfg.AdminEmail), Db.P("@p", "0000000000"),
                Db.P("@h", Passwords.Hash(cfg.AdminPassword)));
        }

        if (Db.Scalar<long>(c, null, "SELECT COUNT(*) FROM categories") == 0)
            foreach (var n in new[] { "Concert", "Conference", "Sports", "Theatre", "Festival", "Other" })
                Db.Insert(c, null, "INSERT INTO categories (name) VALUES (@n)", Db.P("@n", n));

        if (cfg.SeedDemoData && Db.Scalar<long>(c, null, "SELECT COUNT(*) FROM events") == 0)
        {
            var organizerId = Db.Insert(c, null,
                "INSERT INTO users (full_name, email, phone, password_hash, role) VALUES (@n,@e,@p,@h,'Organizer')",
                Db.P("@n", "Demo Organizer"), Db.P("@e", "organizer@trs.local"), Db.P("@p", "0788000000"),
                Db.P("@h", Passwords.Hash("Organizer@12345")));

            var venueId = Db.Insert(c, null,
                "INSERT INTO venues (name, city, address, capacity) VALUES ('Kigali Convention Hall','Kigali','KG 2 Roundabout',500)");
            for (var r = 0; r < 3; r++)
                for (var n = 1; n <= 8; n++)
                    Db.Insert(c, null, "INSERT INTO seats (venue_id, section, row_label, seat_number) VALUES (@v,'VIP',@r,@n)",
                        Db.P("@v", venueId), Db.P("@r", ((char)('A' + r)).ToString()), Db.P("@n", n));

            var catId = Db.Scalar<int>(c, null, "SELECT id FROM categories WHERE name='Concert'");
            var eventId = Db.Insert(c, null,
                @"INSERT INTO events (title, description, start_date, end_date, venue_id, category_id, organizer_id, status)
                  VALUES ('Kigali Jazz Night','An evening of live jazz with local and regional artists.',
                          DATE_ADD(CURDATE(), INTERVAL 30 DAY) + INTERVAL 19 HOUR,
                          DATE_ADD(CURDATE(), INTERVAL 30 DAY) + INTERVAL 23 HOUR,
                          @v, @c, @o, 'Published')",
                Db.P("@v", venueId), Db.P("@c", catId), Db.P("@o", organizerId));
            Db.Insert(c, null, "INSERT INTO ticket_categories (event_id, name, price, quantity, section) VALUES (@e,'Regular',5000,200,NULL)", Db.P("@e", eventId));
            Db.Insert(c, null, "INSERT INTO ticket_categories (event_id, name, price, quantity, section) VALUES (@e,'VIP (numbered seats)',20000,24,'VIP')", Db.P("@e", eventId));
            Db.Insert(c, null, "INSERT INTO promo_codes (code, discount_percent, expiry_date, is_active) VALUES ('WELCOME10',10,DATE_ADD(CURDATE(), INTERVAL 3 MONTH),1)");
        }
    }
}
