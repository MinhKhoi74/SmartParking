using SmartParking.Models.Enums;

namespace SmartParking.DTOs.Booking
{
    public class ElectronicTicketDetailDto
    {
        public Guid TicketId { get; set; }

        public Guid BookingId { get; set; }

        public string TicketCode { get; set; }

        public string LicensePlate { get; set; }

        public string BranchName { get; set; }

        public string BranchAddress { get; set; }

        public string ParkingLotName { get; set; }

        public string ZoneName { get; set; }

        public string SlotCode { get; set; }

        public DateTime BookingTime { get; set; }

        public DateTime IssuedAt { get; set; }

        public DateTime ValidUntil { get; set; }

        public BookingStatus Status { get; set; }
    }
}
