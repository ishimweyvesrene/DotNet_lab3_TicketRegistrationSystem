using TicketRegistrationDesktop.Controllers;

namespace TicketRegistrationDesktop.Views;

public class RegisterForm : Form
{
    private readonly AuthController _auth = new();
    private readonly TextBox _name = Ui.Txt(240), _email = Ui.Txt(240), _phone = Ui.Txt(240);
    private readonly TextBox _pw = Ui.Txt(240, true), _pw2 = Ui.Txt(240, true);
    public string RegisteredEmail { get; private set; } = "";

    public RegisterForm()
    {
        Text = "Create your account";
        ClientSize = new Size(420, 290);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;

        var save = Ui.Btn("Register", (_, _) => Save(), 100);
        var cancel = Ui.Btn("Cancel", (_, _) => DialogResult = DialogResult.Cancel, 100);
        Controls.Add(Ui.Form(("Full name", _name), ("Email", _email), ("Phone", _phone),
                             ("Password (8+ chars)", _pw), ("Confirm password", _pw2)));
        Controls.Add(Ui.ButtonBar(cancel, save));
        AcceptButton = save; CancelButton = cancel;
    }

    private void Save()
    {
        if (!Ui.Report(_auth.Register(_name.Text, _email.Text, _phone.Text, _pw.Text, _pw2.Text))) return;
        RegisteredEmail = _email.Text.Trim();
        DialogResult = DialogResult.OK;
    }
}
