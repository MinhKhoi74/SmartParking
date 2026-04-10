using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.DTOs.Booking;
using SmartParking.Services.Interfaces;

namespace SmartParking.Controllers
{
    [ApiController]
    [Route("api/bookings")]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _service;

        public BookingController(IBookingService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateBookingDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var result = await _service.CreateBookingAsync(userId, dto);

            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            await _service.CancelBookingAsync(id, userId);

            return Ok();
        }
        [HttpGet("my-bookings")]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var result = await _service.GetMyBookingsAsync(userId);

            return Ok(result);
        }
        [HttpGet("detail/{id:guid}")]
        public async Task<IActionResult> GetBookingDetail(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (User.IsInRole("Admin"))
            {
                var adminResult = await _service.GetBookingDetailAsync(id);
                return Ok(adminResult);
            }

            if (User.IsInRole("Manager"))
            {
                var managerResult = await _service.GetManagerBookingDetailAsync(id, userId);
                return Ok(managerResult);
            }

            var result = await _service.GetMyBookingDetailAsync(id, userId);
            return Ok(result);
        }
        [Authorize(Roles = "Manager")]
        [HttpGet("manager-bookings")]
        public async Task<IActionResult> GetManagerBookings()
        {
            var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var result = await _service.GetManagerBookingsAsync(managerId);

            return Ok(result);
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllBookings()
        {
            var result = await _service.GetAllBookingsAsync();

            return Ok(result);
        }
    }
}
