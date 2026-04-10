using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartParking.Data;
using SmartParking.DTOs.Booking;
using SmartParking.Models;
using SmartParking.Models.Enums;
using SmartParking.Services.Interfaces;
using SmartParking.SignalR;

namespace SmartParking.Services
{
    public class BookingService : IBookingService
    {
        private readonly ApplicationDBContext _context;
        private readonly IHubContext<ParkingHub> _hub;

        public BookingService(ApplicationDBContext context, IHubContext<ParkingHub> hub)
        {
            _context = context;
            _hub = hub;
        }

        public async Task<BookingDetailDto> CreateBookingAsync(string userId, CreateBookingDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            var vehicle = await _context.Vehicle
                .FirstOrDefaultAsync(v => v.Id == dto.VehicleId && v.UserId == userId && v.IsActive);
            var slot = await _context.Slots
                .Include(x => x.Zone)
                    .ThenInclude(x => x.ParkingLot)
                        .ThenInclude(x => x.Branch)
                .FirstOrDefaultAsync(x => x.Id == dto.SlotId);

            if (vehicle == null)
                throw new Exception("Vehicle not found");

            if (slot == null)
                throw new Exception("Slot not found");

            if (slot.Status != SlotStatus.Available)
                throw new Exception("Slot unavailable");

            var activeBooking = await _context.Bookings
                .AnyAsync(x =>
                    x.SlotId == dto.SlotId &&
                    (x.Status == BookingStatus.Pending ||
                     x.Status == BookingStatus.Confirmed));

            if (activeBooking)
                throw new Exception("Slot already booked");

            slot.Status = SlotStatus.Reserved;

            var booking = new Booking
            {
                UserId = userId,
                VehicleId = dto.VehicleId,
                SlotId = dto.SlotId,
                BookingTime = DateTime.UtcNow,
                ExpiredAt = DateTime.UtcNow.AddMinutes(15),
                Status = BookingStatus.Pending,
                Vehicle = vehicle,
                Slot = slot
            };

            _context.Bookings.Add(booking);

            var electronicTicket = new ElectronicTicket
            {
                Booking = booking,
                TicketCode = GenerateTicketCode(),
                IssuedAt = DateTime.UtcNow,
                ValidUntil = booking.ExpiredAt,
                Status = booking.Status
            };

            _context.ElectronicTickets.Add(electronicTicket);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            await _hub.Clients.All.SendAsync("SlotUpdated", dto.SlotId);

            return new BookingDetailDto
            {
                BookingId = booking.Id,
                LicensePlate = vehicle.LicensePlate,
                BranchName = slot.Zone.ParkingLot.Branch.Name,
                BranchAddress = slot.Zone.ParkingLot.Branch.Address,
                ParkingLotName = slot.Zone.ParkingLot.Name,
                ZoneName = slot.Zone.Name,
                SlotCode = slot.SlotCode,
                BookingTime = booking.BookingTime,
                ExpiredAt = booking.ExpiredAt,
                Status = booking.Status,
                ElectronicTicket = electronicTicket.ToSummaryDto()
            };
        }

        public async Task CancelBookingAsync(Guid bookingId, string userId)
        {
            var booking = await _context.Bookings
                .Include(x => x.Slot)
                .Include(x => x.ElectronicTicket)
                .FirstOrDefaultAsync(x => x.Id == bookingId && x.UserId == userId);

            if (booking == null)
                throw new Exception("Booking not found");

            booking.Status = BookingStatus.Cancelled;
            booking.Slot.Status = SlotStatus.Available;
            UpdateTicketStatus(booking);

            await _context.SaveChangesAsync();

            await _hub.Clients.All.SendAsync("SlotUpdated", booking.SlotId);
        }
        public async Task<List<BookingSummaryDto>> GetAllBookingsAsync()
        {
            var bookings = await _context.Bookings
                .Include(x => x.Vehicle)
                .Include(x => x.Slot)
                    .ThenInclude(x => x.Zone)
                        .ThenInclude(x => x.ParkingLot)
                            .ThenInclude(x => x.Branch)
                .Include(x => x.ElectronicTicket)
                .OrderByDescending(x => x.BookingTime)
                .ToListAsync();

            return bookings.Select(x => new BookingSummaryDto
                {
                    BookingId = x.Id,
                    LicensePlate = x.Vehicle.LicensePlate,
                    SlotCode = x.Slot.SlotCode,
                    BookingTime = x.BookingTime,
                    ExpiredAt = x.ExpiredAt,
                    Status = x.Status
                })
                .ToList();
        }
        public async Task<List<BookingSummaryDto>> GetMyBookingsAsync(string userId)
        {
            var bookings = await _context.Bookings
                .Include(x => x.Vehicle)
                .Include(x => x.Slot)
                    .ThenInclude(x => x.Zone)
                        .ThenInclude(x => x.ParkingLot)
                            .ThenInclude(x => x.Branch)
                .Include(x => x.ElectronicTicket)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.BookingTime)
                .ToListAsync();

