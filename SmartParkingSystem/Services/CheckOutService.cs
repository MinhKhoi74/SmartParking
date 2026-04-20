using Microsoft.EntityFrameworkCore;
using SmartParking.Data;
using SmartParking.DTOs;
using SmartParking.Models;
using SmartParking.Services.Interfaces;

namespace SmartParking.Services
{
    public class CheckOutService : ICheckOutService
    {
        private readonly ApplicationDBContext _context;
        private readonly IRedisService _redis;
        private readonly ILogger<CheckOutService> _logger;

        // Fee configuration: 50,000 VND per hour
        // Free for first 15 minutes
        private readonly decimal _feePerHour = 50000m;
        private readonly int _freeMinutes = 15;

        public CheckOutService(
            ApplicationDBContext context,
            IRedisService redis,
            ILogger<CheckOutService> logger)
        {
            _context = context;
            _redis = redis;
            _logger = logger;
        }

        public async Task<CheckOutResult> ProcessCheckOutAsync(CheckOutRequest request)
        {
            if (string.IsNullOrEmpty(request.PlateNumber))
            {
                return new CheckOutResult
                {
                    Success = false,
                    Message = "Biển số xe không được để trống",
                    ErrorCode = "EMPTY_PLATE"
                };
            }

            var plate = request.PlateNumber.ToUpper().Trim();
            var now = DateTime.Now;

            DateTime? checkinTime = null;
            CheckInOut checkinRecord = null;

            try
            {
                // 1. Kiểm tra Redis trước (nhanh)
                checkinTime = await _redis.GetAndRemoveCheckinAsync(plate);

                if (checkinTime != null)
                {
                    _logger.LogInformation($"✓ Found in Redis: {plate}");

                    // Lấy record từ DB để update
                    checkinRecord = await _context.CheckInOuts
                        .Where(c => c.LicensePlate == plate && c.Status == "Active")
                        .OrderByDescending(c => c.CheckInTime)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    // 2. Nếu không có trong Redis, kiểm tra Database
                    _logger.LogWarning($"⚠️ Not in Redis, checking database: {plate}");

                    checkinRecord = await _context.CheckInOuts
                        .Where(c => c.LicensePlate == plate && c.Status == "Active")
                        .OrderByDescending(c => c.CheckInTime)
                        .FirstOrDefaultAsync();

                    if (checkinRecord == null)
                    {
                        _logger.LogWarning($"❌ No checkin record found for {plate}");
                        return new CheckOutResult
                        {
                            Success = false,
                            Message = $"Không tìm thấy thông tin checkin cho {plate}",
                            ErrorCode = "NO_CHECKIN_RECORD"
                        };
                    }

                    checkinTime = checkinRecord.CheckInTime;
                }

                // 3. Tính phí
                var duration = now - checkinTime.Value;
                var fee = CalculateFee(duration);

                // 4. Update record
                checkinRecord.CheckOutTime = now;
                checkinRecord.CheckOutStationId = request.StationId ?? "STATION_02";
                checkinRecord.CheckOutImageBase64 = request.ImageBase64 ?? checkinRecord.CheckOutImageBase64 ?? string.Empty;
                checkinRecord.DurationMinutes = (int)duration.TotalMinutes;
                checkinRecord.FeeAmount = fee;
                checkinRecord.FeeCalculatedAt = now;
                checkinRecord.FeeStatus = "Calculated";
                checkinRecord.Status = "Completed";
                checkinRecord.UpdatedAt = now;

                _context.CheckInOuts.Update(checkinRecord);
                await _context.SaveChangesAsync();

                var feeText = fee > 0 ? $"- Phí: {fee:N0}đ" : "- Miễn phí (< 15 phút)";
                _logger.LogInformation($"✓ Checkout - {plate} - {now:dd/M/yyyy - HH:mm} {feeText}");

                return new CheckOutResult
                {
                    Success = true,
                    Message = $"✓ Checkout thành công cho {plate}",
                    CheckOutId = checkinRecord.Id,
                    CheckOutTime = now,
                    DurationMinutes = (int)duration.TotalMinutes,
                    FeeAmount = fee
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Checkout failed for {Plate}: {Message}", plate, ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "❌ Checkout inner exception for {Plate}: {Message}", plate, ex.InnerException.Message);
                }

                return new CheckOutResult
                {
                    Success = false,
                    Message = "Có lỗi xảy ra trong quá trình checkout",
                    ErrorCode = "SYSTEM_ERROR"
                };
            }
        }

        public decimal CalculateFee(TimeSpan duration)
        {
            // Tính toán phí dựa vào thời gian:
            // - <= 15 phút: Free
            // - > 15 phút: Tính theo giờ (làm tròn lên)
            // Ví dụ: 20 phút = 1 giờ = 50,000đ
            //        1 giờ 30 phút = 2 giờ = 100,000đ

            if (duration.TotalMinutes <= _freeMinutes)
                return 0m;

            var minutesAfterFree = duration.TotalMinutes - _freeMinutes;
            var hours = Math.Ceiling(minutesAfterFree / 60.0);

            return (decimal)hours * _feePerHour;
        }
    }
}
