using SmartParking.Models.Enums;

namespace SmartParking.DTOs.Booking
{
    public class BookingDetailDto
    {
        public Guid BookingId { get; set; }

        public string LicensePlate { get; set; }

        public string BranchName { get; set; }

        public string BranchAddress { get; set; }

        public string ParkingLotName { get; set; }

        public string ZoneName { get; set; }

        public string SlotCode { get; set; }

        public DateTime BookingTime { get; set; }

        public DateTime ExpiredAt { get; set; }

        public BookingStatus Status { get; set; }

        public ElectronicTicketSummaryDto? ElectronicTicket { get; set; }
    }
}
