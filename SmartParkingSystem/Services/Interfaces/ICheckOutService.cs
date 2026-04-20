using SmartParking.DTOs;

namespace SmartParking.Services.Interfaces
{
    public interface ICheckOutService
    {
        Task<CheckOutResult> ProcessCheckOutAsync(CheckOutRequest request);
        decimal CalculateFee(TimeSpan duration);
    }
}
