using Microsoft.EntityFrameworkCore;
using SmartParking.Data;
using SmartParking.DTOs.Parking;
using SmartParking.Services.Interfaces;

namespace SmartParking.Services
{
    public class ParkingHistoryService : IParkingHistoryService
    {
        private readonly ApplicationDBContext _context;

        public ParkingHistoryService(ApplicationDBContext context)
        {
            _context = context;
        }

        public async Task<List<ParkingHistoryItemDto>> GetHistoryByPlateAsync(string plate)
        {
            plate = plate.ToUpper().Trim();

            return await BuildQuery()
                .Where(x => x.LicensePlate == plate)
                .OrderByDescending(x => x.CheckInTime)
                .ToListAsync();
        }

        public async Task<List<ParkingHistoryItemDto>> GetMyHistoryAsync(string userId)
        {
            return await BuildQuery()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CheckInTime)
                .ToListAsync();
        }

        private IQueryable<ParkingHistoryItemDto> BuildQuery()
        {
            return _context.CheckInOuts
                .Select(x => new ParkingHistoryItemDto
                {
                    Id = x.Id,
                    LicensePlate = x.LicensePlate,
                    CheckInTime = x.CheckInTime,
                    CheckOutTime = x.CheckOutTime,
                    DurationMinutes = x.DurationMinutes,
                    FeeAmount = x.FeeAmount,
                    FeeStatus = x.FeeStatus,
                    PaymentStatus = x.PaymentStatus,
                    PaymentMethod = x.PaymentMethod,
                    Status = x.Status,
                    CheckInStationId = x.CheckInStationId,
                    CheckOutStationId = x.CheckOutStationId,
                    VehicleId = x.VehicleId,
                    UserId = x.UserId
                });
        }
    }
}
