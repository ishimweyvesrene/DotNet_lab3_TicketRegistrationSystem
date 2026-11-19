using MySqlConnector;
using TicketRegistrationDesktop.Models;
using static TicketRegistrationDesktop.Data.Db;

namespace TicketRegistrationDesktop.Data;

public class UserRepository
{
    private const string Select = "SELECT id, full_name, email, phone, password_hash, role, is_active, created_at FROM users";

    private static User Map(MySqlDataReader r) => new()
    {
        Id = r.Int("id"), FullName = r.Str("full_name"), Email = r.Str("email"), Phone = r.Str("phone"),
        PasswordHash = r.Str("password_hash"), Role = r.EnumOf<UserRole>("role"),
        IsActive = r.Bool("is_active"), CreatedAt = r.Dt("created_at")
    };

    public User? FindByEmail(string email) =>
        Query<User>(Select + " WHERE email=@e", Map, P("@e", email)).FirstOrDefault();

    public User? FindById(int id) =>
        Query<User>(Select + " WHERE id=@i", Map, P("@i", id)).FirstOrDefault();

    public bool EmailExists(string email) =>
        Scalar<long>("SELECT COUNT(*) FROM users WHERE email=@e", P("@e", email)) > 0;

    public int Create(string fullName, string email, string phone, string passwordHash, UserRole role) =>
        (int)Insert("INSERT INTO users (full_name, email, phone, password_hash, role) VALUES (@n,@e,@p,@h,@r)",
            P("@n", fullName), P("@e", email), P("@p", phone), P("@h", passwordHash), P("@r", role.ToString()));

    public List<User> Search(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Query<User>(Select + " ORDER BY full_name", Map);
        return Query<User>(Select + " WHERE full_name LIKE @q OR email LIKE @q ORDER BY full_name", Map, P("@q", "%" + q.Trim() + "%"));
    }

    public void SetActive(int id, bool active) =>
        Execute("UPDATE users SET is_active=@a WHERE id=@i", P("@a", active ? 1 : 0), P("@i", id));

    public void SetRole(int id, UserRole role) =>
        Execute("UPDATE users SET role=@r WHERE id=@i", P("@r", role.ToString()), P("@i", id));

    public void UpdateProfile(int id, string fullName, string phone) =>
        Execute("UPDATE users SET full_name=@n, phone=@p WHERE id=@i", P("@n", fullName), P("@p", phone), P("@i", id));

    public void UpdatePassword(int id, string hash) =>
        Execute("UPDATE users SET password_hash=@h WHERE id=@i", P("@h", hash), P("@i", id));
}
