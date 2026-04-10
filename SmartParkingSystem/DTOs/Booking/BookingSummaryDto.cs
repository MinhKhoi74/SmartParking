using SmartParking.Models.Enums;

namespace SmartParking.DTOs.Booking
{
    public class BookingSummaryDto
    {
        public Guid BookingId { get; set; }

        public string LicensePlate { get; set; }

        public string SlotCode { get; set; }

        public DateTime BookingTime { get; set; }

        public DateTime ExpiredAt { get; set; }

        public BookingStatus Status { get; set; }
    }
}
