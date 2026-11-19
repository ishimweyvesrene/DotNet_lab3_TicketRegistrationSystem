using TicketRegistrationDesktop.Data;
using TicketRegistrationDesktop.Services;
using TicketRegistrationDesktop.Views;

namespace TicketRegistrationDesktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            AppSettings.Load();
            DatabaseInitializer.EnsureReady();   // creates the MySQL database + tables on first run
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The application could not reach MySQL.\n\n" + ex.Message +
                "\n\nCheck that the MySQL service is running and that Host, User and Password in appsettings.json are correct.",
                "Database error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        while (true)
        {
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() != DialogResult.OK) break;
            }
            var main = new MainForm();
            Application.Run(main);
            var loggedOut = main.LoggedOut;
            main.Dispose();
            AppSession.CurrentUser = null;
            if (!loggedOut) break;
        }
    }
}
