using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-11..19: choose tickets or seats, apply a promo code, pay and receive the tickets.</summary>
public class BookingView : UserControl, IRefreshable
{
    private readonly EventController _events = new();
    private readonly BookingController _bookings = new();
    private readonly ComboBox _event = Ui.Combo(420), _type = Ui.Combo(420), _method = Ui.Combo(180);
    private readonly NumericUpDown _qty = Ui.Num(1, 50, 1);
    private readonly TextBox _promo = Ui.Txt(140);
    private readonly CheckedListBox _seats = new() { Dock = DockStyle.Fill, CheckOnClick = true, MultiColumn = true, ColumnWidth = 90 };
    private readonly Label _info = new() { AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(3, 6, 3, 6) };
    private readonly Label _subtotal = Ui.Lbl("-"), _discount = Ui.Lbl("-"), _fee = Ui.Lbl("-"), _total = Ui.Lbl("-", true);
    private readonly GroupBox _seatBox = new() { Text = "Pick your seats", Dock = DockStyle.Fill };
    private string? _appliedPromo;
    private bool _loading;

    public BookingView()
    {
        foreach (var m in Enum.GetValues<PaymentMethod>()) _method.Items.Add(m);
        _method.SelectedIndex = 0;

        var apply = Ui.Btn("Apply", (_, _) => ApplyPromo(), 70);
        var promoRow = Ui.Bar(DockStyle.None, _promo, apply); promoRow.Padding = Padding.Empty;
        var book = Ui.Btn("Book and pay", (_, _) => DoBook(), 150); book.Height = 36;

        var form = Ui.Form(("Event", _event), ("Ticket type", _type), ("Quantity", _qty), ("Promo code", promoRow),
                           ("Payment method", _method), ("Subtotal", _subtotal), ("Discount", _discount),
                           ("Service fee", _fee), ("Total", _total));
        form.Dock = DockStyle.Fill;

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 640 };
        leftPanel.Controls.Add(form);
        var infoBox = new GroupBox { Text = "Event", Dock = DockStyle.Top, Height = 120 };
        infoBox.Controls.Add(_info);
        _seatBox.Controls.Add(_seats);

        Controls.Add(_seatBox);
        Controls.Add(infoBox);
        Controls.Add(leftPanel);
        Controls.Add(Ui.Bar(DockStyle.Bottom, book));

