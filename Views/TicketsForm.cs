using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-22/23: the tickets of a booking with their signed QR codes.</summary>
public class TicketsForm : Form
{
    private readonly DataGridView _grid = Ui.Grid();
    private readonly PictureBox _qr = new() { Size = new Size(260, 260), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
    private readonly Label _caption = new() { AutoSize = false, Size = new Size(260, 70), TextAlign = ContentAlignment.TopCenter };

    public TicketsForm(Booking booking)
    {
        Text = $"Tickets - {booking.Reference}";
        ClientSize = new Size(900, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;

        var tickets = new BookingController().Tickets(booking);
        Ui.Bind(_grid, tickets);

        var save = Ui.Btn("Save QR as image...", (_, _) => SaveQr(), 160);
        var close = Ui.Btn("Close", (_, _) => Close(), 90);
        var side = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 290, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        side.Controls.Add(_qr); side.Controls.Add(_caption); side.Controls.Add(save);

        Controls.Add(_grid);
        Controls.Add(side);
        Controls.Add(Ui.ButtonBar(close));
        _grid.SelectionChanged += (_, _) => ShowQr();
        ShowQr();
    }

    private void ShowQr()
    {
        var old = _qr.Image; _qr.Image = null; old?.Dispose();
        if (_grid.Selected<Ticket>() is not { } t) { _caption.Text = "No tickets."; return; }
        _qr.Image = TicketTokens.QrImage(t.Code, 8);
        _caption.Text = $"{t.EventTitle}\r\n{t.TicketType} - {t.Seat}\r\nStatus: {t.Status}";
    }

    private void SaveQr()
    {
        if (_qr.Image is null || _grid.Selected<Ticket>() is not { } t) return;
        using var dlg = new SaveFileDialog { Filter = "PNG image|*.png", FileName = $"ticket-{t.Reference}-{t.Id}.png" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { _qr.Image.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png); Ui.Info("QR code saved."); }
        catch (Exception ex) when (ex is IOException or System.Runtime.InteropServices.ExternalException) { Ui.Warn("Could not save the image: " + ex.Message); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _qr.Image?.Dispose();
        base.Dispose(disposing);
    }
}
