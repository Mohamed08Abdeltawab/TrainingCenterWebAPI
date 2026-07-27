using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Unicode;
using TrainingCenter.DTOs.Auth;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;
using TrainingCenter.Repositories;

namespace TrainingCenterWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountController(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 1. Validate Role value
            var allowedRoles = new[] { "Admin", "Instructor", "Student" };
            if (!allowedRoles.Contains(dto.Role))
                return BadRequest(new AuthResponseDto { IsSuccess = false, Message = "Invalid Role. Allowed values: Admin, Instructor, Student." });

            // 2. Cross-check Role constraints (InstructorId / StudentId)
            if (dto.Role == "Student" && dto.InstructorId.HasValue)
                return BadRequest(new AuthResponseDto { IsSuccess = false, Message = "Students cannot have an InstructorId." });

            if (dto.Role == "Instructor" && dto.StudentId.HasValue)
                return BadRequest(new AuthResponseDto { IsSuccess = false, Message = "Instructors cannot have a StudentId." });

            if (dto.Role == "Admin" && (dto.StudentId.HasValue || dto.InstructorId.HasValue))
                return BadRequest(new AuthResponseDto { IsSuccess = false, Message = "Admins cannot have StudentId or InstructorId." });

            // 3. Check for unique Username and Email
            //var existingUsers = await _unitOfWork.Users.GetAllAsync();

            if (await _unitOfWork.Users.AnyAsync(u => u.Username == dto.Username))
            {
                return BadRequest(new AuthResponseDto
                {
                    IsSuccess = false,
                    Message = "Username is already taken."
                });
            }

            if (await _unitOfWork.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest(new AuthResponseDto
                {
                    IsSuccess = false,
                    Message = "Email is already registered."
                });
            }

            // 4. Create new user & Hash password
            var newUser = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                Role = dto.Role,
                InstructorId = dto.InstructorId,
                StudentId = dto.StudentId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            newUser.PasswordHash = _passwordHasher.HashPassword(newUser, dto.Password);

            await _unitOfWork.Users.AddAsync(newUser);
            await _unitOfWork.CompleteAsync();

            return Ok(new AuthResponseDto
            {
                IsSuccess = true,
                Message = "User registered successfully.",
                Username = newUser.Username,
                Role = newUser.Role
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 1. Find user by Username or Email
            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u =>
                        u.Username == dto.UsernameOrEmail ||
                        u.Email == dto.UsernameOrEmail);

            if (user == null || !user.IsActive)
                return Unauthorized(new AuthResponseDto { IsSuccess = false, Message = "Invalid credentials or inactive account." });

            // 2. Verify Hashed Password
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new AuthResponseDto { IsSuccess = false, Message = "Invalid credentials." });

            // 3. Generate JWT Token
            var token = GenerateJwtToken(user, out DateTime expiresOn);

            return Ok(new AuthResponseDto
            {
                IsSuccess = true,
                Message = "Logged in successfully.",
                Token = token,
                ExpiresOn = expiresOn,
                Username = user.Username,
                Role = user.Role
            });
        }

        private string GenerateJwtToken(User user, out DateTime expiresOn)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "SUPER_SECRET_KEY_FOR_JWT_TRAINING_CENTER_API_2026!");

            expiresOn = DateTime.Now.AddHours(2);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            if (user.StudentId.HasValue)
                claims.Add(new Claim("StudentId", user.StudentId.Value.ToString()));

            if (user.InstructorId.HasValue)
                claims.Add(new Claim("InstructorId", user.InstructorId.Value.ToString()));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresOn,
                Issuer = jwtSettings["Issuer"] ?? "TrainingCenterAPI",
                Audience = jwtSettings["Audience"] ?? "TrainingCenterClients",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}