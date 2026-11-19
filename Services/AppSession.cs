using TicketRegistrationDesktop.Models;

namespace TicketRegistrationDesktop.Services;

/// <summary>The signed-in user for the running application.</summary>
public static class AppSession
{
    public static User? CurrentUser { get; set; }
    public static User Me => CurrentUser ?? throw new InvalidOperationException("Nobody is signed in.");
    public static bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
    public static bool IsOrganizer => CurrentUser?.Role == UserRole.Organizer;
}
