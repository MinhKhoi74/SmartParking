using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Services.Interfaces;

namespace SmartParking.Controllers
{
    [ApiController]
    [Route("api/electronic-tickets")]
    [Authorize]
    public class ElectronicTicketsController : ControllerBase
    {
        private readonly IElectronicTicketService _service;

        public ElectronicTicketsController(IElectronicTicketService service)
        {
            _service = service;
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyTickets()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _service.GetMyTicketsAsync(userId);

            return Ok(result);
        }

        [HttpGet("detail/{id:guid}")]
        public async Task<IActionResult> GetTicketDetail(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (User.IsInRole("Admin"))
            {
                var adminResult = await _service.GetTicketByIdAsync(id);
                return Ok(adminResult);
            }

            if (User.IsInRole("Manager"))
            {
                var managerResult = await _service.GetManagerTicketByIdAsync(userId, id);
                return Ok(managerResult);
            }

            var result = await _service.GetMyTicketByIdAsync(userId, id);

            return Ok(result);
        }

        [Authorize(Roles = "Admin,Manager,Staff")]
        [HttpGet("code/{ticketCode}")]
        public async Task<IActionResult> GetByCode(string ticketCode)
        {
            var result = await _service.GetTicketByCodeAsync(ticketCode);

            return Ok(result);
        }

        [Authorize(Roles = "Manager")]
        [HttpGet("manager")]
        public async Task<IActionResult> GetManagerTickets()
        {
            var managerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _service.GetManagerTicketsAsync(managerId);

            return Ok(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllTickets()
        {
            var result = await _service.GetAllTicketsAsync();

            return Ok(result);
        }
    }
}
