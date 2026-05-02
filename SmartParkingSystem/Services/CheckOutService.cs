using Microsoft.EntityFrameworkCore;
using SmartParking.Data;
using SmartParking.DTOs;
using SmartParking.DTOs.ElectronicTicket;
using SmartParking.DTOs.Momo;
using SmartParking.Models;
using SmartParking.Models.Enums;
using SmartParking.Services.Interfaces;
using System.Text;
using System.Text.Json;

namespace SmartParking.Services
{
    public class CheckOutService : ICheckOutService
    {
        private readonly ApplicationDBContext _context;
        private readonly IRedisService _redis;
        private readonly IWalletService _walletService;
        private readonly IMomoService _momoService;
        private readonly IElectronicTicketService _electronicTicketService;
        private readonly IElectronicTicketNotificationService _notificationService;
        private readonly ILogger<CheckOutService> _logger;

        private readonly decimal _feePerHour = 50000m;
        private readonly int _freeMinutes = 1;

        public CheckOutService(
            ApplicationDBContext context,
            IRedisService redis,
            IWalletService walletService,
            IMomoService momoService,
            IElectronicTicketService electronicTicketService,
            IElectronicTicketNotificationService notificationService,
            ILogger<CheckOutService> logger)
        {
            _context = context;
            _redis = redis;
            _walletService = walletService;
            _momoService = momoService;
            _electronicTicketService = electronicTicketService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<CheckOutResult> ProcessCheckOutAsync(CheckOutRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PlateNumber))
            {
                return new CheckOutResult
                {
                    Success = false,
                    Message = "Bien so xe khong duoc de trong",
                    ErrorCode = "EMPTY_PLATE"
                };
            }

            var plate = request.PlateNumber.ToUpper().Trim();
            var now = DateTime.Now;