        _event.SelectedIndexChanged += (_, _) => OnEventChanged();
        _type.SelectedIndexChanged += (_, _) => OnTypeChanged();
        _qty.ValueChanged += (_, _) => Recalc();
        _seats.ItemCheck += OnSeatCheck;
        RefreshData();
    }

    public void RefreshData()
    {
        var keep = (_event.SelectedItem as Event)?.Id;
        _loading = true;
        _event.Items.Clear();
        foreach (var e in _events.Browse(null, null, null, null, null, null, null, "date")) _event.Items.Add(e);
        _event.SelectedItem = _event.Items.OfType<Event>().FirstOrDefault(e => e.Id == keep);
        _loading = false;
        if (_event.SelectedItem is null && _event.Items.Count > 0) _event.SelectedIndex = 0; else OnEventChanged();
    }

    public void Preselect(Event ev)
    {
        RefreshData();
        var match = _event.Items.OfType<Event>().FirstOrDefault(e => e.Id == ev.Id);
        if (match is not null) _event.SelectedItem = match;
    }

    private void OnEventChanged()
    {
        if (_loading) return;
        _type.Items.Clear();
        _appliedPromo = null; _promo.Clear();
        if (_event.SelectedItem is not Event ev) { _info.Text = "No events are open for booking."; OnTypeChanged(); return; }
        _info.Text = $"{ev.Title}\r\n{Ui.When(ev.StartDate)}\r\n{ev.VenueName}, {ev.City}\r\nMax {ev.MaxTicketsPerBooking} tickets per booking.";
        foreach (var t in _events.TicketCategories(ev.Id)) _type.Items.Add(t);
        if (_type.Items.Count > 0) _type.SelectedIndex = 0; else OnTypeChanged();
    }

    private void OnTypeChanged()
    {
        _seats.Items.Clear();
        var ev = _event.SelectedItem as Event; var cat = _type.SelectedItem as TicketCategory;
        if (ev is null || cat is null) { _seatBox.Visible = false; _qty.Enabled = false; Recalc(); return; }

        _qty.Maximum = Math.Max(1, Math.Min(ev.MaxTicketsPerBooking, Math.Max(cat.Available, 1)));
        if (cat.IsNumbered)
        {
            foreach (var s in _bookings.AvailableSeats(ev.Id, cat.Id)) _seats.Items.Add(s);
            _seatBox.Visible = true; _seatBox.Text = $"Pick your seats in section {cat.Section} ({_seats.Items.Count} free) - up to {ev.MaxTicketsPerBooking}";
            _qty.Enabled = false; _qty.Value = 1;
        }
        else
        {
            _seatBox.Visible = false; _qty.Enabled = cat.Available > 0;
            if (_qty.Value > _qty.Maximum) _qty.Value = _qty.Maximum;
        }
        Recalc();
    }

    private void OnSeatCheck(object? sender, ItemCheckEventArgs e)
    {
        var max = (_event.SelectedItem as Event)?.MaxTicketsPerBooking ?? 1;
        if (e.NewValue == CheckState.Checked && _seats.CheckedItems.Count >= max) { e.NewValue = CheckState.Unchecked; return; }
        BeginInvoke(new Action(() =>
        {
            var n = Math.Max(1, _seats.CheckedItems.Count);
            _qty.Value = Math.Min(n, _qty.Maximum);
            Recalc();
        }));
    }

    private int Quantity => (_type.SelectedItem as TicketCategory)?.IsNumbered == true ? _seats.CheckedItems.Count : (int)_qty.Value;

    private void Recalc()
    {
        if (_type.SelectedItem is not TicketCategory cat || Quantity < 1)
        {
            _subtotal.Text = _discount.Text = _fee.Text = _total.Text = "-";
            return;
        }
        var q = _bookings.Quote(cat, Quantity, _appliedPromo);
        if (!q.Ok) { _appliedPromo = null; Ui.Warn(q.Error!); q = _bookings.Quote(cat, Quantity, null); }
        var v = q.Value;
        _subtotal.Text = Ui.Rwf(v.Subtotal); _discount.Text = "- " + Ui.Rwf(v.Discount);
        _fee.Text = Ui.Rwf(v.Fee); _total.Text = Ui.Rwf(v.Total);
    }

    private void ApplyPromo()
    {
        if (_type.SelectedItem is not TicketCategory cat) { Ui.Warn("Choose a ticket type first."); return; }
        var code = _promo.Text.Trim();
        if (code.Length == 0) { _appliedPromo = null; Recalc(); return; }
        var q = _bookings.Quote(cat, Math.Max(1, Quantity), code);
        if (!Ui.Report(q)) return;
        _appliedPromo = code;
        Recalc();
        Ui.Info("Promo code applied.");
    }

    private void DoBook()
    {
        if (_event.SelectedItem is not Event ev || _type.SelectedItem is not TicketCategory cat) { Ui.Warn("Choose an event and a ticket type."); return; }
        var qty = Quantity;
        if (qty < 1) { Ui.Warn(cat.IsNumbered ? "Pick at least one seat." : "Choose a quantity."); return; }
        var seatIds = _seats.CheckedItems.OfType<Seat>().Select(s => s.Id).ToList();
        var method = (PaymentMethod)_method.SelectedItem!;
        if (!Ui.Confirm($"Book {qty} x {cat.Name} for {ev.Title}?\r\nTotal to pay: {_total.Text} ({method}).")) return;

        var result = _bookings.Book(ev, cat, qty, seatIds, _appliedPromo, method);
        if (!Ui.Report(result)) { OnTypeChanged(); return; }       // availability may have changed - reload it

        var booking = result.Value!;
        Ui.Info($"Booking confirmed.\r\nReference: {booking.Reference}\r\nPaid: {Ui.Rwf(booking.Total)}");
        _appliedPromo = null; _promo.Clear();
        OnEventChanged();
        using var tickets = new TicketsForm(booking);
        tickets.ShowDialog(FindForm());
    }
}
