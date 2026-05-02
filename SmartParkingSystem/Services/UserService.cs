using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartParking.Data;
using SmartParking.DTOs.User;
using SmartParking.Models.Identity;
using SmartParking.Services.Interfaces;

namespace SmartParking.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDBContext _context;

        public UserService(UserManager<ApplicationUser> userManager, ApplicationDBContext context)
        {
            _userManager = userManager;
            _context = context;
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
            var wallet = await _context.Wallets.FirstOrDefaultAsync(x => x.UserId == userId);

            return new
            {
                user.FullName,
                user.Email,
                user.PhoneNumber,
                WalletBalance = wallet?.Balance ?? 0m
            };
        }

        public async Task UpdateProfileAsync(string userId, UpdateProfileDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId);

            user.FullName = dto.FullName;
            user.PhoneNumber = dto.PhoneNumber;

            await _userManager.UpdateAsync(user);
        }

        public async Task<List<UserListDto>> GetAllUsersAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var result = new List<UserListDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new UserListDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Roles = roles.ToArray(),
                    IsActive = user.IsActive
                });
            }

            return result;
        }

        public async Task<UserDetailDto> GetUserDetailAsync(string userId, string currentUserId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            var roles = await _userManager.GetRolesAsync(user);
            var currentUserRoles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(currentUserId));

            // Nếu là Customer và không phải chính họ, không được xem
            if (roles.Contains("Customer") && userId != currentUserId && !currentUserRoles.Contains("Admin"))
            {
                throw new Exception("You don't have permission to view this user's details");
            }

            var detail = new UserDetailDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToArray(),
                IsActive = user.IsActive
            };

            // Nếu là Manager hoặc Staff, thêm branch info
            if ((roles.Contains("Manager") || roles.Contains("Staff")) && user.BranchId.HasValue)
            {
                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == user.BranchId);
                if (branch != null)
                {
                    detail.Branch = new BranchInfoDto
                    {
                        Id = branch.Id,
                        Name = branch.Name,
                        Address = branch.Address
                    };
                }
            }

            return detail;
        }

        public async Task<string> CreateCustomerAsync(CreateCustomerDto dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                throw new Exception("Email already exists");

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Customer");
            _context.Wallets.Add(new Models.Wallet
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Balance = 0m,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
            return user.Id;
        }

        public async Task<string> CreateManagerAsync(CreateManagerDto dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                throw new Exception("Email already exists");

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == dto.BranchId);
            if (branch == null)
                throw new Exception("Branch not found");

            // Kiểm tra xem branch đã có manager chưa (1-1 relationship)
            if (!string.IsNullOrEmpty(branch.ManagerId))
                throw new Exception("This branch already has a manager");

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                BranchId = dto.BranchId,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Manager");

            // Gán manager cho branch
            branch.ManagerId = user.Id;
            await _context.SaveChangesAsync();

            return user.Id;
        }

        public async Task<string> CreateStaffAsync(CreateStaffDto dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                throw new Exception("Email already exists");

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == dto.BranchId);
            if (branch == null)
                throw new Exception("Branch not found");

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                BranchId = dto.BranchId,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Staff");
            return user.Id;
        }

        public async Task DeleteUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            // Nếu user là Manager của branch, xóa ManagerId (branch không bị xóa)
            var managerBranches = await _context.Branches
                .Where(b => b.ManagerId == userId)
                .ToListAsync();
            
            foreach (var branch in managerBranches)
            {
                branch.ManagerId = null;
            }

            // Xóa tất cả Vehicles của user (CheckInOut sẽ bị xóa cascade nếu có FK)
            var vehicles = await _context.Vehicle
                .Where(v => v.UserId == userId)
                .ToListAsync();
            
            if (vehicles.Any())
            {
                _context.Vehicle.RemoveRange(vehicles);
            }

            // Xóa tất cả RefreshTokens của user
            var refreshTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();
            
            if (refreshTokens.Any())
            {
                _context.RefreshTokens.RemoveRange(refreshTokens);
            }

            // Lưu tất cả thay đổi trước khi xóa user
            await _context.SaveChangesAsync();

            // Xóa user
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        public async Task<List<UserListDto>> GetStaffInBranchAsync(string managerId)
        {
            var manager = await _userManager.FindByIdAsync(managerId);
            if (manager == null)
                throw new Exception("Manager not found");

            var managerRoles = await _userManager.GetRolesAsync(manager);
            if (!managerRoles.Contains("Manager"))
                throw new Exception("User is not a manager");

            if (!manager.BranchId.HasValue)
                throw new Exception("Manager has no assigned branch");

            var staff = await _userManager.Users
                .Where(u => u.BranchId == manager.BranchId && u.Id != managerId)
                .ToListAsync();

            var result = new List<UserListDto>();
            foreach (var s in staff)
            {
                var roles = await _userManager.GetRolesAsync(s);
                result.Add(new UserListDto
                {
                    Id = s.Id,
                    FullName = s.FullName,
                    Email = s.Email,
                    Roles = roles.ToArray(),
                    IsActive = s.IsActive
                });
            }

            return result;
        }

        public async Task<string> CreateStaffByManagerAsync(string managerId, CreateStaffDto dto)
        {
            var manager = await _userManager.FindByIdAsync(managerId);
            if (manager == null)
                throw new Exception("Manager not found");

            var managerRoles = await _userManager.GetRolesAsync(manager);
            if (!managerRoles.Contains("Manager"))
                throw new Exception("User is not a manager");

            if (!manager.BranchId.HasValue)
                throw new Exception("Manager has no assigned branch");

            // Kiểm tra staff thuộc branch của manager
            if (dto.BranchId != manager.BranchId)
                throw new Exception("You can only create staff for your own branch");

            return await CreateStaffAsync(dto);
        }

        public async Task DeleteStaffAsync(string managerId, string staffId)
        {
            var manager = await _userManager.FindByIdAsync(managerId);
            if (manager == null)
                throw new Exception("Manager not found");

            var managerRoles = await _userManager.GetRolesAsync(manager);
            if (!managerRoles.Contains("Manager"))
                throw new Exception("User is not a manager");

            if (!manager.BranchId.HasValue)
                throw new Exception("Manager has no assigned branch");

            var staff = await _userManager.FindByIdAsync(staffId);
            if (staff == null)
                throw new Exception("Staff not found");

            var staffRoles = await _userManager.GetRolesAsync(staff);
            if (!staffRoles.Contains("Staff"))
                throw new Exception("User is not staff");

            // Kiểm tra staff thuộc branch của manager
            if (staff.BranchId != manager.BranchId)
                throw new Exception("You can only delete staff from your own branch");

            await DeleteUserAsync(staffId);
        }
    }
}
