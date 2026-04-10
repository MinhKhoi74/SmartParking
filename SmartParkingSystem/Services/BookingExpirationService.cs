using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartParking.Data;
using SmartParking.Models.Enums;
using SmartParking.SignalR; // Đảm bảo đúng namespace của Hub

namespace SmartParking.Services
{
    public class BookingExpirationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BookingExpirationService> _logger;

        public BookingExpirationService(
            IServiceScopeFactory scopeFactory,
            ILogger<BookingExpirationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Booking Expiration Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
                        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ParkingHub>>();

                        var now = DateTime.UtcNow;

                        var expiredBookings = await context.Bookings
                            .Include(x => x.Slot)
                            .Include(x => x.ElectronicTicket)
                            .Where(x => x.Status == BookingStatus.Pending && x.ExpiredAt < now)
                            .ToListAsync(stoppingToken); // Truyền stoppingToken vào đây

                        if (expiredBookings.Any())
                        {
                            foreach (var booking in expiredBookings)
                            {
                                booking.Status = BookingStatus.Expired;
                                if (booking.Slot != null)
                                {
                                    booking.Slot.Status = SlotStatus.Available;
                                }

                                if (booking.ElectronicTicket != null)
                                {
                                    booking.ElectronicTicket.Status = BookingStatus.Expired;
                                    booking.ElectronicTicket.ValidUntil = booking.ExpiredAt;
                                }
                            }

                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Released {expiredBookings.Count} expired slots.");

                            // THÔNG BÁO REAL-TIME QUA SIGNALR
                            foreach (var booking in expiredBookings)
                            {
                                // Gửi cho tất cả client để cập nhật bản đồ bãi xe
                                await hubContext.Clients.All.SendAsync("SlotUpdated", booking.SlotId, stoppingToken);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Lỗi này xảy ra khi App tắt, là bình thường nên không cần log error
                    _logger.LogWarning("Booking Expiration Task was canceled (App is shutting down).");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking expired bookings.");
                }

                // Đợi 1 phút trước khi quét lại
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException) { /* Trình chặn lỗi khi dừng App */ }
            }
        }
    }
}