            try
            {
                var checkinTime = await _redis.GetCheckinTimeAsync(plate);

                CheckInOut? checkinRecord;
                if (checkinTime != null)
                {
                    _logger.LogInformation("Found active checkin in Redis: {Plate}", plate);
                    checkinRecord = await _context.CheckInOuts
                        .Where(c => c.LicensePlate == plate && c.Status == "Active")
                        .OrderByDescending(c => c.CheckInTime)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    _logger.LogWarning("Plate {Plate} not found in Redis, falling back to database", plate);
                    checkinRecord = await _context.CheckInOuts
                        .Where(c => c.LicensePlate == plate && c.Status == "Active")
                        .OrderByDescending(c => c.CheckInTime)
                        .FirstOrDefaultAsync();

                    checkinTime = checkinRecord?.CheckInTime;
                }

                if (checkinRecord == null || checkinTime == null)
                {
                    _logger.LogWarning("No active checkin found for {Plate}", plate);
                    return new CheckOutResult
                    {
                        Success = false,
                        Message = $"Khong tim thay thong tin checkin cho {plate}",
                        ErrorCode = "NO_CHECKIN_RECORD"
                    };
                }

                var duration = now - checkinTime.Value;
                var fee = CalculateFee(duration);
                decimal? walletBalanceAfter = null;
                WalletTransaction? walletTransaction = null;

                if (fee > 0 && string.IsNullOrWhiteSpace(checkinRecord.UserId))
                {
                    return await BuildPendingPaymentResultAsync(
                        checkinRecord,
                        request,
                        plate,
                        now,
                        duration,
                        fee,
                        "Khach vang lai chua lien ket tai khoan, vui long thanh toan bang MoMo QR hoac tien mat tai quay",
                        "PAYMENT_REQUIRED_WALK_IN");
                }

                if (!string.IsNullOrWhiteSpace(checkinRecord.UserId) && fee > 0)
                {
                    if (!await _walletService.HasSufficientBalanceAsync(checkinRecord.UserId, fee))
                    {
                        // ❌ VÍ KHÔNG ĐỦ - CHECKOUT THẤT BẠI
                        // Không cập nhật CheckInOut record, trả về lỗi
                        _logger.LogInformation("Wallet insufficient for {Plate}: Required={Fee}, UserId={UserId}", plate, fee, checkinRecord.UserId);
                        
                        return new CheckOutResult
                        {
                            Success = false,
                            Message = $"Vi SmartParking không đủ để thanh toán. Vui lòng thanh toán {fee:N0}đ bằng tiền mặt tại quầy",
                            ErrorCode = "INSUFFICIENT_WALLET_BALANCE",
                            CheckOutId = checkinRecord.Id,
                            DurationMinutes = (int)duration.TotalMinutes,
                            FeeAmount = fee,
                            PaymentStatus = "PendingCash"
                        };
                    }

                    walletTransaction = await _walletService.DebitAsync(
                        checkinRecord.UserId,
                        fee,
                        "ParkingFee",
                        $"Thanh toan phi gui xe cho bien so {plate}",
                        "CheckInOut",
                        checkinRecord.Id.ToString());

                    walletBalanceAfter = walletTransaction.BalanceAfter;
                }

                checkinRecord.CheckOutTime = now;
                checkinRecord.CheckOutStationId = request.StationId ?? "STATION_02";
                checkinRecord.CheckOutImageBase64 = request.ImageBase64 ?? checkinRecord.CheckOutImageBase64 ?? string.Empty;
                checkinRecord.DurationMinutes = (int)duration.TotalMinutes;
                checkinRecord.FeeAmount = fee;
                checkinRecord.FeeCalculatedAt = now;
                checkinRecord.FeeStatus = fee > 0 && walletTransaction == null ? "Calculated" : "Paid";
                checkinRecord.PaymentStatus = fee > 0 && walletTransaction == null ? "Pending" : "Paid";
                checkinRecord.PaymentMethod = fee == 0 ? "Free" : walletTransaction != null ? "Wallet" : null;
                checkinRecord.WalletTransactionId = walletTransaction?.Id;
                checkinRecord.PaidAt = fee == 0 || walletTransaction != null ? now : null;
                checkinRecord.Status = fee > 0 && walletTransaction == null ? "Active" : "Completed";
                checkinRecord.UpdatedAt = now;

                _context.CheckInOuts.Update(checkinRecord);
                await _context.SaveChangesAsync();

                // ======== CẬP NHẬT VÉ ĐIỆN TỬ ========
                try
                {
                    // Tìm vé điện tử theo biển số
                    var ticket = await _electronicTicketService.GetTicketByLicensePlateAsync(plate);

                    if (ticket != null)
                    {
                        // Cập nhật checkout time và fee amount
                        await _electronicTicketService.UpdateCheckOutInfoAsync(ticket.Id, now, fee);

                        // Xử lý thanh toán
                        if (fee > 0 && walletTransaction != null)
                        {
                            // Đã thanh toán bằng ví
                            var confirmDto = new PaymentConfirmationDto
                            {
                                TicketId = ticket.Id,
                                PaymentAmount = fee,
                                PaymentMethod = PaymentMethod.Wallet
                            };
                            await _electronicTicketService.UpdatePaymentStatusAsync(ticket.Id, confirmDto);

                            // Gửi thông báo cho user
                            if (!string.IsNullOrEmpty(checkinRecord.UserId))
                            {
                                var updatedTicket = await _electronicTicketService.GetTicketByIdAsync(checkinRecord.UserId, ticket.Id);
                                await _notificationService.SendPaymentConfirmationAsync(
                                    checkinRecord.UserId,
                                    ticket.Id,
                                    $"Thanh toán {fee:N0}đ qua ví thành công");
                            }
                        }
                        else if (fee > 0 && walletTransaction == null && !string.IsNullOrEmpty(checkinRecord.UserId))
                        {
                            // Ví không đủ - cần thanh toán tiền mặt
                            var updatedTicket = await _electronicTicketService.GetTicketByIdAsync(checkinRecord.UserId, ticket.Id);
                            await _notificationService.SendCashPaymentRequiredNotificationAsync(checkinRecord.UserId, updatedTicket);
                        }

                        _logger.LogInformation("Electronic ticket updated for {Plate}: {TicketId}", plate, ticket.Id);
                    }
                }
                catch (Exception ticketEx)
                {
                    _logger.LogError(ticketEx, "Error updating electronic ticket for {Plate}", plate);
                    // Không throw - tiếp tục checkout ngay cả nếu cập nhật vé thất bại
                }

                if (checkinRecord.Status == "Completed")
                {
                    await _redis.RemoveCheckinAsync(plate);
                }

                var feeText = fee > 0 ? $"- Phi: {fee:N0}d" : "- Mien phi";
                _logger.LogInformation("Checkout - {Plate} - {Time:dd/M/yyyy - HH:mm} {FeeText}", plate, now, feeText);

                return new CheckOutResult
                {
                    Success = true,
                    Message = $"Checkout thanh cong cho {plate}",
                    CheckOutId = checkinRecord.Id,
                    CheckOutTime = now,
                    DurationMinutes = (int)duration.TotalMinutes,
                    FeeAmount = fee,
                    PaymentStatus = checkinRecord.PaymentStatus,
                    PaymentMethod = checkinRecord.PaymentMethod,
                    WalletBalanceAfter = walletBalanceAfter
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout failed for {Plate}: {Message}", plate, ex.Message);

                return new CheckOutResult
                {
                    Success = false,
                    Message = "Co loi xay ra trong qua trinh checkout",
                    ErrorCode = "SYSTEM_ERROR"
                };
            }
        }

