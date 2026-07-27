using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrainingCenter.DTOs.Instructor;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;

namespace TrainingCenterWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InstructorsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<InstructorsController> _logger;

        public InstructorsController(IUnitOfWork unitOfWork, IMapper mapper, ILogger<InstructorsController> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        // ==========================================
        // PRIVATE HELPER METHODS
        // ==========================================
        private string GetCallerIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

        private bool IsAuthorizedInstructorOrAdmin(int targetInstructorId)
        {
            // Admins have full access
            if (User.IsInRole("Admin"))
                return true;

            // Instructors can only edit their OWN profile
            if (User.IsInRole("Instructor"))
            {
                var claimInstructorId = User.FindFirst("InstructorId")?.Value;
                if (int.TryParse(claimInstructorId, out int currentInstructorId))
                {
                    return currentInstructorId == targetInstructorId;
                }
            }

            return false;
        }

        // ==========================================
        // INSTRUCTOR ENDPOINTS
        // ==========================================

        // 1️⃣ Get all instructors (Admin, Instructor, Student)
        [HttpGet]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetAllInstructors()
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            var instructors = await _unitOfWork.Instructors.GetAllAsync();
            var instructorsReadDto = _mapper.Map<IEnumerable<InstructorReadDto>>(instructors);

            _logger.LogInformation("Retrieved all instructors list. RequestedBy={UserId}, IP={IP}", userId, ip);

            return Ok(instructorsReadDto);
        }

        // 2️⃣ Get instructor by ID (Admin, Instructor, Student)
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetInstructorById(int id)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            var instructor = await _unitOfWork.Instructors.GetByIdAsync(id);

            if (instructor == null)
            {
                _logger.LogWarning(
                    "Instructor requested but not found. UserId={UserId}, TargetInstructorId={TargetInstructorId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Instructor with ID: {id} was not found.");
            }

            var instructorReadDto = _mapper.Map<InstructorReadDto>(instructor);
            return Ok(instructorReadDto);
        }

        // 3️⃣ Create new instructor (Admin Only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateInstructor([FromBody] InstructorCreateDto instructorCreateDto)
        {
            var adminId = GetUserId();
            var ip = GetCallerIp();

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CreateInstructor. AdminId={AdminId}, IP={IP}", adminId, ip);
                return BadRequest(ModelState);
            }

            var instructorEntity = _mapper.Map<Instructor>(instructorCreateDto);

            await _unitOfWork.Instructors.AddAsync(instructorEntity);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "New instructor created successfully. AdminId={AdminId}, CreatedInstructorId={CreatedInstructorId}, IP={IP}",
                adminId, instructorEntity.InstructorId, ip);

            var instructorReadDto = _mapper.Map<InstructorReadDto>(instructorEntity);
            return CreatedAtAction(nameof(GetInstructorById), new { id = instructorReadDto.InstructorId }, instructorReadDto);
        }

        // 4️⃣ Update instructor data (Admin, Instructor - Owner Only)
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> UpdateInstructor(int id, [FromBody] InstructorCreateDto instructorUpdateDto)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            // 🔐 Ownership Check
            if (!IsAuthorizedInstructorOrAdmin(id))
            {
                _logger.LogWarning(
                    "Unauthorized attempt to update instructor profile. UserId={UserId}, TargetInstructorId={TargetInstructorId}, IP={IP}",
                    userId, id, ip);

                return Forbid(); // 403 Forbidden if instructor tries to edit another instructor's profile
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for UpdateInstructor. UserId={UserId}, TargetInstructorId={TargetInstructorId}, IP={IP}", userId, id, ip);
                return BadRequest(ModelState);
            }

            var instructor = await _unitOfWork.Instructors.GetByIdAsync(id);

            if (instructor == null)
            {
                _logger.LogWarning(
                    "Update failed (instructor not found). UserId={UserId}, TargetInstructorId={TargetInstructorId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Instructor with ID: {id} was not found.");
            }

            _mapper.Map(instructorUpdateDto, instructor);

            _unitOfWork.Instructors.Update(instructor);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "Instructor data updated successfully. UpdatedBy={UserId}, InstructorId={InstructorId}, IP={IP}",
                userId, id, ip);

            return Ok("Instructor updated successfully.");
        }

        // 5️⃣ Delete instructor (Admin Only)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteInstructor(int id)
        {
            var adminId = GetUserId();
            var ip = GetCallerIp();

            var instructor = await _unitOfWork.Instructors.GetByIdAsync(id);

            if (instructor == null)
            {
                _logger.LogWarning(
                    "Admin action failed (target instructor not found). AdminId={AdminId}, Action=DeleteInstructor, TargetInstructorId={TargetInstructorId}, IP={IP}",
                    adminId, id, ip);

                return NotFound($"Instructor with ID: {id} was not found.");
            }

            _unitOfWork.Instructors.Delete(instructor);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "Instructor deleted successfully. AdminId={AdminId}, DeletedInstructorId={DeletedInstructorId}, IP={IP}",
                adminId, id, ip);

            return Ok("Instructor deleted successfully.");
        }
    }
}