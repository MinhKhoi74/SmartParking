using Microsoft.EntityFrameworkCore;
using SmartParking.Data;
using SmartParking.DTOs.Booking;
using SmartParking.Services.Interfaces;

namespace SmartParking.Services
{
    public class ElectronicTicketService : IElectronicTicketService
    {
        private readonly ApplicationDBContext _context;

        public ElectronicTicketService(ApplicationDBContext context)
        {
            _context = context;
        }

        public async Task<ElectronicTicketDetailDto> GetMyTicketByIdAsync(string userId, Guid ticketId)
        {
            var ticket = await BuildTicketQuery()
                .FirstOrDefaultAsync(x => x.Id == ticketId && x.Booking.UserId == userId);

            if (ticket == null)
            {
                throw new Exception("Electronic ticket not found");
            }

            return ticket.ToDetailDto();
        }

        public async Task<List<ElectronicTicketSummaryDto>> GetMyTicketsAsync(string userId)
        {
            var tickets = await BuildTicketQuery()
                .Where(x => x.Booking.UserId == userId)
                .OrderByDescending(x => x.IssuedAt)
                .ToListAsync();

            return tickets.Select(x => x.ToSummaryDto()).ToList();
        }

        public async Task<ElectronicTicketDetailDto> GetTicketByCodeAsync(string ticketCode)
        {
            var normalizedCode = ticketCode.Trim();

            var ticket = await BuildTicketQuery()
                .FirstOrDefaultAsync(x => x.TicketCode == normalizedCode);

            if (ticket == null)
            {
                throw new Exception("Electronic ticket not found");
            }

            return ticket.ToDetailDto();
        }

        public async Task<List<ElectronicTicketSummaryDto>> GetManagerTicketsAsync(string managerId)
        {
            var tickets = await BuildTicketQuery()
                .Where(x => x.Booking.Slot.Zone.ParkingLot.Branch.ManagerId == managerId)
                .OrderByDescending(x => x.IssuedAt)
                .ToListAsync();

            return tickets.Select(x => x.ToSummaryDto()).ToList();
        }

        public async Task<List<ElectronicTicketSummaryDto>> GetAllTicketsAsync()
        {
            var tickets = await BuildTicketQuery()
                .OrderByDescending(x => x.IssuedAt)
                .ToListAsync();

            return tickets.Select(x => x.ToSummaryDto()).ToList();
        }

        public async Task<ElectronicTicketDetailDto> GetManagerTicketByIdAsync(string managerId, Guid ticketId)
        {
            var ticket = await BuildTicketQuery()
                .FirstOrDefaultAsync(x => x.Id == ticketId && x.Booking.Slot.Zone.ParkingLot.Branch.ManagerId == managerId);

            if (ticket == null)
            {
                throw new Exception("Electronic ticket not found");
            }

            return ticket.ToDetailDto();
        }

        public async Task<ElectronicTicketDetailDto> GetTicketByIdAsync(Guid ticketId)
        {
            var ticket = await BuildTicketQuery()
                .FirstOrDefaultAsync(x => x.Id == ticketId);

            if (ticket == null)
            {
                throw new Exception("Electronic ticket not found");
            }

            return ticket.ToDetailDto();
        }

        private IQueryable<Models.ElectronicTicket> BuildTicketQuery()
        {
            return _context.ElectronicTickets
                .Include(x => x.Booking)
                    .ThenInclude(x => x.Vehicle)
                .Include(x => x.Booking)
                    .ThenInclude(x => x.Slot)
                        .ThenInclude(x => x.Zone)
                            .ThenInclude(x => x.ParkingLot)
                                .ThenInclude(x => x.Branch);
        }
    }
}
