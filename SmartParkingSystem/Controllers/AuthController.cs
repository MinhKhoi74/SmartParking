using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartParking.DTOs.Auth;
using SmartParking.Services;
using SmartParking.Services.Interfaces;

namespace SmartParking.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
            => Ok(await _authService.RegisterAsync(dto));

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
            => Ok(await _authService.LoginAsync(dto));

        [HttpPost("refresh-token")]
        public async Task<IActionResult> Refresh(RefreshTokenRequestDto dto)
            => Ok(await _authService.RefreshTokenAsync(dto));

        [HttpPost("assign-role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignRole(AssignRoleDto dto)
        {
            try
            {
                await _authService.AssignRoleAsync(dto);
                return Ok(new { message = $"Đã cập nhật Role {dto.RoleName} cho người dùng thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("assign-manager")]
        public async Task<IActionResult> AssignManager(AssignManagerDto dto)
        {
            await _authService.AssignManagerAsync(dto);

            return Ok();
        }
    }
}
