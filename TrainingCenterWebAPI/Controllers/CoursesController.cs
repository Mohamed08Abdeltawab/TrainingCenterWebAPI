using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrainingCenter.DTOs.Course;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;

namespace TrainingCenterWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CoursesController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<CoursesController> _logger;

        public CoursesController(IUnitOfWork unitOfWork, IMapper mapper, ILogger<CoursesController> logger)
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

        private bool IsAuthorizedToManageCourse(Course course)
        {
            // Admin has full control over all courses
            if (User.IsInRole("Admin"))
                return true;

            // Instructor can only manage courses where they are assigned as the instructor
            if (User.IsInRole("Instructor"))
            {
                var claimInstructorId = User.FindFirst("InstructorId")?.Value;
                if (int.TryParse(claimInstructorId, out int currentInstructorId))
                {
                    return course.InstructorId == currentInstructorId;
                }
            }

            return false;
        }

        // ==========================================
        // COURSE ENDPOINTS
        // ==========================================

        // 1️⃣ Get all courses (Admin, Instructor, Student)
        [HttpGet]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetAllCourses()
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            var courses = await _unitOfWork.Courses.GetAllAsync();
            var coursesReadDto = _mapper.Map<IEnumerable<CourseReadDto>>(courses);

            _logger.LogInformation("Retrieved all courses list. RequestedBy={UserId}, IP={IP}", userId, ip);

            return Ok(coursesReadDto);
        }

        // 2️⃣ Get course by ID (Admin, Instructor, Student)
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetCourseById(int id)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            var course = await _unitOfWork.Courses.GetByIdAsync(id);

            if (course == null)
            {
                _logger.LogWarning(
                    "Course requested but not found. UserId={UserId}, TargetCourseId={TargetCourseId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Course with ID: {id} not found.");
            }

            var courseDto = _mapper.Map<CourseReadDto>(course);
            return Ok(courseDto);
        }

        // 3️⃣ Create a new course (Admin, Instructor)
        [HttpPost]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> CreateCourse([FromBody] CourseCreateDto courseCreateDto)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CreateCourse. UserId={UserId}, IP={IP}", userId, ip);
                return BadRequest(ModelState);
            }

            var courseEntity = _mapper.Map<Course>(courseCreateDto);

            // If an Instructor creates a course, force the InstructorId to be their OWN Id from Token
            if (User.IsInRole("Instructor"))
            {
                var claimInstructorId = User.FindFirst("InstructorId")?.Value;
                if (int.TryParse(claimInstructorId, out int currentInstructorId))
                {
                    courseEntity.InstructorId = currentInstructorId;
                }
            }

            // Set default properties
            courseEntity.Status = "Draft";
            courseEntity.CreatedAt = DateTime.UtcNow;
            if (string.IsNullOrEmpty(courseEntity.Level))
            {
                courseEntity.Level = "Beginner";
            }

            await _unitOfWork.Courses.AddAsync(courseEntity);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "New course created successfully. CreatedBy={UserId}, CreatedCourseId={CreatedCourseId}, InstructorId={InstructorId}, IP={IP}",
                userId, courseEntity.CourseId, courseEntity.InstructorId, ip);

            var courseReadDto = _mapper.Map<CourseReadDto>(courseEntity);
            return CreatedAtAction(nameof(GetCourseById), new { id = courseReadDto.CourseId }, courseReadDto);
        }

        // 4️⃣ Update course data (Admin, Instructor - Owner Only)
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] CourseCreateDto courseUpdateDto)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for UpdateCourse. UserId={UserId}, TargetCourseId={TargetCourseId}, IP={IP}", userId, id, ip);
                return BadRequest(ModelState);
            }

            var course = await _unitOfWork.Courses.GetByIdAsync(id);

            if (course == null)
            {
                _logger.LogWarning(
                    "Update failed (course not found). UserId={UserId}, TargetCourseId={TargetCourseId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Course with ID: {id} was not found.");
            }

            // 🔐 Ownership Check
            if (!IsAuthorizedToManageCourse(course))
            {
                _logger.LogWarning(
                    "Unauthorized attempt to update course. UserId={UserId}, TargetCourseId={TargetCourseId}, IP={IP}",
                    userId, id, ip);

                return Forbid(); // 403 Forbidden if instructor tries to edit someone else's course
            }

            _mapper.Map(courseUpdateDto, course);

            // Prevent Instructor from reassigning the course to another instructor via DTO update
            if (User.IsInRole("Instructor"))
            {
                var claimInstructorId = User.FindFirst("InstructorId")?.Value;
                if (int.TryParse(claimInstructorId, out int currentInstructorId))
                {
                    course.InstructorId = currentInstructorId;
                }
            }

            _unitOfWork.Courses.Update(course);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "Course updated successfully. UpdatedBy={UserId}, CourseId={CourseId}, IP={IP}",
                userId, id, ip);

            return Ok("Course updated successfully.");
        }

        // 5️⃣ Delete course (Admin, Instructor - Owner Only)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            var course = await _unitOfWork.Courses.GetByIdAsync(id);

            if (course == null)
            {
                _logger.LogWarning(
                    "Deletion failed (course not found). UserId={UserId}, Action=DeleteCourse, TargetCourseId={TargetCourseId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Course with ID: {id} was not found.");
            }

            // 🔐 Ownership Check
            if (!IsAuthorizedToManageCourse(course))
            {
                _logger.LogWarning(
                    "Unauthorized attempt to delete course. UserId={UserId}, TargetCourseId={TargetCourseId}, IP={IP}",
                    userId, id, ip);

                return Forbid(); // 403 Forbidden if instructor tries to delete someone else's course
            }

            _unitOfWork.Courses.Delete(course);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "Course deleted successfully. DeletedBy={UserId}, DeletedCourseId={DeletedCourseId}, IP={IP}",
                userId, id, ip);

            return Ok("Course deleted successfully.");
        }
    }
}