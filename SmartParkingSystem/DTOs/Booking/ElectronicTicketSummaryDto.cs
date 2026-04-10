using SmartParking.Models.Enums;

namespace SmartParking.DTOs.Booking
{
    public class ElectronicTicketSummaryDto
    {
        public Guid TicketId { get; set; }

        public string TicketCode { get; set; }

        public string LicensePlate { get; set; }

        public string SlotCode { get; set; }

        public DateTime IssuedAt { get; set; }

        public DateTime ValidUntil { get; set; }

        public BookingStatus Status { get; set; }
    }
}
