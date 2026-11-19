using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-32: venues and their seat layouts (admin).</summary>
public class VenuesView : UserControl, IRefreshable
{
    private readonly AdminController _admin = new();
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _name = Ui.Txt(220), _city = Ui.Txt(160), _address = Ui.Txt(260);
    private readonly NumericUpDown _capacity = Ui.Num(1, 1000000, 100, 100);
    private int _editingId;

    public VenuesView()
    {
        var group = new GroupBox { Text = "Add or edit a venue", Dock = DockStyle.Bottom, Height = 180 };
        group.Controls.Add(Ui.Form(("Name", _name), ("City", _city), ("Address", _address), ("Capacity", _capacity)));
        var buttons = Ui.Bar(DockStyle.Bottom,
            Ui.Btn("Save venue", (_, _) => Save(), 110),
            Ui.Btn("New", (_, _) => Clear(), 70),
            Ui.Btn("Seat layout...", (_, _) => Seats(), 120),
            Ui.Btn("Delete", (_, _) => Delete(), 90));
        Controls.Add(_grid);
        Controls.Add(buttons);
        Controls.Add(group);
        _grid.SelectionChanged += (_, _) => LoadSelected();
        Load();
    }

    public void RefreshData() => Load();
    private void Load() { Ui.Bind(_grid, _admin.Venues()); LoadSelected(); }

    private void LoadSelected()
    {
        if (_grid.Selected<Venue>() is not { } v) return;
        _editingId = v.Id; _name.Text = v.Name; _city.Text = v.City; _address.Text = v.Address ?? "";
        _capacity.Value = Math.Min(_capacity.Maximum, Math.Max(1, v.Capacity));
    }

    private void Clear() { _grid.ClearSelection(); _editingId = 0; _name.Clear(); _city.Clear(); _address.Clear(); _capacity.Value = 100; _name.Focus(); }

    private void Save()
    {
        var v = new Venue { Id = _editingId, Name = _name.Text, City = _city.Text, Address = _address.Text, Capacity = (int)_capacity.Value };
        if (Ui.Report(_admin.SaveVenue(v), "Venue saved.")) { Load(); }
    }

    private void Delete()
    {
        if (_grid.Selected<Venue>() is not { } v) { Ui.Warn("Select a venue first."); return; }
        if (Ui.Confirm($"Delete '{v.Name}' and its seats?") && Ui.Report(_admin.DeleteVenue(v.Id), "Venue deleted.")) Load();
    }

    private void Seats()
    {
        if (_grid.Selected<Venue>() is not { } v) { Ui.Warn("Select a venue first."); return; }
        using var f = new SeatLayoutForm(v);
        f.ShowDialog(FindForm());
        Load();
    }
}
