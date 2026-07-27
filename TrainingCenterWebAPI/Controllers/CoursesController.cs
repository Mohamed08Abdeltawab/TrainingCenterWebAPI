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

        public CoursesController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        // ==========================================
        // HELPER METHOD FOR OWNERSHIP CHECK
        // ==========================================
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

        // 1️⃣ عرض جميع الكورسات (Admin, Instructor, Student)
        [HttpGet]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetAllCourses()
        {
            var courses = await _unitOfWork.Courses.GetAllAsync();
            var coursesReadDto = _mapper.Map<IEnumerable<CourseReadDto>>(courses);

            return Ok(coursesReadDto);
        }

        // 2️⃣ عرض كورس محدد بـ ID (Admin, Instructor, Student)
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetCourseById(int id)
        {
            var course = await _unitOfWork.Courses.GetByIdAsync(id);

            if (course == null)
                return NotFound($"Course with ID: {id} not found.");

            var courseDto = _mapper.Map<CourseReadDto>(course);
            return Ok(courseDto);
        }

        // 3️⃣ إنشاء كورس جديد (Admin, Instructor)
        [HttpPost]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> CreateCourse([FromBody] CourseCreateDto courseCreateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

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

            // تعيين القيم الافتراضية
            courseEntity.Status = "Draft";
            courseEntity.CreatedAt = DateTime.UtcNow;
            if (string.IsNullOrEmpty(courseEntity.Level))
            {
                courseEntity.Level = "Beginner";
            }

            await _unitOfWork.Courses.AddAsync(courseEntity);
            await _unitOfWork.CompleteAsync();

            var courseReadDto = _mapper.Map<CourseReadDto>(courseEntity);
            return CreatedAtAction(nameof(GetCourseById), new { id = courseReadDto.CourseId }, courseReadDto);
        }

        // 4️⃣ تحديث بيانات كورس (Admin, Instructor - Owner Only)
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] CourseCreateDto courseUpdateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var course = await _unitOfWork.Courses.GetByIdAsync(id);

            if (course == null)
                return NotFound($"Course with ID: {id} was not found.");

            // 🔐 Ownership Check
            if (!IsAuthorizedToManageCourse(course))
                return Forbid(); // 403 Forbidden if instructor tries to edit someone else's course

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

            return Ok("Course updated successfully.");
        }

        // 5️⃣ حذف كورس (Admin, Instructor - Owner Only)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _unitOfWork.Courses.GetByIdAsync(id);

            if (course == null)
                return NotFound($"Course with ID: {id} was not found.");

            // 🔐 Ownership Check
            if (!IsAuthorizedToManageCourse(course))
                return Forbid(); // 403 Forbidden if instructor tries to delete someone else's course

            _unitOfWork.Courses.Delete(course);
            await _unitOfWork.CompleteAsync();

            return Ok("Course deleted successfully.");
        }
    }
}