using TicketRegistrationDesktop.Controllers;

namespace TicketRegistrationDesktop.Views;

public class LoginForm : Form
{
    private readonly AuthController _auth = new();
    private readonly TextBox _email = Ui.Txt(240);
    private readonly TextBox _password = Ui.Txt(240, true);

    public LoginForm()
    {
        Text = Ui.AppName + " - Sign in";
        ClientSize = new Size(380, 250);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var title = new Label
        {
            Text = "Ticket Registration System", Dock = DockStyle.Top, Height = 64, TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 15f, FontStyle.Bold)
        };
        var login = Ui.Btn("Sign in", (_, _) => DoLogin(), 100);
        var register = Ui.Btn("Create account", (_, _) => DoRegister(), 130);

        Controls.Add(Ui.Form(("Email", _email), ("Password", _password)));
        Controls.Add(title);
        Controls.Add(Ui.ButtonBar(login, register));
        AcceptButton = login;
    }

    private void DoLogin()
    {
        var r = _auth.Login(_email.Text, _password.Text);
        if (!Ui.Report(r)) { _password.Clear(); _password.Focus(); return; }
        DialogResult = DialogResult.OK;
    }

    private void DoRegister()
    {
        using var f = new RegisterForm();
        if (f.ShowDialog(this) == DialogResult.OK)
        {
            Ui.Info("Your account was created. You can sign in now.");
            _email.Text = f.RegisteredEmail;
            _password.Focus();
        }
    }
}
