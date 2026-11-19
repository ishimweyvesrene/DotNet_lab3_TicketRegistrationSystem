using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-19..24, FR-26, FR-34: booking history, tickets, cancellation and refunds.</summary>
public class MyBookingsView : UserControl, IRefreshable
{
    private readonly BookingController _bookings = new();
    private readonly TextBox _search = Ui.Txt(200);
    private readonly ComboBox _status = Ui.Combo(130);
    private readonly CheckBox _all = new() { AutoSize = true, Margin = new Padding(8, 8, 3, 3) };
    private readonly DataGridView _grid = Ui.Grid();

    public MyBookingsView()
    {
        _status.Items.Add("All statuses");
        foreach (var s in Enum.GetValues<BookingStatus>()) _status.Items.Add(s);
        _status.SelectedIndex = 0;
        _all.Text = AppSession.IsAdmin ? "Show all bookings" : "Bookings for my events";
        _all.Visible = AppSession.Me.Role != UserRole.Attendee;

        var filters = Ui.Bar(DockStyle.Top, Ui.Lbl("Search"), _search, Ui.Lbl("Status"), _status, _all, Ui.Btn("Search", (_, _) => Load(), 90));
        var buttons = Ui.Bar(DockStyle.Bottom,
            Ui.Btn("View tickets / QR", (_, _) => ViewTickets(), 150),
            Ui.Btn("Cancel booking", (_, _) => Cancel(false), 130),
            Ui.Btn("Request refund", (_, _) => Cancel(true), 130));

        Controls.Add(_grid);
        Controls.Add(buttons);
        Controls.Add(filters);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ViewTickets(); };
        _search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Load(); };
        Load();
    }

    public void RefreshData() => Load();

    private void Load()
    {
        var status = _status.SelectedItem is BookingStatus s ? s : (BookingStatus?)null;
        Ui.Bind(_grid, _bookings.List(_all.Checked, _search.Text, status));
    }

    private void ViewTickets()
    {
        if (_grid.Selected<Booking>() is not { } b) { Ui.Warn("Select a booking first."); return; }
        using var f = new TicketsForm(b);
        f.ShowDialog(FindForm());
    }

    private void Cancel(bool refund)
    {
        if (_grid.Selected<Booking>() is not { } b) { Ui.Warn("Select a booking first."); return; }
        var question = refund
            ? $"Refund booking {b.Reference}? (only possible when the event was cancelled)"
            : $"Cancel booking {b.Reference}? The seats become available again and the payment is refunded.";
        if (!Ui.Confirm(question)) return;
        var r = refund ? _bookings.Refund(b.Id) : _bookings.Cancel(b.Id);
        if (Ui.Report(r, refund ? "Refund issued." : "Booking cancelled.")) Load();
    }
}
