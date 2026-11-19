using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-25: validate tickets at the entrance. A USB / Bluetooth QR scanner types the code and presses Enter,
/// or the code can be pasted / typed by hand.</summary>
public class CheckInView : UserControl, IRefreshable
{
    private readonly CheckInController _checkIn = new();
    private readonly EventController _events = new();
    private readonly ComboBox _event = Ui.Combo(420);
    private readonly TextBox _code = new() { Width = 420, Font = new Font("Consolas", 12f), Margin = new Padding(3) };
    private readonly Panel _result = new() { Dock = DockStyle.Top, Height = 170, BackColor = SystemColors.Control, Padding = new Padding(16) };
    private readonly Label _title = new() { Dock = DockStyle.Top, Height = 60, Font = new Font("Segoe UI", 28f, FontStyle.Bold), Text = "Ready" };
    private readonly Label _message = new() { Dock = DockStyle.Top, Height = 32, Font = new Font("Segoe UI", 13f) };
    private readonly Label _who = new() { Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 11f) };
    private readonly ListBox _log = new() { Dock = DockStyle.Fill };

    public CheckInView()
    {
        _result.Controls.Add(_who); _result.Controls.Add(_message); _result.Controls.Add(_title);
        var validate = Ui.Btn("Validate", (_, _) => Scan(), 110);
        var top = Ui.Bar(DockStyle.Top, Ui.Lbl("Event"), _event, Ui.Lbl("Ticket code"), _code, validate);

        Controls.Add(_log);
        Controls.Add(new Label { Dock = DockStyle.Top, Text = "Recent scans", Height = 24, Padding = new Padding(6, 6, 0, 0) });
        Controls.Add(_result);
        Controls.Add(top);
        _code.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Scan(); } };
        RefreshData();
    }

    public void RefreshData()
    {
        var keep = (_event.SelectedItem as Event)?.Id;
        _event.Items.Clear();
        _event.Items.Add("Any of my events");
        foreach (var e in _events.ManagedEvents().Where(e => e.Status == EventStatus.Published)) _event.Items.Add(e);
        _event.SelectedItem = _event.Items.OfType<Event>().FirstOrDefault(e => e.Id == keep) ?? _event.Items[0];
    }

    private void Scan()
    {
        var text = _code.Text.Trim();
        if (text.Length == 0) return;
        var r = _checkIn.Scan(text, (_event.SelectedItem as Event)?.Id);

        _result.BackColor = r.Status switch
        {
            CheckInStatus.Valid => Color.LightGreen,
            CheckInStatus.AlreadyUsed => Color.Khaki,
            _ => Color.LightCoral
        };
        _title.Text = r.Status switch
        {
            CheckInStatus.Valid => "VALID",
            CheckInStatus.AlreadyUsed => "ALREADY USED",
            CheckInStatus.Cancelled => "CANCELLED",
            CheckInStatus.WrongEvent => "WRONG EVENT",
            CheckInStatus.Forbidden => "NOT ALLOWED",
            _ => "INVALID"
        };
        _message.Text = r.Message;
        _who.Text = r.Holder is null ? "" : r.Holder + (r.Detail is null ? "" : " - " + r.Detail);
        _log.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {_title.Text}  {_who.Text}");

        _code.Clear(); _code.Focus();
    }
}
