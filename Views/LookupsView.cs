using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-32 / FR-35: event categories and promo codes (admin).</summary>
public class LookupsView : UserControl, IRefreshable
{
    private readonly AdminController _admin = new();
    private readonly ListBox _categories = new() { Dock = DockStyle.Fill };
    private readonly TextBox _catName = Ui.Txt(200);
    private readonly DataGridView _promos = Ui.Grid();
    private readonly TextBox _code = Ui.Txt(140);
    private readonly NumericUpDown _percent = Ui.Num(1, 100, 10);
    private readonly DateTimePicker _expiry = Ui.Picker(false, DateTime.Today.AddMonths(1), 140);
    private readonly CheckBox _active = new() { Text = "Active", Checked = true, AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
    private int _promoId;

    public LookupsView()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

        var catBox = new GroupBox { Text = "Event categories", Dock = DockStyle.Fill };
        catBox.Controls.Add(_categories);
        catBox.Controls.Add(Ui.Bar(DockStyle.Bottom, _catName,
            Ui.Btn("Save", (_, _) => SaveCategory(), 70), Ui.Btn("New", (_, _) => { _categories.ClearSelected(); _catName.Clear(); }, 60),
            Ui.Btn("Delete", (_, _) => DeleteCategory(), 70)));

        var promoBox = new GroupBox { Text = "Promo codes", Dock = DockStyle.Fill };
        promoBox.Controls.Add(_promos);
        promoBox.Controls.Add(Ui.Bar(DockStyle.Bottom, Ui.Lbl("Code"), _code, Ui.Lbl("Discount %"), _percent, Ui.Lbl("Expires"), _expiry, _active));
        promoBox.Controls.Add(Ui.Bar(DockStyle.Bottom,
            Ui.Btn("Save code", (_, _) => SavePromo(), 100), Ui.Btn("New", (_, _) => ClearPromo(), 70), Ui.Btn("Delete", (_, _) => DeletePromo(), 80)));

        layout.Controls.Add(catBox, 0, 0);
        layout.Controls.Add(promoBox, 1, 0);
        Controls.Add(layout);

        _categories.SelectedIndexChanged += (_, _) => { if (_categories.SelectedItem is EventCategory c) _catName.Text = c.Name; };
        _promos.SelectionChanged += (_, _) =>
        {
            if (_promos.Selected<PromoCode>() is not { } p) return;
            _promoId = p.Id; _code.Text = p.Code; _percent.Value = Math.Clamp(p.DiscountPercent, 1, 100);
            _expiry.Value = p.ExpiryDate; _active.Checked = p.IsActive;
        };
        Load();
    }

    public void RefreshData() => Load();

    private void Load()
    {
        _categories.Items.Clear();
        foreach (var c in _admin.Categories()) _categories.Items.Add(c);
        Ui.Bind(_promos, _admin.Promos());
    }

    private void SaveCategory()
    {
        var id = (_categories.SelectedItem as EventCategory)?.Id ?? 0;
        if (Ui.Report(_admin.SaveCategory(id, _catName.Text), "Category saved.")) Load();
    }

    private void DeleteCategory()
    {
        if (_categories.SelectedItem is not EventCategory c) { Ui.Warn("Select a category first."); return; }
        if (Ui.Confirm($"Delete '{c.Name}'?") && Ui.Report(_admin.DeleteCategory(c.Id), "Category deleted.")) { _catName.Clear(); Load(); }
    }

    private void ClearPromo() { _promos.ClearSelection(); _promoId = 0; _code.Clear(); _percent.Value = 10; _expiry.Value = DateTime.Today.AddMonths(1); _active.Checked = true; _code.Focus(); }

    private void SavePromo()
    {
        var p = new PromoCode { Id = _promoId, Code = _code.Text, DiscountPercent = (int)_percent.Value, ExpiryDate = _expiry.Value.Date, IsActive = _active.Checked };
        if (Ui.Report(_admin.SavePromo(p), "Promo code saved.")) { _promoId = 0; Load(); }
    }

    private void DeletePromo()
    {
        if (_promos.Selected<PromoCode>() is not { } p) { Ui.Warn("Select a promo code first."); return; }
        if (!Ui.Confirm($"Delete '{p.Code}'?")) return;
        var r = _admin.DeletePromo(p.Id);
        if (Ui.Report(r)) { Ui.Info(r.Value!); _promoId = 0; Load(); }
    }
}
