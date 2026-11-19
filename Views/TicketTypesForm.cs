using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-29: ticket types of an event - general admission (quantity) or numbered seats (venue section).</summary>
public class TicketTypesForm : Form
{
    private readonly EventController _events = new();
    private readonly Event _event;
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _name = Ui.Txt(200);
    private readonly NumericUpDown _price = Ui.Num(0, 100000000, 5000, 120), _qty = Ui.Num(0, 1000000, 100, 100);
    private readonly ComboBox _seating = Ui.Combo(200);
    private int _editingId;

    public TicketTypesForm(Event ev)
    {
        _event = ev;
        Text = $"Ticket types - {ev.Title}";
        ClientSize = new Size(900, 520);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false; ShowInTaskbar = false;

        _price.Increment = 500;
        LoadSeatingOptions();

        var form = Ui.Form(("Name", _name), ("Price (RWF)", _price), ("Seating", _seating), ("Quantity", _qty));
        var group = new GroupBox { Text = "Add or edit a ticket type", Dock = DockStyle.Bottom, Height = 190 };
        group.Controls.Add(form);
        var save = Ui.Btn("Save ticket type", (_, _) => Save(), 140);
        var clear = Ui.Btn("New", (_, _) => ClearForm(), 70);
        var delete = Ui.Btn("Delete selected", (_, _) => Delete(), 130);
        var bar = Ui.Bar(DockStyle.Bottom, save, clear, delete);

        Controls.Add(_grid);
        Controls.Add(bar);
        Controls.Add(group);
        _grid.SelectionChanged += (_, _) => LoadSelected();
        _seating.SelectedIndexChanged += (_, _) => _qty.Enabled = _seating.SelectedIndex == 0;
        Reload();
    }

    private void LoadSeatingOptions()
    {
        _seating.Items.Clear();
        _seating.Items.Add("General admission (quantity)");
        foreach (var s in _events.Sections(_event.VenueId)) _seating.Items.Add(s);
        _seating.SelectedIndex = 0;
    }

    private void Reload() { Ui.Bind(_grid, _events.TicketCategories(_event.Id)); LoadSelected(); }

    private void LoadSelected()
    {
        if (_grid.Selected<TicketCategory>() is not { } t) return;
        _editingId = t.Id; _name.Text = t.Name; _price.Value = Math.Min(_price.Maximum, t.Price);
        _qty.Value = Math.Min(_qty.Maximum, t.Quantity);
        var section = _seating.Items.OfType<SectionInfo>().FirstOrDefault(s => s.Section == t.Section);
        _seating.SelectedItem = (object?)section ?? _seating.Items[0];
    }

    private void ClearForm()
    {
        _grid.ClearSelection(); _editingId = 0; _name.Clear(); _price.Value = 5000; _qty.Value = 100; _seating.SelectedIndex = 0;
        _name.Focus();
    }

    private void Save()
    {
        var section = (_seating.SelectedItem as SectionInfo)?.Section;
        var r = _events.SaveTicketCategory(_event.Id, _editingId, _name.Text, _price.Value, (int)_qty.Value, section);
        if (!Ui.Report(r, "Ticket type saved.")) return;
        _editingId = 0; Reload(); ClearForm();
    }

    private void Delete()
    {
        if (_grid.Selected<TicketCategory>() is not { } t) { Ui.Warn("Select a ticket type first."); return; }
        if (!Ui.Confirm($"Delete '{t.Name}'?")) return;
        if (Ui.Report(_events.DeleteTicketCategory(_event.Id, t.Id), "Ticket type deleted.")) { _editingId = 0; Reload(); }
    }
}
