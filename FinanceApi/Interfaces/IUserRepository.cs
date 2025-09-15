using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IUserRepository
    {
        Task<List<User>> GetAllAsync();
        Task<User?> GetByIdAsync(int id);
        Task<User> CreateAsync(UserDto userDto);
        Task<User?> UpdateAsync(int id, UserDto userDto);
        Task<bool> DeleteAsync(int id);
        Task<User?> GetByUsernameAsync(string username);
        Task SaveChangesAsync();
        Task<User?> GetUserByEmailAsync(string email);
        Task SaveResetTokenAsync(PasswordResetToken token);
        Task<PasswordResetToken?> GetValidTokenAsync(int userId, string token);
        Task<bool> UpdatePasswordAsync(int userId, string hashedPassword);
        Task DeleteTokenAsync(PasswordResetToken token);

    }
}
