using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserManagement.Core.Entities;
using UserManagement.Core.Interfaces;
using UserManagement.DTOs;
using UserManagement.Infastructure.Services;

namespace UserManagement.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly ILogger<UsersController> _logger;
        private readonly IConfiguration _configuration;

        public UsersController(
            IUserRepository userRepository,
            IPasswordHasher<User> passwordHasher,
            IEmailService emailService,
            ILogger<UsersController> logger,
            IConfiguration configuration)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (await _userRepository.GetUserByEmailAsync(request.Email) != null)
            {
                return BadRequest("Email already exists.");
            }

            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = _passwordHasher.HashPassword(null, request.Password),
                VerificationCode = GenerateRandomCode(6), // Assign value here
                VerificationCodeExpiration = DateTime.UtcNow.AddMinutes(10)
            };

            await _userRepository.AddUserAsync(user);

            user.VerificationCode = GenerateRandomCode(6);
            user.VerificationCodeExpiration = DateTime.UtcNow.AddMinutes(10);

            try
            {
                await _emailService.SendEmailAsync(user.Email, "Email Verification", $"Your verification code is: {user.VerificationCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email.");
                return StatusCode(500, "Failed to send verification email.");
            }

            return CreatedAtAction(nameof(GetUserById), new { userId = user.Id }, user);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userRepository.GetUserByEmailAsync(request.Email);
            if (user == null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                return Unauthorized("Invalid email or password.");
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:Secret"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email)
                }),
                Expires = DateTime.UtcNow.AddMinutes(30),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return Ok(new { token = tokenHandler.WriteToken(token) });
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userRepository.GetUserByEmailAsync(request.Email);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            if (user.IsEmailVerified)
            {
                return Ok("Email is already verified.");
            }

            user.VerificationCode = GenerateRandomCode(6);
            user.VerificationCodeExpiration = DateTime.UtcNow.AddMinutes(10);

            try
            {
                await _emailService.SendEmailAsync(user.Email, "Email Verification", $"Your verification code is: {user.VerificationCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email.");
                return StatusCode(500, "Failed to send verification email.");
            }

            await _userRepository.UpdateUserAsync(user);
            return Ok("Verification code sent to your email.");
        }

        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userRepository.GetUserByEmailAsync(request.Email);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            if (user.VerificationCode != request.Code || user.VerificationCodeExpiration < DateTime.UtcNow)
            {
                return BadRequest("Invalid or expired verification code.");
            }

            user.IsEmailVerified = true;
            user.VerificationCode = null;
            user.VerificationCodeExpiration = null;

            await _userRepository.UpdateUserAsync(user);
            return Ok("Email verified successfully.");
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserById(int userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            return user == null ? NotFound() : Ok(user);
        }

        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateUserDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.PhoneNumber = request.PhoneNumber;

            await _userRepository.UpdateUserAsync(user);
            return NoContent();
        }

        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            await _userRepository.DeleteUserAsync(user);
            return NoContent();
        }

        private static string GenerateRandomCode(int length)
        {
            const string chars = "0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[new Random().Next(s.Length)]).ToArray());
        }
    }
}