        public decimal CalculateFee(TimeSpan duration)
        {
            if (duration.TotalMinutes <= _freeMinutes)
            {
                return 0m;
            }

            var minutesAfterFree = duration.TotalMinutes - _freeMinutes;
            var hours = Math.Ceiling(minutesAfterFree / 60.0);
            return (decimal)hours * _feePerHour;
        }

        public async Task<CheckOutResult> ConfirmPendingPaymentAsync(int checkOutId, ConfirmCheckOutPaymentRequest request)
        {
            var paymentMethod = (request.PaymentMethod ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(paymentMethod))
            {
                return new CheckOutResult
                {
                    Success = false,
                    Message = "Payment method is required",
                    ErrorCode = "EMPTY_PAYMENT_METHOD"
                };
            }

            var checkinRecord = await _context.CheckInOuts.FirstOrDefaultAsync(x => x.Id == checkOutId);
            if (checkinRecord == null)
            {
                return new CheckOutResult
                {
                    Success = false,
                    Message = $"Khong tim thay phien gui xe {checkOutId}",
                    ErrorCode = "CHECKOUT_NOT_FOUND"
                };
            }

            if (!string.Equals(checkinRecord.PaymentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return new CheckOutResult
                {
                    Success = false,
                    Message = $"Phien gui xe {checkOutId} khong o trang thai cho thanh toan",
                    ErrorCode = "PAYMENT_NOT_PENDING"
                };
            }

            if (checkinRecord.FeeAmount <= 0m)
            {
                return new CheckOutResult
                {
                    Success = false,
                    Message = $"Phien gui xe {checkOutId} khong co phi can thanh toan",
                    ErrorCode = "PAYMENT_NOT_REQUIRED"
                };
            }

            var now = DateTime.Now;
            checkinRecord.PaymentStatus = "Paid";
            checkinRecord.PaymentMethod = paymentMethod;
            checkinRecord.FeeStatus = "Paid";
            checkinRecord.PaidAt = now;
            checkinRecord.Status = "Completed";
            checkinRecord.UpdatedAt = now;

            _context.CheckInOuts.Update(checkinRecord);
            await _context.SaveChangesAsync();
            await _redis.RemoveCheckinAsync(checkinRecord.LicensePlate);

            return new CheckOutResult
            {
                Success = true,
                Message = $"Da xac nhan thanh toan checkout cho {checkinRecord.LicensePlate}",
                CheckOutId = checkinRecord.Id,
                CheckOutTime = checkinRecord.CheckOutTime,
                DurationMinutes = checkinRecord.DurationMinutes,
                FeeAmount = checkinRecord.FeeAmount,
                PaymentStatus = checkinRecord.PaymentStatus,
                PaymentMethod = checkinRecord.PaymentMethod
            };
        }

        private async Task<CheckOutResult> BuildPendingPaymentResultAsync(
            CheckInOut checkinRecord,
            CheckOutRequest request,
            string plate,
            DateTime now,
            TimeSpan duration,
            decimal fee,
            string message,
            string errorCode)
        {
            checkinRecord.CheckOutTime = now;
            checkinRecord.CheckOutStationId = request.StationId ?? "STATION_02";
            checkinRecord.CheckOutImageBase64 = request.ImageBase64 ?? checkinRecord.CheckOutImageBase64 ?? string.Empty;
            checkinRecord.DurationMinutes = (int)duration.TotalMinutes;
            checkinRecord.FeeAmount = fee;
            checkinRecord.FeeCalculatedAt = now;
            checkinRecord.FeeStatus = "Calculated";
            checkinRecord.PaymentStatus = "Pending";
            checkinRecord.PaymentMethod = null;
            checkinRecord.PaidAt = null;
            checkinRecord.Status = "Active";
            checkinRecord.UpdatedAt = now;

            _context.CheckInOuts.Update(checkinRecord);
            await _context.SaveChangesAsync();

            // Only support Cash payment for checkout (no MoMo payment)
            var paymentOptions = new List<CheckOutPaymentOptionDto>
            {
                new()
                {
                    Method = "Cash",
                    Label = "Tiền mặt",
                    Note = "Thu tiền mặt tại quay và cập nhật thanh toán thủ công"
                }
            };

            return new CheckOutResult
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                CheckOutId = checkinRecord.Id,
                CheckOutTime = now,
                DurationMinutes = (int)duration.TotalMinutes,
                FeeAmount = fee,
                PaymentStatus = "Pending",
                RequiresPaymentAction = true,
                PaymentOptions = paymentOptions
            };
        }

        private static string EncodeExtraData(Dictionary<string, string> data)
        {
            var json = JsonSerializer.Serialize(data);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
