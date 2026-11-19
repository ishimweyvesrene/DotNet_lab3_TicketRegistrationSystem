using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

public class EventEditForm : Form
{
    private readonly EventController _events = new();
    private readonly Event _event;
    private readonly TextBox _title = Ui.Txt(380);
    private readonly TextBox _desc = new() { Multiline = true, Width = 380, Height = 90, ScrollBars = ScrollBars.Vertical, Margin = new Padding(3) };
    private readonly DateTimePicker _start = Ui.Picker(true), _end = Ui.Picker(true);
    private readonly ComboBox _venue = Ui.Combo(380), _category = Ui.Combo(200);
    private readonly NumericUpDown _max = Ui.Num(1, 50, 6), _hours = Ui.Num(0, 720, 24, 80);
    public Event? SavedEvent { get; private set; }

    public EventEditForm(Event? existing)
    {
        _event = existing ?? new Event { StartDate = DateTime.Today.AddDays(14).AddHours(18), EndDate = DateTime.Today.AddDays(14).AddHours(21) };
        Text = existing is null ? "New event" : "Edit event";
        ClientSize = new Size(560, 440);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;

        foreach (var v in _events.Venues()) _venue.Items.Add(v);
        foreach (var c in _events.Categories()) _category.Items.Add(c);

        _title.Text = _event.Title; _desc.Text = _event.Description;
        _start.Value = _event.StartDate; _end.Value = _event.EndDate;
        _max.Value = _event.MaxTicketsPerBooking; _hours.Value = _event.CancellationDeadlineHours;
        _venue.SelectedItem = _venue.Items.OfType<Venue>().FirstOrDefault(v => v.Id == _event.VenueId);
        _category.SelectedItem = _category.Items.OfType<EventCategory>().FirstOrDefault(c => c.Id == _event.CategoryId);

        var save = Ui.Btn("Save", (_, _) => Save(), 100);
        var cancel = Ui.Btn("Cancel", (_, _) => DialogResult = DialogResult.Cancel, 100);
        Controls.Add(Ui.Form(("Title", _title), ("Description", _desc), ("Starts", _start), ("Ends", _end),
                             ("Venue", _venue), ("Category", _category), ("Max tickets per booking", _max),
                             ("Free cancellation until (hours before)", _hours)));
        Controls.Add(Ui.ButtonBar(cancel, save));
        AcceptButton = null; CancelButton = cancel;
    }

    private void Save()
    {
        _event.Title = _title.Text; _event.Description = _desc.Text;
        _event.StartDate = _start.Value; _event.EndDate = _end.Value;
        _event.VenueId = (_venue.SelectedItem as Venue)?.Id ?? 0;
        _event.CategoryId = (_category.SelectedItem as EventCategory)?.Id ?? 0;
        _event.MaxTicketsPerBooking = (int)_max.Value; _event.CancellationDeadlineHours = (int)_hours.Value;

        var r = _events.Save(_event);
        if (!Ui.Report(r)) return;
        SavedEvent = r.Value;
        DialogResult = DialogResult.OK;
    }
}
