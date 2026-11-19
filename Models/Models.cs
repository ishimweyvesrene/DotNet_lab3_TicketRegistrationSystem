using System.ComponentModel;

namespace TicketRegistrationDesktop.Models;

public enum UserRole { Admin, Organizer, Attendee }
public enum EventStatus { Pending, Published, Cancelled }
public enum BookingStatus { Confirmed, Cancelled, Refunded }
public enum TicketStatus { Valid, Used, Cancelled }
public enum PaymentMethod { Cash, Card, MobileMoney }
public enum CheckInStatus { Valid, AlreadyUsed, Invalid, Cancelled, WrongEvent, Forbidden }

/// <summary>Result of a controller operation, shown to the user by the view.</summary>
public record Result(bool Ok, string? Error = null)
{
    public static Result Success() => new(true);
    public static Result Fail(string error) => new(false, error);
}

public record Result<T>(bool Ok, T? Value, string? Error = null) : Result(Ok, Error)
{
    public static Result<T> Success(T value) => new(true, value);
    public new static Result<T> Fail(string error) => new(false, default, error);
}

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    [Browsable(false)] public string PasswordHash { get; set; } = "";
}

public class Venue
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string? Address { get; set; }
    public int Capacity { get; set; }
    public int SeatCount { get; set; }
    public override string ToString() => $"{Name} ({City})";
}

public class Seat
{
    public int Id { get; set; }
    [Browsable(false)] public int VenueId { get; set; }
    public string Section { get; set; } = "";
    public string RowLabel { get; set; } = "";
    public int SeatNumber { get; set; }
    [Browsable(false)] public string Label => $"{Section} {RowLabel}{SeatNumber}";
    public override string ToString() => Label;
}

public class SectionInfo
{
    public string Section { get; set; } = "";
    public int Rows { get; set; }
    public int Seats { get; set; }
    public override string ToString() => Section;
}

public class EventCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}

public class PromoCode
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int DiscountPercent { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; }
}

public class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    [Browsable(false)] public string Description { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    [Browsable(false)] public int VenueId { get; set; }
    public string VenueName { get; set; } = "";
    public string City { get; set; } = "";
    [Browsable(false)] public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    [Browsable(false)] public int OrganizerId { get; set; }
    public string OrganizerName { get; set; } = "";
    public EventStatus Status { get; set; }
    [Browsable(false)] public int MaxTicketsPerBooking { get; set; } = 6;
    [Browsable(false)] public int CancellationDeadlineHours { get; set; } = 24;
    public decimal? FromPrice { get; set; }
    public int Sold { get; set; }
    public override string ToString() => $"{Title} - {StartDate:dd MMM yyyy HH:mm}";
}

public class TicketCategory
{
    public int Id { get; set; }
    [Browsable(false)] public int EventId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? Section { get; set; }
    public int Sold { get; set; }
    public int Available => Math.Max(0, Quantity - Sold);
    [Browsable(false)] public bool IsNumbered => !string.IsNullOrWhiteSpace(Section);
    public override string ToString() => $"{Name} - RWF {Price:N0} ({Available} left{(IsNumbered ? ", numbered seats" : "")})";
}

public class Booking
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    [Browsable(false)] public int UserId { get; set; }
    public string Customer { get; set; } = "";
    [Browsable(false)] public int EventId { get; set; }
    public string EventTitle { get; set; } = "";
    public DateTime EventStart { get; set; }
    public string TicketType { get; set; } = "";
    public int Quantity { get; set; }
    public BookingStatus Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }
    public DateTime BookedOn { get; set; }
}

public class Ticket
{
    public int Id { get; set; }
    [Browsable(false)] public int BookingId { get; set; }
    public string Reference { get; set; } = "";
    public string EventTitle { get; set; } = "";
    public DateTime EventStart { get; set; }
    public string Venue { get; set; } = "";
    public string TicketType { get; set; } = "";
    public string Seat { get; set; } = "General admission";
    public TicketStatus Status { get; set; }
    public DateTime? CheckedInAt { get; set; }
    [Browsable(false)] public string Code { get; set; } = "";
}

public class SalesRow
{
    public string Event { get; set; } = "";
    public string Category { get; set; } = "";
    public int Bookings { get; set; }
    public int Tickets { get; set; }
    public decimal Revenue { get; set; }
}

public class Totals
{
    public decimal Sales { get; set; }
    public int Bookings { get; set; }
    public int TicketsSold { get; set; }
    public int ActiveEvents { get; set; }
    public int Users { get; set; }
}

public record CheckInResult(CheckInStatus Status, string Message, string? Holder = null, string? Detail = null);
