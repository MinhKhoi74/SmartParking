using SmartParking.DTOs.User;

namespace SmartParking.Services.Interfaces
{
    public interface IUserService
    {
        Task ChangePasswordAsync(string userId, ChangePasswordDto dto);

        Task<object> GetProfileAsync(string userId);

        Task UpdateProfileAsync(string userId, UpdateProfileDto dto);
    }
}

