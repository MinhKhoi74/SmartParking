using Microsoft.AspNetCore.Identity;

namespace SmartParking.Models.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public bool IsActive { get; set; } = true;
        public ICollection<Vehicle> Vehicles { get; set; }
        public ICollection<Branch> ManagedBranches { get; set; }
        public ICollection<RefreshToken> RefreshTokens { get; set; }
    }
}
