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

        public EnrollmentsController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        // ==========================================
        // HELPER METHOD FOR OWNERSHIP CHECK
        // ==========================================
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

        // 1️⃣ إرجاع كل تسجيلات الكورسات (Admin Only)
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllEnrollments()
        {
            var enrollments = await _unitOfWork.Enrollments.GetAllAsync();
            var enrollmentsReadDto = _mapper.Map<IEnumerable<EnrollmentReadDto>>(enrollments);

            return Ok(enrollmentsReadDto);
        }

        // 2️⃣ إرجاع عملية تسجيل معينة برقم الـ ID (مع فحص الملكية)
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetEnrollmentById(int id)
        {
            var enrollment = await _unitOfWork.Enrollments.GetByIdAsync(id);

            if (enrollment == null)
                return NotFound($"Enrollment with ID: {id} was not found.");

            // 🔐 Ownership Check
            if (!await IsAuthorizedToAccessEnrollmentAsync(enrollment))
                return Forbid();

            var enrollmentReadDto = _mapper.Map<EnrollmentReadDto>(enrollment);
            return Ok(enrollmentReadDto);
        }

        // 3️⃣ تسجيل طالب في كورس جديد (Admin, Student)
        [HttpPost]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> CreateEnrollment([FromBody] EnrollmentCreateDto enrollmentCreateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 🔐 Ownership Check for Student: force student to enroll ONLY themselves
            if (User.IsInRole("Student"))
            {
                var claimStudentId = User.FindFirst("StudentId")?.Value;
                if (int.TryParse(claimStudentId, out int currentStudentId))
                {
                    enrollmentCreateDto.StudentId = currentStudentId;
                }
            }

            // 1. التأكد من وجود الطالب أولاً
            var student = await _unitOfWork.Students.GetByIdAsync(enrollmentCreateDto.StudentId);
            if (student == null)
                return NotFound($"Student with ID: {enrollmentCreateDto.StudentId} was not found.");

            // 2. التأكد من وجود الكورس أولاً
            var course = await _unitOfWork.Courses.GetByIdAsync(enrollmentCreateDto.CourseId);
            if (course == null)
                return NotFound($"Course with ID: {enrollmentCreateDto.CourseId} was not found.");

            // 3. تحويل الـ DTO لـ Entity وتحديد تاريخ التسجيل
            var enrollmentEntity = _mapper.Map<Enrollment>(enrollmentCreateDto);
            enrollmentEntity.EnrollmentDate = DateTime.UtcNow;
            enrollmentEntity.Status = "Active";

            // 4. الحفظ في الداتا بيز
            await _unitOfWork.Enrollments.AddAsync(enrollmentEntity);
            await _unitOfWork.CompleteAsync();

            var enrollmentReadDto = _mapper.Map<EnrollmentReadDto>(enrollmentEntity);
            return CreatedAtAction(nameof(GetEnrollmentById), new { id = enrollmentReadDto.EnrollmentId }, enrollmentReadDto);
        }

        // 4️⃣ تحديث بيانات التسجيل (Admin, Instructor - Owner Only)
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> UpdateEnrollment(int id, [FromBody] EnrollmentCreateDto enrollmentUpdateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var enrollment = await _unitOfWork.Enrollments.GetByIdAsync(id);

            if (enrollment == null)
                return NotFound($"Enrollment with ID: {id} was not found.");

            // 🔐 Ownership Check
            if (!await IsAuthorizedToAccessEnrollmentAsync(enrollment))
                return Forbid();

            _mapper.Map(enrollmentUpdateDto, enrollment);

            _unitOfWork.Enrollments.Update(enrollment);
            await _unitOfWork.CompleteAsync();

            return Ok("Enrollment updated successfully.");
        }

        // 5️⃣ إلغاء/حذف تسجيل طالب من كورس (Admin, Instructor - Owner Only)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> DeleteEnrollment(int id)
        {
            var enrollment = await _unitOfWork.Enrollments.GetByIdAsync(id);

            if (enrollment == null)
                return NotFound($"Enrollment with ID: {id} was not found.");

            // 🔐 Ownership Check
            if (!await IsAuthorizedToAccessEnrollmentAsync(enrollment))
                return Forbid();

            _unitOfWork.Enrollments.Delete(enrollment);
            await _unitOfWork.CompleteAsync();

            return Ok("Enrollment deleted successfully.");
        }
    }
}