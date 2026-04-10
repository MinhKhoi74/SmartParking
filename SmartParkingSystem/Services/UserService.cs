using Microsoft.AspNetCore.Identity;
using SmartParking.DTOs.User;
using SmartParking.Models.Identity;
using SmartParking.Services.Interfaces;

namespace SmartParking.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task ChangePasswordAsync(string userId, ChangePasswordDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) throw new Exception("User not found");

            var result = await _userManager.ChangePasswordAsync(
                user,
                dto.CurrentPassword,
                dto.NewPassword);

            if (!result.Succeeded)
            {
                // Lấy lỗi đầu tiên mà Identity trả về để biết nguyên nhân
                var error = result.Errors.FirstOrDefault()?.Description ?? "Change password failed";
                throw new Exception(error);
            }
        }

        public async Task<object> GetProfileAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            return new
            {
                user.FullName,
                user.Email,
                user.PhoneNumber
            };
        }

        public async Task UpdateProfileAsync(string userId, UpdateProfileDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId);

            user.FullName = dto.FullName;
            user.PhoneNumber = dto.PhoneNumber;

            await _userManager.UpdateAsync(user);
        }
    }
}
