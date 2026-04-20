using Microsoft.AspNetCore.Mvc;
using SmartParking.DTOs;
using SmartParking.Services.Interfaces;

namespace SmartParking.Controllers
{
    [ApiController]
    [Route("api/parking")]
    public class ParkingController : ControllerBase
    {
        private readonly ICheckInService _checkInService;
        private readonly ICheckOutService _checkOutService;
        private readonly ILogger<ParkingController> _logger;

        public ParkingController(
            ICheckInService checkInService,
            ICheckOutService checkOutService,
            ILogger<ParkingController> logger)
        {
            _checkInService = checkInService;
            _checkOutService = checkOutService;
            _logger = logger;
        }

        [HttpPost("check-in")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(CheckInResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CheckInResult), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
        {
            if (!ModelState.IsValid)
            {
                LogValidationErrors("CheckIn");
                return ValidationProblem(ModelState);
            }

            _logger.LogInformation(
                "[CheckIn] Received - Plate: {Plate}, Station: {Station}, Confidence: {Confidence}",
                request.PlateNumber,
                request.StationId,
                request.Confidence);

            var result = await _checkInService.ProcessCheckInAsync(request);

            _logger.LogInformation(
                "[CheckIn] Result - Success: {Success}, ErrorCode: {ErrorCode}, Message: {Message}",
                result.Success,
                result.ErrorCode,
                result.Message);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("check-out")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(CheckOutResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CheckOutResult), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest request)
        {
            if (!ModelState.IsValid)
            {
                LogValidationErrors("CheckOut");
                return ValidationProblem(ModelState);
            }

            _logger.LogInformation(
                "[CheckOut] Received - Plate: {Plate}, Station: {Station}",
                request.PlateNumber,
                request.StationId);

            var result = await _checkOutService.ProcessCheckOutAsync(request);

            _logger.LogInformation(
                "[CheckOut] Result - Success: {Success}, ErrorCode: {ErrorCode}, Message: {Message}",
                result.Success,
                result.ErrorCode,
                result.Message);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("history/{plate}")]
        [Produces("application/json")]
        public async Task<IActionResult> GetCheckInOutHistory(string plate)
        {
            if (string.IsNullOrEmpty(plate))
            {
                return BadRequest(new { message = "Biển số xe không được để trống" });
            }

            plate = plate.ToUpper().Trim();

            return Ok(new { message = "Feature coming soon" });
        }

        private void LogValidationErrors(string actionName)
        {
            var validationErrors = ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            foreach (var (field, errors) in validationErrors)
            {
                _logger.LogError("[{Action}] Validation error on {Field}: {Errors}", actionName, field, string.Join(" | ", errors));
            }
        }
    }
}
