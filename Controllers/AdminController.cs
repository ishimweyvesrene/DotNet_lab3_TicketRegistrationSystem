using TicketRegistrationDesktop.Data;
using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Controllers;

/// <summary>FR-32, FR-35, FR-36: venues + seats, event categories, promo codes, users.</summary>
public class AdminController
{
    private readonly CatalogRepository _catalog = new();
    private readonly UserRepository _users = new();

    private static Result Guard() => AppSession.IsAdmin ? Result.Success() : Result.Fail("Only an admin can do this.");

    // ------------------------------------------------------------ venues and seats
    public List<Venue> Venues() => _catalog.Venues();
    public List<SectionInfo> Sections(int venueId) => _catalog.Sections(venueId);

    public Result SaveVenue(Venue v)
    {
        var g = Guard(); if (!g.Ok) return g;
        v.Name = (v.Name ?? "").Trim(); v.City = (v.City ?? "").Trim(); v.Address = v.Address?.Trim();
        if (v.Name.Length == 0 || v.City.Length == 0) return Result.Fail("Enter the venue name and city.");
        if (v.Capacity < 1) return Result.Fail("Capacity must be at least 1.");
        _catalog.SaveVenue(v);
        return Result.Success();
    }

    public Result DeleteVenue(int id) { var g = Guard(); return g.Ok ? _catalog.DeleteVenue(id) : g; }

    public Result<int> GenerateSeats(int venueId, string section, int rows, int perRow)
    {
        if (!AppSession.IsAdmin) return Result<int>.Fail("Only an admin can do this.");
        section = (section ?? "").Trim();
        if (section.Length == 0) return Result<int>.Fail("Enter a section name.");
        if (rows < 1 || rows > 60 || perRow < 1 || perRow > 100) return Result<int>.Fail("Rows must be 1-60 and seats per row 1-100.");
        return Result<int>.Success(_catalog.GenerateSeats(venueId, section, rows, perRow));
    }

    public Result RemoveSection(int venueId, string section) { var g = Guard(); return g.Ok ? _catalog.RemoveSection(venueId, section) : g; }

    // ------------------------------------------------------------ event categories
    public List<EventCategory> Categories() => _catalog.Categories();

    public Result SaveCategory(int id, string name)
    {
        var g = Guard(); if (!g.Ok) return g;
        name = (name ?? "").Trim();
        if (name.Length == 0) return Result.Fail("Enter a category name.");
        return _catalog.SaveCategory(new EventCategory { Id = id, Name = name });
    }

    public Result DeleteCategory(int id) { var g = Guard(); return g.Ok ? _catalog.DeleteCategory(id) : g; }

    // ------------------------------------------------------------ promo codes
    public List<PromoCode> Promos() => _catalog.Promos();

    public Result SavePromo(PromoCode p)
    {
        var g = Guard(); if (!g.Ok) return g;
        p.Code = (p.Code ?? "").Trim();
        if (p.Code.Length < 3) return Result.Fail("The code needs at least 3 characters.");
        if (p.DiscountPercent < 1 || p.DiscountPercent > 100) return Result.Fail("Discount must be 1-100 percent.");
        return _catalog.SavePromo(p);
    }

    public Result<string> DeletePromo(int id)
    {
        if (!AppSession.IsAdmin) return Result<string>.Fail("Only an admin can do this.");
        return Result<string>.Success(_catalog.DeletePromo(id));
    }

    // ------------------------------------------------------------ users
    public List<User> Users(string? q) => _users.Search(q);

    public Result SetActive(User u, bool active)
    {
        var g = Guard(); if (!g.Ok) return g;
        if (u.Id == AppSession.Me.Id) return Result.Fail("You cannot deactivate your own account.");
        _users.SetActive(u.Id, active);
        return Result.Success();
    }

    public Result SetRole(User u, UserRole role)
    {
        var g = Guard(); if (!g.Ok) return g;
        if (u.Id == AppSession.Me.Id) return Result.Fail("You cannot change your own role.");
        _users.SetRole(u.Id, role);
        return Result.Success();
    }
}
