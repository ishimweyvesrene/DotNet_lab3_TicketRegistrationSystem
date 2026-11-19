using TicketRegistrationDesktop.Data;
using TicketRegistrationDesktop.Models;
using TicketRegistrationDesktop.Services;

namespace TicketRegistrationDesktop.Controllers;

/// <summary>FR-11..24: pricing, booking + payment, my bookings, tickets, cancellation and refunds.</summary>
public class BookingController
{
    private readonly BookingRepository _bookings = new();
    private readonly EventRepository _events = new();
    private readonly CatalogRepository _catalog = new();

    public List<Seat> AvailableSeats(int eventId, int ticketCategoryId) => _events.AvailableSeats(eventId, ticketCategoryId);

    /// <summary>Live price preview for the booking screen. Returns an error for a bad promo code.</summary>
    public Result<(decimal Subtotal, decimal Discount, decimal Fee, decimal Total)> Quote(TicketCategory cat, int quantity, string? promoCode)
    {
        var percent = 0;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            var promo = _catalog.FindActivePromo(promoCode.Trim());
            if (promo is null) return Result<(decimal, decimal, decimal, decimal)>.Fail("This promo code is invalid or has expired.");
            percent = promo.DiscountPercent;
        }
        return Result<(decimal, decimal, decimal, decimal)>.Success(Pricing.Quote(cat.Price, quantity, percent));
    }

    /// <summary>Books and records the payment in one database transaction.</summary>
    public Result<Booking> Book(Event ev, TicketCategory cat, int quantity, IReadOnlyList<int> seatIds, string? promoCode, PaymentMethod method) =>
        _bookings.Create(AppSession.Me.Id, ev.Id, cat.Id, quantity, seatIds, promoCode, method);

    public List<Booking> List(bool scopeAll, string? q, BookingStatus? status) => _bookings.List(AppSession.Me, scopeAll, q, status);

    public List<Ticket> Tickets(Booking booking)
    {
        var me = AppSession.Me;
        var allowed = me.Role == UserRole.Admin || booking.UserId == me.Id ||
                      (me.Role == UserRole.Organizer && _events.Get(booking.EventId)?.OrganizerId == me.Id);
        return allowed ? _bookings.Tickets(booking.Id) : new List<Ticket>();
    }

    public Result Cancel(int bookingId) => _bookings.CancelOrRefund(bookingId, AppSession.Me, refund: false);
    public Result Refund(int bookingId) => _bookings.CancelOrRefund(bookingId, AppSession.Me, refund: true);
}

/// <summary>FR-25: entrance validation of QR codes.</summary>
public class CheckInController
{
    private readonly BookingRepository _bookings = new();

    public CheckInResult Scan(string? payload, int? eventId)
    {
        if (!TicketTokens.TryParse(payload, out var code))
            return new CheckInResult(CheckInStatus.Invalid, "Invalid code. It is not a ticket from this system.");
        return _bookings.CheckIn(code, AppSession.Me, eventId);
    }
}
