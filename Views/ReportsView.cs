using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-39..41: dashboard numbers, sales report and CSV export.</summary>
public class ReportsView : UserControl, IRefreshable
{
    private readonly ReportController _reports = new();
    private readonly EventController _events = new();
    private readonly DateTimePicker _from = Ui.Picker(false), _to = Ui.Picker(false);
    private readonly ComboBox _event = Ui.Combo(220), _category = Ui.Combo(150);
    private readonly DataGridView _grid = Ui.Grid();
    private readonly Label _totals = new() { Dock = DockStyle.Top, Height = 40, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Padding = new Padding(8, 10, 0, 0) };
    private readonly Label _summary = new() { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(8, 6, 0, 0) };
    private List<SalesRow> _rows = new();

    public ReportsView()
    {
        _from.ShowCheckBox = true; _from.Checked = false;
        _to.ShowCheckBox = true; _to.Checked = false;
        var filters = Ui.Bar(DockStyle.Top, Ui.Lbl("From"), _from, Ui.Lbl("To"), _to, Ui.Lbl("Event"), _event,
            Ui.Lbl("Category"), _category, Ui.Btn("Generate", (_, _) => Generate(), 100), Ui.Btn("Export CSV...", (_, _) => Export(), 110));
        Controls.Add(_grid);
        Controls.Add(_summary);
        Controls.Add(filters);
        Controls.Add(_totals);
        RefreshData();
    }

    public void RefreshData()
    {
        var keepEvent = (_event.SelectedItem as Event)?.Id; var keepCat = (_category.SelectedItem as EventCategory)?.Id;
        _event.Items.Clear(); _event.Items.Add("All events");
        foreach (var e in _events.ManagedEvents()) _event.Items.Add(e);
        _event.SelectedItem = _event.Items.OfType<Event>().FirstOrDefault(e => e.Id == keepEvent) ?? _event.Items[0];
        _category.Items.Clear(); _category.Items.Add("All categories");
        foreach (var c in _events.Categories()) _category.Items.Add(c);
        _category.SelectedItem = _category.Items.OfType<EventCategory>().FirstOrDefault(c => c.Id == keepCat) ?? _category.Items[0];

        var t = _reports.Dashboard();
        _totals.Text = $"Sales {Ui.Rwf(t.Sales)}   |   Confirmed bookings {t.Bookings}   |   Tickets sold {t.TicketsSold}   |   Active events {t.ActiveEvents}   |   Users {t.Users}";
        Generate();
    }

    private void Generate()
    {
        _rows = _reports.Sales(_from.Checked ? _from.Value : null, _to.Checked ? _to.Value : null,
            (_event.SelectedItem as Event)?.Id, (_category.SelectedItem as EventCategory)?.Id);
        Ui.Bind(_grid, _rows);
        _summary.Text = _rows.Count == 0
            ? "No confirmed sales for these filters."
            : $"Total: {_rows.Sum(r => r.Bookings)} bookings, {_rows.Sum(r => r.Tickets)} tickets, {Ui.Rwf(_rows.Sum(r => r.Revenue))}";
    }

    private void Export()
    {
        if (_rows.Count == 0) { Ui.Warn("Generate a report with at least one row first."); return; }
        using var dlg = new SaveFileDialog { Filter = "CSV (opens in Excel)|*.csv", FileName = $"sales-report-{DateTime.Now:yyyyMMdd}.csv" };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        Ui.Report(_reports.ExportCsv(_rows, dlg.FileName), "Report saved.");
    }
}
