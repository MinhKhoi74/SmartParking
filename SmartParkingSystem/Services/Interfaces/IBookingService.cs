using SmartParking.DTOs.Booking;

namespace SmartParking.Services.Interfaces
{
    public interface IBookingService
    {
        Task<BookingDetailDto> CreateBookingAsync(string userId, CreateBookingDto dto);

        Task CancelBookingAsync(Guid bookingId, string userId);
        Task<List<BookingSummaryDto>> GetMyBookingsAsync(string userId);

        Task<List<BookingSummaryDto>> GetManagerBookingsAsync(string managerId);

        Task<List<BookingSummaryDto>> GetAllBookingsAsync();

        Task<BookingDetailDto> GetMyBookingDetailAsync(Guid bookingId, string userId);

        Task<BookingDetailDto> GetManagerBookingDetailAsync(Guid bookingId, string managerId);

        Task<BookingDetailDto> GetBookingDetailAsync(Guid bookingId);
    }
}
