using StackExchange.Redis;

namespace SmartParking.Services.Interfaces
{
    public interface IRedisService
    {
        // Checkin operations
        Task<bool> IsPlateActiveAsync(string plate);
        Task<DateTime?> GetCheckinTimeAsync(string plate);
        Task AddCheckinAsync(string plate, DateTime checkinTime);
        
        // Checkout operations
        Task<DateTime?> GetAndRemoveCheckinAsync(string plate);
        Task RemoveCheckinAsync(string plate);
    }
}
