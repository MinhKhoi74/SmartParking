using SmartParking.Models.Enums;
using SmartParking.Models.Identity;

namespace SmartParking.Models
{
    public class Booking
    {
        public Guid Id { get; set; }

        public string UserId { get; set; }

        public Guid VehicleId { get; set; }

        public Guid SlotId { get; set; }

        public DateTime BookingTime { get; set; }

        public DateTime ExpiredAt { get; set; }

        public BookingStatus Status { get; set; }

        public ApplicationUser User { get; set; }

        public Vehicle Vehicle { get; set; }

        public Slot Slot { get; set; }

        public ElectronicTicket? ElectronicTicket { get; set; }
    }
}
