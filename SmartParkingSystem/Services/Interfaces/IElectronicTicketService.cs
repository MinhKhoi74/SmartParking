using SmartParking.DTOs.Booking;

namespace SmartParking.Services.Interfaces
{
    public interface IElectronicTicketService
    {
        Task<ElectronicTicketDetailDto> GetMyTicketByIdAsync(string userId, Guid ticketId);

        Task<List<ElectronicTicketSummaryDto>> GetMyTicketsAsync(string userId);

        Task<ElectronicTicketDetailDto> GetTicketByCodeAsync(string ticketCode);

        Task<List<ElectronicTicketSummaryDto>> GetManagerTicketsAsync(string managerId);

        Task<List<ElectronicTicketSummaryDto>> GetAllTicketsAsync();

        Task<ElectronicTicketDetailDto> GetManagerTicketByIdAsync(string managerId, Guid ticketId);

        Task<ElectronicTicketDetailDto> GetTicketByIdAsync(Guid ticketId);
    }
}
