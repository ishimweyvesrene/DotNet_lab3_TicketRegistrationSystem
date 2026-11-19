using System.Net.Mail;
using TicketRegistrationDesktop.Data;
using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Controllers;

/// <summary>FR-1..FR-5: registration, login, profile.</summary>
public class AuthController
{
    private readonly UserRepository _users = new();

    public static bool IsValidEmail(string email)
    {
        try { return new MailAddress(email).Address == email.Trim(); }
        catch (FormatException) { return false; }
    }

    public Result<User> Login(string email, string password)
    {
        email = (email ?? "").Trim();
        if (email.Length == 0 || string.IsNullOrEmpty(password)) return Result<User>.Fail("Enter your email and password.");
        var user = _users.FindByEmail(email);
        if (user is null || !user.IsActive || !Passwords.Verify(password, user.PasswordHash))
            return Result<User>.Fail("Invalid email or password.");
        AppSession.CurrentUser = user;
        return Result<User>.Success(user);
    }

    public Result Register(string fullName, string email, string phone, string password, string confirm)
    {
        fullName = (fullName ?? "").Trim(); email = (email ?? "").Trim(); phone = (phone ?? "").Trim();
        if (fullName.Length < 2) return Result.Fail("Enter your full name.");
        if (!IsValidEmail(email)) return Result.Fail("Enter a valid email address.");
        if (phone.Count(char.IsDigit) < 7) return Result.Fail("Enter a valid phone number.");
        if (password is null || password.Length < 8) return Result.Fail("The password must have at least 8 characters.");
        if (password != confirm) return Result.Fail("The passwords do not match.");
        if (_users.EmailExists(email)) return Result.Fail("An account with this email already exists.");
        _users.Create(fullName, email, phone, Passwords.Hash(password), UserRole.Attendee);   // admins promote organizers
        return Result.Success();
    }

    public Result UpdateProfile(string fullName, string phone)
    {
        fullName = (fullName ?? "").Trim(); phone = (phone ?? "").Trim();
        if (fullName.Length < 2) return Result.Fail("Enter your full name.");
        if (phone.Count(char.IsDigit) < 7) return Result.Fail("Enter a valid phone number.");
        var me = AppSession.Me;
        _users.UpdateProfile(me.Id, fullName, phone);
        me.FullName = fullName; me.Phone = phone;
        return Result.Success();
    }

    public Result ChangePassword(string oldPassword, string newPassword)
    {
        var me = AppSession.Me;
        var fresh = _users.FindById(me.Id);
        if (fresh is null || !Passwords.Verify(oldPassword ?? "", fresh.PasswordHash)) return Result.Fail("The current password is wrong.");
        if (newPassword is null || newPassword.Length < 8) return Result.Fail("The new password must have at least 8 characters.");
        _users.UpdatePassword(me.Id, Passwords.Hash(newPassword));
        return Result.Success();
    }
}
