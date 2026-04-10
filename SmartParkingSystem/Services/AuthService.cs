using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartParking.Configurations;
using SmartParking.Data;
using SmartParking.DTOs.Auth;
using SmartParking.Helpers;
using SmartParking.Models.Identity;
using SmartParking.Services.Interfaces;

namespace SmartParking.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ApplicationDBContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AuthService(UserManager<ApplicationUser> userManager, IConfiguration config, ApplicationDBContext context, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _config = config;
            _context = context;
            _roleManager = roleManager;
        }

        public async Task ForgotPasswordAsync(ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            if (user == null) return;

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // gửi email token sau
        }

        public async Task<object> LoginAsync(LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
                throw new Exception("Invalid email or password");
            var roles = await _userManager.GetRolesAsync(user);
            var jwt = JwtHelper.GenerateJwtToken(user, roles, _config.GetSection("JwtSettings").Get<JwtSettings>());
            var refreshToken = new RefreshToken
            {
                Token = Guid.NewGuid().ToString(),
                Expires = DateTime.UtcNow.AddDays(7),
                UserId = user.Id
            };
            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
            return new
            {
                Token = jwt,
                RefreshToken = refreshToken.Token
            };
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshToken);

            if (token != null)
            {
                token.IsRevoked = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<object> RefreshTokenAsync(RefreshTokenRequestDto dto)
        {
            var token = await _context.RefreshTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Token == dto.RefreshToken && !x.IsRevoked);

            if (token == null || token.Expires < DateTime.UtcNow)
                throw new Exception("Invalid refresh token");

            var roles = await _userManager.GetRolesAsync(token.User);

            var jwt = JwtHelper.GenerateJwtToken(token.User, roles, _config.GetSection("JwtSettings").Get<JwtSettings>());

            return new { AccessToken = jwt };
        }


        public async Task<object> RegisterAsync(RegisterDto dto)
        {
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
            };
            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
            await _userManager.AddToRoleAsync(user, "Customer");
            return new { message = "Register Success" };
        }

        public async Task ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            if (user == null)
                throw new Exception("User not found");

            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

            if (!result.Succeeded)
                throw new Exception("Reset password failed");
        }
        public async Task AssignManagerAsync(AssignManagerDto dto)
        {
            var branch = await _context.Branches.FindAsync(dto.BranchId);

            if (branch == null)
                throw new Exception("Branch not found");

            var manager = await _userManager.FindByIdAsync(dto.ManagerId);

            if (manager == null)
                throw new Exception("Manager not found");

            if (!await _userManager.IsInRoleAsync(manager, "Manager"))
                throw new Exception("User does not have Manager role");

            branch.ManagerId = dto.ManagerId;

            await _context.SaveChangesAsync();
        }
        public async Task AssignRoleAsync(AssignRoleDto dto)
        {
            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
                throw new Exception("User không tồn tại");
            var roleExists = await _roleManager.RoleExistsAsync(dto.RoleName);
            if (!roleExists)
                throw new Exception($"Role '{dto.RoleName}' không tồn tại trong hệ thống");
            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
                throw new Exception("Lỗi khi xóa các Role cũ");
            var addResult = await _userManager.AddToRoleAsync(user, dto.RoleName);
            if (!addResult.Succeeded)
            {
                var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
                throw new Exception($"Lỗi khi gán Role mới: {errors}");
            }
        }
    }
}
