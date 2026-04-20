using StackExchange.Redis;
using SmartParking.Services.Interfaces;

namespace SmartParking.Services
{
    public class RedisService : IRedisService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private const string PARKING_CHECKINS_KEY = "parking:checkins";

        public RedisService(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = redis.GetDatabase();
        }

        public async Task<bool> IsPlateActiveAsync(string plate)
        {
            if (string.IsNullOrEmpty(plate))
                return false;

            plate = plate.ToUpper().Trim();
            return await _db.HashExistsAsync(PARKING_CHECKINS_KEY, plate);
        }

        public async Task<DateTime?> GetCheckinTimeAsync(string plate)
        {
            if (string.IsNullOrEmpty(plate))
                return null;

            plate = plate.ToUpper().Trim();
            var value = await _db.HashGetAsync(PARKING_CHECKINS_KEY, plate);
            
            if (value.HasValue && long.TryParse(value.ToString(), out var ticks))
            {
                return new DateTime(ticks);
            }
            return null;
        }

        public async Task AddCheckinAsync(string plate, DateTime checkinTime)
        {
            if (string.IsNullOrEmpty(plate))
                throw new ArgumentException("License plate cannot be empty");

            plate = plate.ToUpper().Trim();
            await _db.HashSetAsync(PARKING_CHECKINS_KEY, plate, checkinTime.Ticks.ToString());
        }

        public async Task<DateTime?> GetAndRemoveCheckinAsync(string plate)
        {
            if (string.IsNullOrEmpty(plate))
                return null;

            plate = plate.ToUpper().Trim();
            var value = await _db.HashGetAsync(PARKING_CHECKINS_KEY, plate);
            
            if (value.HasValue && long.TryParse(value.ToString(), out var ticks))
            {
                await _db.HashDeleteAsync(PARKING_CHECKINS_KEY, plate);
                return new DateTime(ticks);
            }
            
            return null;
        }

        public async Task RemoveCheckinAsync(string plate)
        {
            if (string.IsNullOrEmpty(plate))
                return;

            plate = plate.ToUpper().Trim();
            await _db.HashDeleteAsync(PARKING_CHECKINS_KEY, plate);
        }
    }
}
