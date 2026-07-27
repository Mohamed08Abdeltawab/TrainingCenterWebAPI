using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrainingCenter.DTOs.Enrollment;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;

namespace TrainingCenterWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EnrollmentsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<EnrollmentsController> _logger;

        public EnrollmentsController(IUnitOfWork unitOfWork, IMapper mapper, ILogger<EnrollmentsController> logger)
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

        private async Task<bool> IsAuthorizedToAccessEnrollmentAsync(Enrollment enrollment)
        {
            // Admin has full access
            if (User.IsInRole("Admin"))
                return true;

            // Student can only access their own enrollment
            if (User.IsInRole("Student"))
            {
                var claimStudentId = User.FindFirst("StudentId")?.Value;
                if (int.TryParse(claimStudentId, out int currentStudentId))
                {
                    return enrollment.StudentId == currentStudentId;
                }
            }

            // Instructor can only access enrollments for courses they teach
            if (User.IsInRole("Instructor"))
            {
                var claimInstructorId = User.FindFirst("InstructorId")?.Value;
                if (int.TryParse(claimInstructorId, out int currentInstructorId))
                {
                    var course = await _unitOfWork.Courses.GetByIdAsync(enrollment.CourseId);
                    return course != null && course.InstructorId == currentInstructorId;
                }
            }

            return false;
        }

        // ==========================================
        // ENROLLMENT ENDPOINTS
        // ==========================================

        // 1️⃣ Get all enrollments (Admin Only)
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllEnrollments()
        {
            var adminId = GetUserId();
            var ip = GetCallerIp();

            var enrollments = await _unitOfWork.Enrollments.GetAllAsync();
            var enrollmentsReadDto = _mapper.Map<IEnumerable<EnrollmentReadDto>>(enrollments);

            _logger.LogInformation("Retrieved all enrollments list. AdminId={AdminId}, IP={IP}", adminId, ip);

            return Ok(enrollmentsReadDto);
        }

        // 2️⃣ Get enrollment by ID (Admin, Instructor, Student)
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetEnrollmentById(int id)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            var enrollment = await _unitOfWork.Enrollments.GetByIdAsync(id);

            if (enrollment == null)
            {
                _logger.LogWarning(
                    "Enrollment requested but not found. UserId={UserId}, TargetEnrollmentId={TargetEnrollmentId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Enrollment with ID: {id} was not found.");
            }

            // 🔐 Ownership Check
            if (!await IsAuthorizedToAccessEnrollmentAsync(enrollment))
            {
                _logger.LogWarning(
                    "Unauthorized attempt to access enrollment details. UserId={UserId}, TargetEnrollmentId={TargetEnrollmentId}, IP={IP}",
                    userId, id, ip);

                return Forbid();
            }

            var enrollmentReadDto = _mapper.Map<EnrollmentReadDto>(enrollment);
            return Ok(enrollmentReadDto);
        }

        // 3️⃣ Create new course enrollment (Admin, Student)
        [HttpPost]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> CreateEnrollment([FromBody] EnrollmentCreateDto enrollmentCreateDto)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CreateEnrollment. UserId={UserId}, IP={IP}", userId, ip);
                return BadRequest(ModelState);
            }

            // 🔐 Ownership Check for Student: force student to enroll ONLY themselves
            if (User.IsInRole("Student"))
            {
                var claimStudentId = User.FindFirst("StudentId")?.Value;
                if (int.TryParse(claimStudentId, out int currentStudentId))
                {
                    enrollmentCreateDto.StudentId = currentStudentId;
                }
            }

            // Verify Student exists
            var student = await _unitOfWork.Students.GetByIdAsync(enrollmentCreateDto.StudentId);
            if (student == null)
            {
                _logger.LogWarning(
                    "Enrollment failed (target student not found). RequestedBy={UserId}, StudentId={StudentId}, IP={IP}",
                    userId, enrollmentCreateDto.StudentId, ip);

                return NotFound($"Student with ID: {enrollmentCreateDto.StudentId} was not found.");
            }

            // Verify Course exists
            var course = await _unitOfWork.Courses.GetByIdAsync(enrollmentCreateDto.CourseId);
            if (course == null)
            {
                _logger.LogWarning(
                    "Enrollment failed (target course not found). RequestedBy={UserId}, CourseId={CourseId}, IP={IP}",
                    userId, enrollmentCreateDto.CourseId, ip);

                return NotFound($"Course with ID: {enrollmentCreateDto.CourseId} was not found.");
            }

            var enrollmentEntity = _mapper.Map<Enrollment>(enrollmentCreateDto);
            enrollmentEntity.EnrollmentDate = DateTime.UtcNow;
            enrollmentEntity.Status = "Active";

            await _unitOfWork.Enrollments.AddAsync(enrollmentEntity);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "New enrollment created successfully. CreatedBy={UserId}, CreatedEnrollmentId={CreatedEnrollmentId}, StudentId={StudentId}, CourseId={CourseId}, IP={IP}",
                userId, enrollmentEntity.EnrollmentId, enrollmentEntity.StudentId, enrollmentEntity.CourseId, ip);

            var enrollmentReadDto = _mapper.Map<EnrollmentReadDto>(enrollmentEntity);
            return CreatedAtAction(nameof(GetEnrollmentById), new { id = enrollmentReadDto.EnrollmentId }, enrollmentReadDto);
        }

        // 4️⃣ Update enrollment data (Admin, Instructor - Owner Only)
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> UpdateEnrollment(int id, [FromBody] EnrollmentCreateDto enrollmentUpdateDto)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for UpdateEnrollment. UserId={UserId}, TargetEnrollmentId={TargetEnrollmentId}, IP={IP}", userId, id, ip);
                return BadRequest(ModelState);
            }

            var enrollment = await _unitOfWork.Enrollments.GetByIdAsync(id);

            if (enrollment == null)
            {
                _logger.LogWarning(
                    "Update failed (enrollment not found). UserId={UserId}, TargetEnrollmentId={TargetEnrollmentId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Enrollment with ID: {id} was not found.");
            }

            // 🔐 Ownership Check
            if (!await IsAuthorizedToAccessEnrollmentAsync(enrollment))
            {
                _logger.LogWarning(
                    "Unauthorized attempt to update enrollment. UserId={UserId}, TargetEnrollmentId={TargetEnrollmentId}, IP={IP}",
                    userId, id, ip);

                return Forbid();
            }

            _mapper.Map(enrollmentUpdateDto, enrollment);

            _unitOfWork.Enrollments.Update(enrollment);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "Enrollment updated successfully. UpdatedBy={UserId}, EnrollmentId={EnrollmentId}, IP={IP}",
                userId, id, ip);

            return Ok("Enrollment updated successfully.");
        }

        // 5️⃣ Cancel/Delete enrollment record (Admin, Instructor - Owner Only)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> DeleteEnrollment(int id)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            var enrollment = await _unitOfWork.Enrollments.GetByIdAsync(id);

            if (enrollment == null)
            {
                _logger.LogWarning(
                    "Deletion failed (enrollment not found). UserId={UserId}, Action=DeleteEnrollment, TargetEnrollmentId={TargetEnrollmentId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Enrollment with ID: {id} was not found.");
            }

            // 🔐 Ownership Check
            if (!await IsAuthorizedToAccessEnrollmentAsync(enrollment))
            {
                _logger.LogWarning(
                    "Unauthorized attempt to delete enrollment. UserId={UserId}, TargetEnrollmentId={TargetEnrollmentId}, IP={IP}",
                    userId, id, ip);

                return Forbid();
            }

            _unitOfWork.Enrollments.Delete(enrollment);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "Enrollment deleted successfully. DeletedBy={UserId}, DeletedEnrollmentId={DeletedEnrollmentId}, IP={IP}",
                userId, id, ip);

            return Ok("Enrollment deleted successfully.");
        }
    }
}