            return bookings.Select(x => new BookingSummaryDto
                {
                    BookingId = x.Id,
                    LicensePlate = x.Vehicle.LicensePlate,
                    SlotCode = x.Slot.SlotCode,
                    BookingTime = x.BookingTime,
                    ExpiredAt = x.ExpiredAt,
                    Status = x.Status
                })
                .ToList();
        }
        public async Task<List<BookingSummaryDto>> GetManagerBookingsAsync(string managerId)
        {
            var bookings = await _context.Bookings
                .Include(x => x.Vehicle)
                .Include(x => x.Slot)
                    .ThenInclude(s => s.Zone)
                        .ThenInclude(z => z.ParkingLot)
                            .ThenInclude(p => p.Branch)
                .Include(x => x.ElectronicTicket)
                .Where(x => x.Slot.Zone.ParkingLot.Branch.ManagerId == managerId)
                .OrderByDescending(x => x.BookingTime)
                .ToListAsync();

            return bookings.Select(x => new BookingSummaryDto
                {
                    BookingId = x.Id,
                    LicensePlate = x.Vehicle.LicensePlate,
                    SlotCode = x.Slot.SlotCode,
                    BookingTime = x.BookingTime,
                    ExpiredAt = x.ExpiredAt,
                    Status = x.Status
                })
                .ToList();
        }

        public async Task<BookingDetailDto> GetMyBookingDetailAsync(Guid bookingId, string userId)
        {
            var booking = await BuildBookingDetailQuery()
                .FirstOrDefaultAsync(x => x.Id == bookingId && x.UserId == userId);

            if (booking == null)
            {
                throw new Exception("Booking not found");
            }

            return ToDetailDto(booking);
        }

        public async Task<BookingDetailDto> GetManagerBookingDetailAsync(Guid bookingId, string managerId)
        {
            var booking = await BuildBookingDetailQuery()
                .FirstOrDefaultAsync(x => x.Id == bookingId && x.Slot.Zone.ParkingLot.Branch.ManagerId == managerId);

            if (booking == null)
            {
                throw new Exception("Booking not found");
            }

            return ToDetailDto(booking);
        }

        public async Task<BookingDetailDto> GetBookingDetailAsync(Guid bookingId)
        {
            var booking = await BuildBookingDetailQuery()
                .FirstOrDefaultAsync(x => x.Id == bookingId);

            if (booking == null)
            {
                throw new Exception("Booking not found");
            }

            return ToDetailDto(booking);
        }

        private static string GenerateTicketCode()
        {
            return $"ETK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
        }

        private static void UpdateTicketStatus(Booking booking)
        {
            if (booking.ElectronicTicket == null)
            {
                return;
            }

            booking.ElectronicTicket.Status = booking.Status;
            booking.ElectronicTicket.ValidUntil = booking.ExpiredAt;
        }

        private IQueryable<Booking> BuildBookingDetailQuery()
        {
            return _context.Bookings
                .Include(x => x.Vehicle)
                .Include(x => x.Slot)
                    .ThenInclude(x => x.Zone)
                        .ThenInclude(x => x.ParkingLot)
                            .ThenInclude(x => x.Branch)
                .Include(x => x.ElectronicTicket);
        }

        private static BookingDetailDto ToDetailDto(Booking booking)
        {
            return new BookingDetailDto
            {
                BookingId = booking.Id,
                LicensePlate = booking.Vehicle.LicensePlate,
                BranchName = booking.Slot.Zone.ParkingLot.Branch.Name,
                BranchAddress = booking.Slot.Zone.ParkingLot.Branch.Address,
                ParkingLotName = booking.Slot.Zone.ParkingLot.Name,
                ZoneName = booking.Slot.Zone.Name,
                SlotCode = booking.Slot.SlotCode,
                BookingTime = booking.BookingTime,
                ExpiredAt = booking.ExpiredAt,
                Status = booking.Status,
                ElectronicTicket = booking.ElectronicTicket?.ToSummaryDto()
            };
        }
    }
}
