using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TrainingCenter.DTOs.Auth;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;
using TrainingCenter.Repositories;

namespace TrainingCenterWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher,
            ILogger<AccountController> logger)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        // ==========================================
        // 1. REGISTER
        // ==========================================
        [HttpPost("register")]
        //[EnableRateLimiting("AuthLimiter")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var allowedRoles = new[] { "Admin", "Instructor", "Student" };
            if (!allowedRoles.Contains(dto.Role))
                return BadRequest("Invalid Role. Allowed values: Admin, Instructor, Student.");

            if (dto.Role == "Student" && dto.InstructorId.HasValue)
                return BadRequest("Students cannot have an InstructorId.");

            if (dto.Role == "Instructor" && dto.StudentId.HasValue)
                return BadRequest("Instructors cannot have a StudentId.");

            if (dto.Role == "Admin" && (dto.StudentId.HasValue || dto.InstructorId.HasValue))
                return BadRequest("Admins cannot have StudentId or InstructorId.");

            if (await _unitOfWork.Users.AnyAsync(u => u.Username == dto.Username))
                return BadRequest("Username is already taken.");

            if (await _unitOfWork.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest("Email is already registered.");

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

            return Ok(new { Message = "User registered successfully." });
        }

        // ==========================================
        // 2. LOGIN
        // ==========================================
        [HttpPost("login")]
        [EnableRateLimiting("AuthLimiter")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Step 1: Find user by Email OR Username in a single database query
            var user = await _unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Email == request.UsernameOrEmail || u.Username == request.UsernameOrEmail);

            // Step 2: Validate user existence and active state
            if (user == null || !user.IsActive)
            {
                _logger.LogWarning(
                    "Failed login attempt (user not found or inactive). Identifier={Identifier}, IP={IP}",
                    request.UsernameOrEmail,
                    ip
                );

                return Unauthorized("Invalid credentials");
            }

            // Step 2: Verify password
            var passwordResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (passwordResult == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("Failed login attempt (bad password). Email={Email}, IP={IP}", request.UsernameOrEmail, ip);
                return Unauthorized("Invalid credentials");
            }

            // Step 3: Generate Access Token
            var accessToken = GenerateAccessToken(user);

            // Step 4: Generate Refresh Token & Hash it before saving
            var rawRefreshToken = GenerateRefreshToken();

            user.RefreshTokenHash = _passwordHasher.HashPassword(user, rawRefreshToken);
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
            user.RefreshTokenRevokedAt = null;

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Successful login. UserId={UserId}, Email={Email}, IP={IP}", user.UserId, user.Email, ip);

            return Ok(new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = rawRefreshToken
            });
        }

        // ==========================================
        // 3. REFRESH TOKEN
        // ==========================================
        [HttpPost("refresh")]
        [EnableRateLimiting("AuthLimiter")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                _logger.LogWarning("Invalid refresh attempt (email not found). Email={Email}, IP={IP}", request.Email, ip);
                return Unauthorized("Invalid refresh request");
            }

            if (user.RefreshTokenRevokedAt != null)
            {
                _logger.LogWarning("Refresh attempt using revoked token. UserId={UserId}, Email={Email}, IP={IP}", user.UserId, user.Email, ip);
                return Unauthorized("Refresh token is revoked");
            }

            if (user.RefreshTokenExpiresAt == null || user.RefreshTokenExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh attempt using expired token. UserId={UserId}, Email={Email}, IP={IP}", user.UserId, user.Email, ip);
                return Unauthorized("Refresh token expired");
            }

            // Verify Refresh Token Hash
            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.RefreshTokenHash ?? string.Empty, request.RefreshToken);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("Invalid refresh token attempt. UserId={UserId}, Email={Email}, IP={IP}", user.UserId, user.Email, ip);
                return Unauthorized("Invalid refresh token");
            }

            // Issue new Access Token & Rotate Refresh Token
            var newAccessToken = GenerateAccessToken(user);
            var newRawRefreshToken = GenerateRefreshToken();

            user.RefreshTokenHash = _passwordHasher.HashPassword(user, newRawRefreshToken);
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
            user.RefreshTokenRevokedAt = null;

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Refresh succeeded. UserId={UserId}, Email={Email}, IP={IP}", user.UserId, user.Email, ip);

            return Ok(new TokenResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRawRefreshToken
            });
        }

        // ==========================================
        // 4. LOGOUT
        // ==========================================
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
                return Ok(new { Message = "Logged out successfully" }); // Do not reveal user existence

            if (!string.IsNullOrEmpty(user.RefreshTokenHash))
            {
                var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.RefreshTokenHash, request.RefreshToken);
                if (verifyResult == PasswordVerificationResult.Success)
                {
                    user.RefreshTokenRevokedAt = DateTime.UtcNow;
                    await _unitOfWork.CompleteAsync();
                }
            }

            return Ok(new { Message = "Logged out successfully" });
        }

        // ==========================================
        // PRIVATE HELPERS
        // ==========================================
        private string GenerateAccessToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "SUPER_SECRET_KEY_FOR_JWT_TRAINING_CENTER_API_2026!");

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
                Expires = DateTime.UtcNow.AddMinutes(15),
                Issuer = jwtSettings["Issuer"] ?? "TrainingCenterAPI",
                Audience = jwtSettings["Audience"] ?? "TrainingCenterClients",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}