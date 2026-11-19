using System.Globalization;
using System.Text;
using TicketRegistrationDesktop.Data;
using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Controllers;

/// <summary>FR-39..41: dashboard numbers, sales reports and CSV export (opens in Excel).</summary>
public class ReportController
{
    private readonly ReportRepository _reports = new();

    public Totals Dashboard() => _reports.Totals(AppSession.Me);

    public List<SalesRow> Sales(DateTime? from, DateTime? to, int? eventId, int? categoryId) =>
        _reports.Sales(AppSession.Me, from, to, eventId, categoryId);

    public Result ExportCsv(IEnumerable<SalesRow> rows, string path)
    {
        try
        {
            var sb = new StringBuilder("Event,Category,Bookings,Tickets,Revenue (RWF)\r\n");
            foreach (var r in rows)
                sb.Append(Csv(r.Event)).Append(',').Append(Csv(r.Category)).Append(',').Append(r.Bookings).Append(',')
                  .Append(r.Tickets).Append(',').Append(r.Revenue.ToString("0.##", CultureInfo.InvariantCulture)).Append("\r\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));   // BOM so Excel reads UTF-8 correctly
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Fail("Could not write the file: " + ex.Message);
        }
    }

    private static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
}
