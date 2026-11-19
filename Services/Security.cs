using System.Security.Cryptography;
using System.Text;
using QRCoder;

namespace TicketRegistrationDesktop.Services;

public static class Passwords
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public static bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch (Exception) { return false; }
    }
}

/// <summary>Signed ticket codes. The QR contains TRS.&lt;code&gt;.&lt;signature&gt; so a forged QR is rejected at the door.</summary>
public static class TicketTokens
{
    public static string NewCode() => Guid.NewGuid().ToString("N");

    public static string Sign(string code)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(AppSettings.Current.QrSecret));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(code)))[..16];
    }

    public static string Payload(string code) => $"TRS.{code}.{Sign(code)}";

    public static bool TryParse(string? payload, out string code)
    {
        code = "";
        var parts = (payload ?? "").Trim().Split('.');
        if (parts.Length != 3 || parts[0] != "TRS" || parts[1].Length == 0) return false;
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Sign(parts[1])), Encoding.UTF8.GetBytes(parts[2].ToUpperInvariant()));
        if (ok) code = parts[1];
        return ok;
    }

    public static Bitmap QrImage(string code, int pixelsPerModule = 6)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(Payload(code), QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule);
        using var ms = new MemoryStream(png);
        using var img = Image.FromStream(ms);
        return new Bitmap(img);
    }
}

public static class Pricing
{
    /// <summary>FR-16: subtotal, discount, service fee and total.</summary>
    public static (decimal Subtotal, decimal Discount, decimal Fee, decimal Total) Quote(decimal unitPrice, int quantity, int discountPercent)
    {
        var subtotal = unitPrice * quantity;
        var discount = Math.Round(subtotal * discountPercent / 100m, 2);
        var net = subtotal - discount;
        var fee = Math.Round(net * AppSettings.Current.ServiceFeePercent / 100m, 2);
        return (subtotal, discount, fee, net + fee);
    }
}
