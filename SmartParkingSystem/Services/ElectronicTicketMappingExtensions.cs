using SmartParking.DTOs.Booking;
using SmartParking.Models;

namespace SmartParking.Services
{
    public static class ElectronicTicketMappingExtensions
    {
        public static ElectronicTicketSummaryDto ToSummaryDto(this ElectronicTicket ticket)
        {
            return new ElectronicTicketSummaryDto
            {
                TicketId = ticket.Id,
                TicketCode = ticket.TicketCode,
                LicensePlate = ticket.Booking.Vehicle.LicensePlate,
                SlotCode = ticket.Booking.Slot.SlotCode,
                IssuedAt = ticket.IssuedAt,
                ValidUntil = ticket.ValidUntil,
                Status = ticket.Status
            };
        }

        public static ElectronicTicketDetailDto ToDetailDto(this ElectronicTicket ticket)
        {
            return new ElectronicTicketDetailDto
            {
                TicketId = ticket.Id,
                BookingId = ticket.BookingId,
                TicketCode = ticket.TicketCode,
                LicensePlate = ticket.Booking.Vehicle.LicensePlate,
                BranchName = ticket.Booking.Slot.Zone.ParkingLot.Branch.Name,
                BranchAddress = ticket.Booking.Slot.Zone.ParkingLot.Branch.Address,
                ParkingLotName = ticket.Booking.Slot.Zone.ParkingLot.Name,
                ZoneName = ticket.Booking.Slot.Zone.Name,
                SlotCode = ticket.Booking.Slot.SlotCode,
                BookingTime = ticket.Booking.BookingTime,
                IssuedAt = ticket.IssuedAt,
                ValidUntil = ticket.ValidUntil,
                Status = ticket.Status
            };
        }
    }
}
