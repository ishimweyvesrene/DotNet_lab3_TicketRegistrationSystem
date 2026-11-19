using System.Data;
using MySqlConnector;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Data;

/// <summary>Small ADO.NET helper. All SQL is parameterised (no string-built values) to prevent SQL injection.</summary>
public static class Db
{
    public static MySqlConnection Open(bool withDatabase = true)
    {
        var c = new MySqlConnection(AppSettings.Current.BuildConnectionString(withDatabase));
        c.Open();
        return c;
    }

    public static MySqlParameter P(string name, object? value) => new(name, value ?? DBNull.Value);

    private static MySqlCommand Make(MySqlConnection c, MySqlTransaction? tx, string sql, MySqlParameter[] ps)
    {
        var cmd = new MySqlCommand(sql, c, tx);
        cmd.Parameters.AddRange(ps);
        return cmd;
    }

    // ---- on an existing connection / transaction
    public static List<T> Query<T>(MySqlConnection c, MySqlTransaction? tx, string sql, Func<MySqlDataReader, T> map, params MySqlParameter[] ps)
    {
        using var cmd = Make(c, tx, sql, ps);
        using var r = cmd.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    public static T? Scalar<T>(MySqlConnection c, MySqlTransaction? tx, string sql, params MySqlParameter[] ps)
    {
        using var cmd = Make(c, tx, sql, ps);
        var v = cmd.ExecuteScalar();
        if (v is null || v is DBNull) return default;
        return (T)Convert.ChangeType(v, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    public static int Execute(MySqlConnection c, MySqlTransaction? tx, string sql, params MySqlParameter[] ps)
    {
        using var cmd = Make(c, tx, sql, ps);
        return cmd.ExecuteNonQuery();
    }

    public static long Insert(MySqlConnection c, MySqlTransaction? tx, string sql, params MySqlParameter[] ps)
    {
        using var cmd = Make(c, tx, sql, ps);
        cmd.ExecuteNonQuery();
        return cmd.LastInsertedId;
    }

    // ---- one-shot versions that open their own connection
    public static List<T> Query<T>(string sql, Func<MySqlDataReader, T> map, params MySqlParameter[] ps)
    { using var c = Open(); return Query(c, null, sql, map, ps); }

    public static T? Scalar<T>(string sql, params MySqlParameter[] ps)
    { using var c = Open(); return Scalar<T>(c, null, sql, ps); }

    public static int Execute(string sql, params MySqlParameter[] ps)
    { using var c = Open(); return Execute(c, null, sql, ps); }

    public static long Insert(string sql, params MySqlParameter[] ps)
    { using var c = Open(); return Insert(c, null, sql, ps); }
}

/// <summary>Null-safe column readers.</summary>
public static class ReaderExtensions
{
    private static object? Raw(MySqlDataReader r, string col)
    {
        var v = r.GetValue(r.GetOrdinal(col));
        return v is DBNull ? null : v;
    }

    public static string Str(this MySqlDataReader r, string col) => Raw(r, col)?.ToString() ?? "";
    public static string? StrN(this MySqlDataReader r, string col) => Raw(r, col)?.ToString();
    public static int Int(this MySqlDataReader r, string col) => Convert.ToInt32(Raw(r, col) ?? 0);
    public static int? IntN(this MySqlDataReader r, string col) { var v = Raw(r, col); return v is null ? null : Convert.ToInt32(v); }
    public static decimal Dec(this MySqlDataReader r, string col) => Convert.ToDecimal(Raw(r, col) ?? 0m);
    public static decimal? DecN(this MySqlDataReader r, string col) { var v = Raw(r, col); return v is null ? null : Convert.ToDecimal(v); }
    public static DateTime Dt(this MySqlDataReader r, string col) => Convert.ToDateTime(Raw(r, col));
    public static DateTime? DtN(this MySqlDataReader r, string col) { var v = Raw(r, col); return v is null ? null : Convert.ToDateTime(v); }
    public static bool Bool(this MySqlDataReader r, string col) => Convert.ToInt32(Raw(r, col) ?? 0) != 0;
    public static T EnumOf<T>(this MySqlDataReader r, string col) where T : struct, System.Enum => System.Enum.Parse<T>(r.Str(col));
}
