using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrainingCenter.DTOs.Student;
using TrainingCenter.DTOs.StudentProfile;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;

namespace TrainingCenter.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StudentsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public StudentsController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        // ==========================================
        // HELPER METHOD FOR OWNERSHIP CHECK
        // ==========================================
        private bool IsAuthorizedStudentOrAdmin(int targetStudentId)
        {
            // Admins & Instructors bypass ownership check (if role permits action)
            if (User.IsInRole("Admin") || User.IsInRole("Instructor"))
                return true;

            // If user is a Student, ensure they are requesting their OWN data
            if (User.IsInRole("Student"))
            {
                var claimStudentId = User.FindFirst("StudentId")?.Value;//search in token of claims of variable "StudentId" then we return value.
                if (int.TryParse(claimStudentId, out int currentStudentId))
                {
                    return currentStudentId == targetStudentId;
                }
            }

            return false;
        }

        // 1️⃣ عرض كل الطلاب
        [HttpGet]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> GetAllStudents()
        {
            var students = await _unitOfWork.Students.GetAllAsync();
            var studentsDto = _mapper.Map<IEnumerable<StudentReadDto>>(students);
            return Ok(studentsDto);
        }

        // 2️⃣ عرض طالب بـ ID (مع فحص الملكية)
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetStudentById(int id)
        {
            if (!IsAuthorizedStudentOrAdmin(id))
                return Forbid(); // 403 Forbidden if student tries to access another student's data

            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
                return NotFound($"Student with ID {id} was not found.");

            var studentDto = _mapper.Map<StudentReadDto>(student);
            return Ok(studentDto);
        }

        // 3️⃣ إنشاء طالب جديد (Admin Only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateStudent([FromBody] StudentCreateDto studentCreateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var studentEntity = _mapper.Map<Student>(studentCreateDto);
            studentEntity.RegisteredAt = DateTime.UtcNow;
            studentEntity.Status = "Active";

            await _unitOfWork.Students.AddAsync(studentEntity);
            await _unitOfWork.CompleteAsync();

            var studentReadDto = _mapper.Map<StudentReadDto>(studentEntity);

            return CreatedAtAction(nameof(GetStudentById), new { id = studentReadDto.StudentId }, studentReadDto);
        }

        // 4️⃣ تحديث بيانات طالب (مع فحص الملكية)
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentCreateDto studentUpdateDto)
        {
            if (!IsAuthorizedStudentOrAdmin(id))
                return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var student = await _unitOfWork.Students.GetByIdAsync(id);
            if (student == null)
                return NotFound($"Student with ID: {id} was not found.");

            _mapper.Map(studentUpdateDto, student);

            _unitOfWork.Students.Update(student);
            await _unitOfWork.CompleteAsync();

            return Ok("Student data updated successfully.");
        }

        // 5️⃣ حذف طالب (Admin Only)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
                return NotFound($"Student with ID {id} was not found.");

            _unitOfWork.Students.Delete(student);
            await _unitOfWork.CompleteAsync();

            return Ok("Student deleted successfully.");
        }

        // ==========================================
        // STUDENT PROFILE ENDPOINTS
        // ==========================================

        // 6️⃣ جلب بروفايل طالب معين (مع فحص الملكية)
        [HttpGet("{id:int}/profile")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> GetStudentProfile(int id)
        {
            if (!IsAuthorizedStudentOrAdmin(id))
                return Forbid();

            var student = await _unitOfWork.Students.GetByIdAsync(id);
            if (student == null)
                return NotFound($"Student with ID: {id} was not found.");

            var profile = (await _unitOfWork.StudentProfiles
                .FindAsync(p => p.StudentId == id))
                .FirstOrDefault();

            if (profile == null)
                return NotFound($"Profile for Student ID: {id} was not found.");

            var profileReadDto = _mapper.Map<StudentProfileReadDto>(profile);
            return Ok(profileReadDto);
        }

        // 7️⃣ إضافة أو تحديث بروفايل الطالب (مع فحص الملكية)
        [HttpPut("{id:int}/profile")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> AddOrUpdateStudentProfile(int id, [FromBody] StudentProfileCreateDto profileDto)
        {
            if (!IsAuthorizedStudentOrAdmin(id))
                return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var student = await _unitOfWork.Students.GetByIdAsync(id);
            if (student == null)
                return NotFound($"Student with ID: {id} was not found.");

            var existingProfile = (await _unitOfWork.StudentProfiles
                .FindAsync(p => p.StudentId == id))
                .FirstOrDefault();

            if (existingProfile == null)
            {
                var newProfile = _mapper.Map<StudentProfile>(profileDto);
                newProfile.StudentId = id;
                await _unitOfWork.StudentProfiles.AddAsync(newProfile);
            }
            else
            {
                _mapper.Map(profileDto, existingProfile);
                _unitOfWork.StudentProfiles.Update(existingProfile);
            }

            await _unitOfWork.CompleteAsync();
            return Ok("Student profile saved successfully.");
        }

        // 8️⃣ حذف بروفايل الطالب (مع فحص الملكية)
        [HttpDelete("{id:int}/profile")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> DeleteStudentProfile(int id)
        {
            if (!IsAuthorizedStudentOrAdmin(id))
                return Forbid();

            var profile = (await _unitOfWork.StudentProfiles
                .FindAsync(p => p.StudentId == id))
                .FirstOrDefault();

            if (profile == null)
                return NotFound($"Profile for Student ID: {id} was not found.");

            _unitOfWork.StudentProfiles.Delete(profile);
            await _unitOfWork.CompleteAsync();

            return Ok("Student profile deleted successfully.");
        }
    }
}