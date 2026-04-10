namespace SmartParking.DTOs.Booking
{
    public class CreateBookingDto
    {
        public Guid VehicleId { get; set; }

        public Guid SlotId { get; set; }
    }
}
