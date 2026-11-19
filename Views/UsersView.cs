using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-30 / FR-36: view users, assign roles, activate or deactivate accounts (admin).</summary>
public class UsersView : UserControl, IRefreshable
{
    private readonly AdminController _admin = new();
    private readonly TextBox _search = Ui.Txt(220);
    private readonly ComboBox _role = Ui.Combo(130);
    private readonly DataGridView _grid = Ui.Grid();

    public UsersView()
    {
        foreach (var r in Enum.GetValues<UserRole>()) _role.Items.Add(r);
        _role.SelectedItem = UserRole.Organizer;

        var filters = Ui.Bar(DockStyle.Top, Ui.Lbl("Search"), _search, Ui.Btn("Search", (_, _) => Load(), 90));
        var buttons = Ui.Bar(DockStyle.Bottom, Ui.Lbl("Role"), _role, Ui.Btn("Set role", (_, _) => SetRole(), 100),
            Ui.Btn("Activate", (_, _) => SetActive(true), 100), Ui.Btn("Deactivate", (_, _) => SetActive(false), 100));
        Controls.Add(_grid);
        Controls.Add(buttons);
        Controls.Add(filters);
        _search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Load(); };
        Load();
    }

    public void RefreshData() => Load();
    private void Load() => Ui.Bind(_grid, _admin.Users(_search.Text));

    private void SetRole()
    {
        if (_grid.Selected<User>() is not { } u) { Ui.Warn("Select a user first."); return; }
        if (Ui.Report(_admin.SetRole(u, (UserRole)_role.SelectedItem!), $"{u.FullName} is now {_role.SelectedItem}.")) Load();
    }

    private void SetActive(bool active)
    {
        if (_grid.Selected<User>() is not { } u) { Ui.Warn("Select a user first."); return; }
        if (Ui.Report(_admin.SetActive(u, active), active ? "Account activated." : "Account deactivated.")) Load();
    }
}
