using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Views;

/// <summary>Main window: one tab per feature, shown according to the user's role (FR-3).</summary>
public class MainForm : Form
{
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly BookingView _booking = new();
    private readonly MyBookingsView _myBookings = new();
    private readonly TabPage _bookingTab = new("Book tickets");
    public bool LoggedOut { get; private set; }

    public MainForm()
    {
        var me = AppSession.Me;
        Text = $"{Ui.AppName} - {me.FullName} ({me.Role})";
        Size = new Size(1150, 720);
        MinimumSize = new Size(950, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var events = new EventsView();
        events.BookRequested += ev => { _bookingTab.Tag = ev; _tabs.SelectedTab = _bookingTab; _booking.Preselect(ev); };

        AddTab("Events", events);
        _bookingTab.Controls.Add(_booking); _booking.Dock = DockStyle.Fill; _tabs.TabPages.Add(_bookingTab);
        AddTab("My bookings", _myBookings);

        if (me.Role is UserRole.Organizer or UserRole.Admin)
        {
            AddTab("Manage events", new ManageEventsView());
            AddTab("Check-in", new CheckInView());
            AddTab("Reports", new ReportsView());
        }
        if (me.Role == UserRole.Admin)
        {
            AddTab("Venues & seats", new VenuesView());
            AddTab("Categories & promos", new LookupsView());
            AddTab("Users", new UsersView());
        }

        _tabs.SelectedIndexChanged += (_, _) =>
        {
            if (_tabs.SelectedTab?.Controls.Count > 0 && _tabs.SelectedTab.Controls[0] is IRefreshable r) r.RefreshData();
        };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6), FlowDirection = FlowDirection.RightToLeft };
        top.Controls.Add(Ui.Btn("Log out", (_, _) => { LoggedOut = true; Close(); }, 90));
        top.Controls.Add(Ui.Btn("My profile", (_, _) => { using var p = new ProfileForm(); p.ShowDialog(this); }, 100));
        top.Controls.Add(new Label { Text = $"Signed in as {me.FullName} - {me.Role}", AutoSize = true, Margin = new Padding(6, 10, 12, 0) });

        Controls.Add(_tabs);
        Controls.Add(top);
    }

    private void AddTab(string title, UserControl view)
    {
        view.Dock = DockStyle.Fill;
        var page = new TabPage(title);
        page.Controls.Add(view);
        _tabs.TabPages.Add(page);
    }
}

/// <summary>Views that reload their data when their tab is opened.</summary>
public interface IRefreshable { void RefreshData(); }
