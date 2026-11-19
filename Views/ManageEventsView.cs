using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-28..30, FR-33: organizers manage their events; admins manage all and approve.</summary>
public class ManageEventsView : UserControl, IRefreshable
{
    private readonly EventController _events = new();
    private readonly DataGridView _grid = Ui.Grid();

    public ManageEventsView()
    {
        var buttons = Ui.Bar(DockStyle.Bottom,
            Ui.Btn("New event", (_, _) => Edit(null), 110),
            Ui.Btn("Edit", (_, _) => Edit(_grid.Selected<Event>()), 90),
            Ui.Btn("Ticket types", (_, _) => TicketTypes(), 110),
            Ui.Btn("Cancel event", (_, _) => CancelEvent(), 110));
        if (AppSession.IsAdmin)
        {
            buttons.Controls.Add(Ui.Btn("Approve", (_, _) => Approve(), 90));
            buttons.Controls.Add(Ui.Btn("Remove", (_, _) => Remove(), 90));
        }
        var hint = new Label
        {
            Dock = DockStyle.Top, Height = 28, Padding = new Padding(6, 6, 0, 0),
            Text = AppSession.IsAdmin ? "All events. Approve pending events so they appear in the catalogue."
                                      : "Your events. New events are visible to attendees after an admin approves them."
        };
        Controls.Add(_grid);
        Controls.Add(buttons);
        Controls.Add(hint);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Edit(_grid.Selected<Event>()); };
        Load();
    }

    public void RefreshData() => Load();
    private void Load() => Ui.Bind(_grid, _events.ManagedEvents());

    private void Edit(Event? ev)
    {
        using var f = new EventEditForm(ev);
        if (f.ShowDialog(FindForm()) != DialogResult.OK) return;
        Load();
        if (ev is null && f.SavedEvent is { } created && Ui.Confirm("Event saved. Add its ticket types now?"))
            OpenTicketTypes(created);
    }

    private void TicketTypes()
    {
        if (_grid.Selected<Event>() is { } ev) OpenTicketTypes(ev); else Ui.Warn("Select an event first.");
    }

    private void OpenTicketTypes(Event ev)
    {
        using var f = new TicketTypesForm(ev);
        f.ShowDialog(FindForm());
        Load();
    }

    private void CancelEvent()
    {
        if (_grid.Selected<Event>() is not { } ev) { Ui.Warn("Select an event first."); return; }
        if (!Ui.Confirm($"Cancel '{ev.Title}'? Attendees will be able to request refunds.")) return;
        if (Ui.Report(_events.Cancel(ev.Id), "Event cancelled.")) Load();
    }

    private void Approve()
    {
        if (_grid.Selected<Event>() is not { } ev) { Ui.Warn("Select an event first."); return; }
        if (ev.Status != EventStatus.Pending) { Ui.Info("Only pending events need approval."); return; }
        if (Ui.Report(_events.Approve(ev.Id), "Event published.")) Load();
    }

    private void Remove()
    {
        if (_grid.Selected<Event>() is not { } ev) { Ui.Warn("Select an event first."); return; }
        if (!Ui.Confirm($"Remove '{ev.Title}'? If it has bookings it will be cancelled instead of deleted.")) return;
        var r = _events.Remove(ev.Id);
        if (Ui.Report(r)) { Ui.Info(r.Value!); Load(); }
    }
}
