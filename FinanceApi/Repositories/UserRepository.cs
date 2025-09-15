using FinanceApi.Models;
using FinanceApi.Data;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;
using System.Net;

namespace FinanceApi.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _context.User.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.User.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<User> CreateAsync(UserDto userDto)
        {

            try
            {
                var user = new User
                {
                    Username = userDto.Username,
                    Email = userDto.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(userDto.Password),
                    CreatedBy = "System",
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                _context.User.Add(user);
                await _context.SaveChangesAsync();

                return user;
            }
            catch (DbUpdateException dbEx)
            {

                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;

                throw new Exception($"Error saving supplier: {innerMessage}", dbEx);
            }
            catch (Exception ex)
            {

                Console.WriteLine($"General error: {ex.Message}");
                throw;
            }
        }

        public async Task<User?> UpdateAsync(int id, UserDto updatedUser)
        {
            try
            {
                var existingUser = await _context.User.FirstOrDefaultAsync(s => s.Id == id);
                if (existingUser == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingUser.Username = updatedUser.Username;
                existingUser.Email = updatedUser.Email;
                if (!string.IsNullOrWhiteSpace(updatedUser.Password))
                {
                    existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updatedUser.Password);
                }
                existingUser.UpdatedBy = "System";
                existingUser.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingUser;
            }
            catch (DbUpdateException dbEx)
            {

                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;

                throw new Exception($"Error saving supplier: {innerMessage}", dbEx);
            }
            catch (Exception ex)
            {

                Console.WriteLine($"General error: {ex.Message}");
                throw;
            }
        }


        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _context.User.FirstOrDefaultAsync(s => s.Id == id);
            if (user == null) return false;

            user.IsActive = false;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _context.User.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.User.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task SaveResetTokenAsync(PasswordResetToken token)
        {
            _context.PasswordResetToken.Add(token);
            await _context.SaveChangesAsync();
        }

        public async Task<PasswordResetToken?> GetValidTokenAsync(int userId, string token)
        {
            var decodedToken = WebUtility.UrlDecode(token).Replace(" ", "+");

            return await _context.PasswordResetToken
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == decodedToken);
        }
        public async Task<bool> UpdatePasswordAsync(int userId, string hashedPassword)
        {
            var user = await _context.User.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return false;

            user.PasswordHash = hashedPassword;
            user.UpdatedDate = DateTime.UtcNow;
            user.UpdatedBy = "System"; // or whoever is resetting

            await _context.SaveChangesAsync();
            return true;
        }
        public async Task DeleteTokenAsync(PasswordResetToken token)
        {
            _context.PasswordResetToken.Remove(token);
            await _context.SaveChangesAsync();
        }
    }
}
