using SmartParking.Models.Enums;

namespace SmartParking.Models
{
    public class ElectronicTicket
    {
        public Guid Id { get; set; }

        public Guid BookingId { get; set; }

        public string TicketCode { get; set; }

        public DateTime IssuedAt { get; set; }

        public DateTime ValidUntil { get; set; }

        public BookingStatus Status { get; set; }

        public Booking Booking { get; set; }
    }
}
