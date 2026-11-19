using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-6..10: browse, search, filter and sort published events.</summary>
public class EventsView : UserControl, IRefreshable
{
    private readonly EventController _events = new();
    private readonly TextBox _search = Ui.Txt(200);
    private readonly ComboBox _category = Ui.Combo(150), _city = Ui.Combo(130), _sort = Ui.Combo(160);
    private readonly NumericUpDown _min = Ui.Num(0, 100000000, 0, 95), _max = Ui.Num(0, 100000000, 0, 95);
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _details = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly ListBox _types = new() { Dock = DockStyle.Fill };
    public event Action<Event>? BookRequested;

    public EventsView()
    {
        _sort.Items.AddRange(new object[] { "Soonest first", "Price (low to high)", "Popularity" });
        _sort.SelectedIndex = 0;

        var search = Ui.Btn("Search", (_, _) => Load(), 90);
        var clear = Ui.Btn("Clear", (_, _) => { _search.Clear(); _category.SelectedIndex = 0; _city.SelectedIndex = 0; _min.Value = 0; _max.Value = 0; _sort.SelectedIndex = 0; Load(); }, 80);
        var filters = Ui.Bar(DockStyle.Top, Ui.Lbl("Search"), _search, Ui.Lbl("Category"), _category, Ui.Lbl("City"), _city,
            Ui.Lbl("Price from"), _min, Ui.Lbl("to"), _max, Ui.Lbl("Sort"), _sort, search, clear);

        var book = Ui.Btn("Book tickets for this event", (_, _) =>
        {
            if (_grid.Selected<Event>() is { } ev) BookRequested?.Invoke(ev);
            else Ui.Warn("Select an event first.");
        }, 210);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 190, ColumnCount = 2, RowCount = 1 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        var left = new GroupBox { Text = "Details", Dock = DockStyle.Fill }; left.Controls.Add(_details);
        var right = new GroupBox { Text = "Ticket types", Dock = DockStyle.Fill }; right.Controls.Add(_types);
        bottom.Controls.Add(left, 0, 0); bottom.Controls.Add(right, 1, 0);

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(Ui.Bar(DockStyle.Bottom, book));
        Controls.Add(filters);

        _grid.SelectionChanged += (_, _) => ShowDetails();
        _search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Load(); };
        LoadLookups();
        Load();
    }

    public void RefreshData() { LoadLookups(); Load(); }

    private void LoadLookups()
    {
        var cat = (_category.SelectedItem as EventCategory)?.Id; var city = _city.SelectedItem as string;
        _category.Items.Clear(); _category.Items.Add("All categories");
        foreach (var c in _events.Categories()) _category.Items.Add(c);
        _category.SelectedItem = _category.Items.OfType<EventCategory>().FirstOrDefault(c => c.Id == cat) ?? _category.Items[0];

        _city.Items.Clear(); _city.Items.Add("All cities");
        foreach (var c in _events.Cities()) _city.Items.Add(c);
        _city.SelectedItem = city is not null && _city.Items.Contains(city) ? city : _city.Items[0];
    }

    private void Load()
    {
        var sort = _sort.SelectedIndex switch { 1 => "price", 2 => "popular", _ => "date" };
        var list = _events.Browse(_search.Text, (_category.SelectedItem as EventCategory)?.Id,
            _city.SelectedIndex > 0 ? _city.SelectedItem as string : null, null, null,
            _min.Value > 0 ? _min.Value : null, _max.Value > 0 ? _max.Value : null, sort);
        Ui.Bind(_grid, list);
        ShowDetails();
    }

    private void ShowDetails()
    {
        _types.Items.Clear();
        if (_grid.Selected<Event>() is not { } ev) { _details.Text = list0(); return; }
        _details.Text = $"{ev.Title}\r\n{Ui.When(ev.StartDate)} - {Ui.When(ev.EndDate)}\r\n{ev.VenueName}, {ev.City}\r\n" +
                        $"Category: {ev.CategoryName}   Organiser: {ev.OrganizerName}\r\n" +
                        $"Max {ev.MaxTicketsPerBooking} tickets per booking. Free cancellation until {ev.CancellationDeadlineHours} h before the start.\r\n\r\n{ev.Description}";
        foreach (var t in _events.TicketCategories(ev.Id)) _types.Items.Add(t);
        if (_types.Items.Count == 0) _types.Items.Add("Tickets are not on sale yet.");

        static string list0() => "No events match your search.";
    }
}
