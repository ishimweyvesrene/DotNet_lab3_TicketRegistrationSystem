using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

/// <summary>Numbered seat sections of a venue. Rows are lettered A, B, C... and existing seats are kept.</summary>
public class SeatLayoutForm : Form
{
    private readonly AdminController _admin = new();
    private readonly Venue _venue;
    private readonly DataGridView _grid = Ui.Grid();
    private readonly TextBox _section = Ui.Txt(160);
    private readonly NumericUpDown _rows = Ui.Num(1, 60, 5), _perRow = Ui.Num(1, 100, 10);

    public SeatLayoutForm(Venue venue)
    {
        _venue = venue;
        Text = $"Seat layout - {venue.Name}";
        ClientSize = new Size(620, 460);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false; ShowInTaskbar = false;

        var group = new GroupBox { Text = "Add seats to a section", Dock = DockStyle.Bottom, Height = 150 };
        group.Controls.Add(Ui.Form(("Section (e.g. VIP)", _section), ("Rows", _rows), ("Seats per row", _perRow)));
        var buttons = Ui.Bar(DockStyle.Bottom,
            Ui.Btn("Generate seats", (_, _) => Generate(), 130),
            Ui.Btn("Remove selected section", (_, _) => Remove(), 180),
            Ui.Btn("Close", (_, _) => Close(), 80));
        Controls.Add(_grid);
        Controls.Add(buttons);
        Controls.Add(group);
        Load();
    }

    private void Load() => Ui.Bind(_grid, _admin.Sections(_venue.Id));

    private void Generate()
    {
        var r = _admin.GenerateSeats(_venue.Id, _section.Text, (int)_rows.Value, (int)_perRow.Value);
        if (!Ui.Report(r)) return;
        Ui.Info($"{r.Value} seat(s) added.");
        Load();
    }

    private void Remove()
    {
        if (_grid.Selected<SectionInfo>() is not { } s) { Ui.Warn("Select a section first."); return; }
        if (Ui.Confirm($"Remove section '{s.Section}' and all its seats?") && Ui.Report(_admin.RemoveSection(_venue.Id, s.Section), "Section removed.")) Load();
    }
}
