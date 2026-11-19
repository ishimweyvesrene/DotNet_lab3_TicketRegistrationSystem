using TicketRegistrationDesktop.Data;
using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Controllers;

/// <summary>FR-6..10 catalogue and FR-28..33 event management.</summary>
public class EventController
{
    private readonly EventRepository _events = new();
    private readonly CatalogRepository _catalog = new();

    // ---------------------------------------------------------------- lookups
    public List<EventCategory> Categories() => _catalog.Categories();
    public List<Venue> Venues() => _catalog.Venues();
    public List<string> Cities() => _events.Cities();
    public List<SectionInfo> Sections(int venueId) => _catalog.Sections(venueId);

    // ---------------------------------------------------------------- catalogue
    public List<Event> Browse(string? q, int? categoryId, string? city, DateTime? from, DateTime? to,
                              decimal? minPrice, decimal? maxPrice, string sort) =>
        _events.Browse(q, categoryId, city, from, to, minPrice, maxPrice, sort);

    public List<TicketCategory> TicketCategories(int eventId) => _events.TicketCategories(eventId);

    // ---------------------------------------------------------------- management
    public List<Event> ManagedEvents() => _events.ForManagement(AppSession.Me);
    public Event? Get(int id) => _events.Get(id);

    private static bool CanManage(Event e) => AppSession.IsAdmin || (AppSession.IsOrganizer && e.OrganizerId == AppSession.Me.Id);

    public Result<Event> Save(Event e)
    {
        var me = AppSession.Me;
        if (me.Role == UserRole.Attendee) return Result<Event>.Fail("Only organizers and admins can manage events.");
        e.Title = (e.Title ?? "").Trim(); e.Description = (e.Description ?? "").Trim();
        if (e.Title.Length < 3) return Result<Event>.Fail("Enter an event title.");
        if (e.Description.Length < 5) return Result<Event>.Fail("Enter a description.");
        if (e.EndDate <= e.StartDate) return Result<Event>.Fail("The end must be after the start.");
        if (e.VenueId == 0) return Result<Event>.Fail("Select a venue.");
        if (e.CategoryId == 0) return Result<Event>.Fail("Select a category.");
        if (e.MaxTicketsPerBooking < 1 || e.MaxTicketsPerBooking > 50) return Result<Event>.Fail("Max tickets per booking must be 1-50.");
        if (e.CancellationDeadlineHours < 0 || e.CancellationDeadlineHours > 720) return Result<Event>.Fail("Cancellation hours must be 0-720.");

        if (e.Id == 0)
        {
            e.OrganizerId = me.Id;
            e.Status = me.Role == UserRole.Admin ? EventStatus.Published : EventStatus.Pending;   // organizers need approval
        }
        else
        {
            var old = _events.Get(e.Id);
            if (old is null) return Result<Event>.Fail("Event not found.");
            if (!CanManage(old)) return Result<Event>.Fail("You cannot edit this event.");
            if (old.Status == EventStatus.Cancelled) return Result<Event>.Fail("A cancelled event cannot be edited.");
            if (old.VenueId != e.VenueId)
            {
                var numbered = _events.TicketCategories(old.Id).Any(t => t.IsNumbered);
                if (numbered || _events.HasBookings(old.Id))
                    return Result<Event>.Fail("The venue cannot change once bookings or numbered-seat ticket types exist.");
            }
        }
        _events.Save(e);
        return Result<Event>.Success(e);
    }

    public Result Cancel(int eventId)
    {
        var e = _events.Get(eventId);
        if (e is null) return Result.Fail("Event not found.");
        if (!CanManage(e)) return Result.Fail("You cannot cancel this event.");
        _events.SetStatus(eventId, EventStatus.Cancelled);   // attendees can then request refunds
        return Result.Success();
    }

    public Result Approve(int eventId)
    {
        if (!AppSession.IsAdmin) return Result.Fail("Only an admin can approve events.");
        _events.SetStatus(eventId, EventStatus.Published);
        return Result.Success();
    }

    /// <summary>FR-33: admin removes an event. With bookings it is cancelled instead of deleted so refunds stay possible.</summary>
    public Result<string> Remove(int eventId)
    {
        if (!AppSession.IsAdmin) return Result<string>.Fail("Only an admin can remove events.");
        if (_events.HasBookings(eventId))
        {
            _events.SetStatus(eventId, EventStatus.Cancelled);
            return Result<string>.Success("The event had bookings, so it was cancelled (attendees can request refunds).");
        }
        _events.Delete(eventId);
        return Result<string>.Success("Event removed.");
    }

    // ---------------------------------------------------------------- ticket types (FR-29)
    public Result SaveTicketCategory(int eventId, int id, string name, decimal price, int quantity, string? section)
    {
        var ev = _events.Get(eventId);
        if (ev is null) return Result.Fail("Event not found.");
        if (!CanManage(ev)) return Result.Fail("You cannot change this event's tickets.");
        name = (name ?? "").Trim();
        if (name.Length == 0) return Result.Fail("Enter a name (for example Regular or VIP).");
        if (price < 0) return Result.Fail("The price cannot be negative.");
        section = string.IsNullOrWhiteSpace(section) ? null : section.Trim();

        TicketCategory? current = id == 0 ? null : _events.TicketCategories(eventId).FirstOrDefault(t => t.Id == id);
        if (id != 0 && current is null) return Result.Fail("Ticket type not found.");

        if (section is not null)
        {
            var seatCount = _events.SeatCount(ev.VenueId, section);
            if (seatCount == 0) return Result.Fail($"The venue has no seats in section '{section}'.");
            if (_events.SectionUsed(eventId, section, id)) return Result.Fail($"Another ticket type already uses section '{section}'.");
            quantity = seatCount;
        }
        else if (quantity < 1) return Result.Fail("General admission needs a quantity of at least 1.");

        if (current is not null)
        {
            if (_events.CategoryHasBookings(id) && current.Section != section) return Result.Fail("Seating type cannot change once bookings exist.");
            if (quantity < current.Sold) return Result.Fail($"{current.Sold} ticket(s) are already sold; the quantity cannot be lower.");
        }
        var venue = _catalog.Venues().First(v => v.Id == ev.VenueId);
        var total = _events.OtherQuantity(eventId, id) + quantity;
        if (total > venue.Capacity) return Result.Fail($"Total tickets ({total}) would exceed the venue capacity ({venue.Capacity}).");

        _events.SaveTicketCategory(new TicketCategory { Id = id, EventId = eventId, Name = name, Price = price, Quantity = quantity, Section = section });
        return Result.Success();
    }

    public Result DeleteTicketCategory(int eventId, int id)
    {
        var ev = _events.Get(eventId);
        if (ev is null || !CanManage(ev)) return Result.Fail("You cannot change this event's tickets.");
        if (_events.CategoryHasBookings(id)) return Result.Fail("This ticket type already has bookings and cannot be deleted.");
        _events.DeleteTicketCategory(id);
        return Result.Success();
    }
}
