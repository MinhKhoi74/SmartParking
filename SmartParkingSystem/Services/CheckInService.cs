using SmartParking.Data;
using SmartParking.DTOs;
using SmartParking.Models;
using SmartParking.Services.Interfaces;

namespace SmartParking.Services
{
    public class CheckInService : ICheckInService
    {
        private readonly ApplicationDBContext _context;
        private readonly IRedisService _redis;
        private readonly ILogger<CheckInService> _logger;

        public CheckInService(
            ApplicationDBContext context,
            IRedisService redis,
            ILogger<CheckInService> logger)
        {
            _context = context;
            _redis = redis;
            _logger = logger;
        }

        public async Task<CheckInResult> ProcessCheckInAsync(CheckInRequest request)
        {
            if (string.IsNullOrEmpty(request.PlateNumber))
            {
                return new CheckInResult
                {
                    Success = false,
                    Message = "Biển số xe không được để trống",
                    ErrorCode = "EMPTY_PLATE"
                };
            }

            var plate = request.PlateNumber.ToUpper().Trim();
            var now = DateTime.Now;

            try
            {
                // 1. Kiểm tra Redis - duplicate check
                if (await _redis.IsPlateActiveAsync(plate))
                {
                    _logger.LogWarning($"❌ Duplicate checkin attempt: {plate}");
                    return new CheckInResult
                    {
                        Success = false,
                        Message = $"Xe {plate} đã checkin rồi. Không thể checkin lại!",
                        ErrorCode = "DUPLICATE_CHECKIN"
                    };
                }

                // 2. Thêm vào Redis
                await _redis.AddCheckinAsync(plate, now);

                // 3. Lưu vào Database
                var checkinRecord = new CheckInOut
                {
                    LicensePlate = plate,
                    PlateImagePath = string.Empty,
                    CheckInTime = now,
                    CheckInStationId = request.StationId ?? "STATION_01",
                    CheckInImageBase64 = request.ImageBase64 ?? string.Empty,
                    CheckOutStationId = string.Empty,
                    CheckOutImageBase64 = string.Empty,
                    Confidence = request.Confidence,
                    Status = "Active",
                    FeeStatus = "Pending",
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.CheckInOuts.Add(checkinRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✓ Checkin - {plate} - {now:dd/M/yyyy - HH:mm}");

                return new CheckInResult
                {
                    Success = true,
                    Message = $"✓ Checkin thành công cho {plate}",
                    CheckInId = checkinRecord.Id,
                    CheckInTime = now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Checkin failed for {Plate}: {Message}", plate, ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "❌ Checkin inner exception for {Plate}: {Message}", plate, ex.InnerException.Message);
                }
                // Rollback Redis nếu DB fail
                try
                {
                    await _redis.RemoveCheckinAsync(plate);
                }
                catch (Exception redisEx)
                {
                    _logger.LogError($"Failed to rollback Redis: {redisEx.Message}");
                }
                
                return new CheckInResult
                {
                    Success = false,
                    Message = "Có lỗi xảy ra trong quá trình checkin",
                    ErrorCode = "SYSTEM_ERROR"
                };
            }
        }
    }
}
