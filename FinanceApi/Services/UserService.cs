using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace FinanceApi.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repository;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;


        public UserService(IUserRepository repository, IConfiguration config, IEmailService emailService)
        {
            _repository = repository;
            _config = config;
            _emailService = emailService;
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<User> CreateAsync(UserDto userDto)
        {

            return await _repository.CreateAsync(userDto);
        }

        public async Task<User?> UpdateAsync(int id, UserDto userDto)
        {
            return await _repository.UpdateAsync(id, userDto);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }

        public async Task<LoginResponseDto> LoginAsync(UserDto userDto)
        {

            var user = await _repository.GetByUsernameAsync(userDto.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(userDto.Password, user.PasswordHash))
                return null;

            var token = GenerateJwtToken(user, _config);
            return new LoginResponseDto
            {
                UserId = user.Id,
                Username = user.Username,
                Token = token
            };
        }

        private string GenerateJwtToken(User user, IConfiguration config)
        {
            var key = Encoding.ASCII.GetBytes(config["Jwt:Secret"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                new Claim("id", user.Id.ToString()),                   
                new Claim(ClaimTypes.Name, user.Username)
        }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto)
        {

            var user = await _repository.GetByIdAsync(userId);
            if (user == null) return false;

            bool isValid = BCrypt.Net.BCrypt.Verify(changePasswordDto.CurrentPassword, user.PasswordHash);
            if (!isValid) return false;

            bool isSameAsCurrent = BCrypt.Net.BCrypt.Verify(changePasswordDto.NewPassword, user.PasswordHash);
            if (isSameAsCurrent)
                throw new ArgumentException("New password cannot be the same as the current password.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);
            user.UpdatedDate = DateTime.UtcNow;

            await _repository.SaveChangesAsync();
            return true;
        }

        public async Task<string> ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            var user = await _repository.GetUserByEmailAsync(request.Email);
            if (user == null) return null;
            if (user != null)
            {
                var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

                var resetToken = new PasswordResetToken
                {
                    UserId = user.Id,
                    Token = token,
                    Expiration = DateTime.UtcNow.AddHours(1)
                };

                await _repository.SaveResetTokenAsync(resetToken);

                var resetLink = $"http://localhost:4200/reset-password?token={token}&email={user.Email}";
                await _emailService.SendResetPasswordEmail(user.Email, resetLink);
            }

            return "If the email exists, a reset link has been sent.";
        }

        public async Task<(bool IsSuccess, string Message)> ResetPasswordAsync(ResetPasswordDto request)
        {
            var user = await _repository.GetUserByEmailAsync(request.Email);
            if (user == null)
                return (false, "Invalid email.");

            var token = await _repository.GetValidTokenAsync(user.Id, request.Token);
            if (token == null || token.Expiration < DateTime.UtcNow)
                return (false, "Invalid or expired token.");

            // Hash the new password
            string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            var success = await _repository.UpdatePasswordAsync(user.Id, newPasswordHash);
            if (!success)
                return (false, "Failed to update password.");

            await _repository.DeleteTokenAsync(token); // Invalidate token

            return (true, "Password reset successful.");
        }


    }
}
