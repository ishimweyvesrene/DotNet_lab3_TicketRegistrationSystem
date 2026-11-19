using TicketRegistrationDesktop.Controllers;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Views;

/// <summary>FR-5: update profile and change password.</summary>
public class ProfileForm : Form
{
    private readonly AuthController _auth = new();
    private readonly TextBox _name = Ui.Txt(240), _phone = Ui.Txt(240);
    private readonly TextBox _old = Ui.Txt(240, true), _new = Ui.Txt(240, true);

    public ProfileForm()
    {
        Text = "My profile";
        ClientSize = new Size(440, 330);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;

        var me = AppSession.Me;
        _name.Text = me.FullName; _phone.Text = me.Phone;
        var email = Ui.Txt(240); email.Text = me.Email; email.ReadOnly = true;

        var saveProfile = Ui.Btn("Save profile", (_, _) =>
        {
            if (Ui.Report(_auth.UpdateProfile(_name.Text, _phone.Text), "Profile updated.")) Text = "My profile";
        }, 120);
        var changePw = Ui.Btn("Change password", (_, _) =>
        {
            if (Ui.Report(_auth.ChangePassword(_old.Text, _new.Text), "Password changed.")) { _old.Clear(); _new.Clear(); }
        }, 130);
        var close = Ui.Btn("Close", (_, _) => Close(), 90);

        var pwGroup = new GroupBox { Text = "Change password", Dock = DockStyle.Top, Height = 120 };
        pwGroup.Controls.Add(Ui.Form(("Current password", _old), ("New password (8+)", _new)));
        var profileGroup = new GroupBox { Text = "Profile", Dock = DockStyle.Top, Height = 130 };
        profileGroup.Controls.Add(Ui.Form(("Email", email), ("Full name", _name), ("Phone", _phone)));

        Controls.Add(pwGroup);
        Controls.Add(profileGroup);
        Controls.Add(Ui.ButtonBar(close, changePw, saveProfile));
    }
}
