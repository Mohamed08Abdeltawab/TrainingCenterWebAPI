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
        private readonly ILogger<StudentsController> _logger;

        public StudentsController(IUnitOfWork unitOfWork, IMapper mapper, ILogger<StudentsController> logger)
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

        private bool IsAuthorizedStudentOrAdmin(int targetStudentId)
        {
            // Admins & Instructors bypass ownership check (if role permits action)
            if (User.IsInRole("Admin") || User.IsInRole("Instructor"))
                return true;

            // If user is a Student, ensure they are requesting their OWN data
            if (User.IsInRole("Student"))
            {
                var claimStudentId = User.FindFirst("StudentId")?.Value;
                if (int.TryParse(claimStudentId, out int currentStudentId))
                {
                    return currentStudentId == targetStudentId;
                }
            }

            return false;
        }

        // ==========================================
        // STUDENT ENDPOINTS
        // ==========================================

        // 1️⃣ Get all students
        [HttpGet]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> GetAllStudents()
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            var students = await _unitOfWork.Students.GetAllAsync();
            var studentsDto = _mapper.Map<IEnumerable<StudentReadDto>>(students);

            _logger.LogInformation("Retrieved all students list. RequestedBy={UserId}, IP={IP}", userId, ip);

            return Ok(studentsDto);
        }

        // 2️⃣ Get student by ID
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetStudentById(int id)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            if (!IsAuthorizedStudentOrAdmin(id))
            {
                _logger.LogWarning(
                    "Unauthorized access attempt to student details. UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return Forbid();
            }

            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
            {
                _logger.LogWarning(
                    "Student requested but not found. UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Student with ID {id} was not found.");
            }

            var studentDto = _mapper.Map<StudentReadDto>(student);
            return Ok(studentDto);
        }

        // 3️⃣ Create new student (Admin Only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateStudent([FromBody] StudentCreateDto studentCreateDto)
        {
            var adminId = GetUserId();
            var ip = GetCallerIp();

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CreateStudent. AdminId={AdminId}, IP={IP}", adminId, ip);
                return BadRequest(ModelState);
            }

            var studentEntity = _mapper.Map<Student>(studentCreateDto);
            studentEntity.RegisteredAt = DateTime.UtcNow;
            studentEntity.Status = "Active";

            await _unitOfWork.Students.AddAsync(studentEntity);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "New student created successfully. AdminId={AdminId}, CreatedStudentId={CreatedStudentId}, IP={IP}",
                adminId, studentEntity.StudentId, ip);

            var studentReadDto = _mapper.Map<StudentReadDto>(studentEntity);
            return CreatedAtAction(nameof(GetStudentById), new { id = studentReadDto.StudentId }, studentReadDto);
        }

        // 4️⃣ Update student data
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentCreateDto studentUpdateDto)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            if (!IsAuthorizedStudentOrAdmin(id))
            {
                _logger.LogWarning(
                    "Unauthorized attempt to update student data. UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for UpdateStudent. UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}", userId, id, ip);
                return BadRequest(ModelState);
            }

            var student = await _unitOfWork.Students.GetByIdAsync(id);
            if (student == null)
            {
                _logger.LogWarning(
                    "Update failed (student not found). UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Student with ID: {id} was not found.");
            }

            _mapper.Map(studentUpdateDto, student);

            _unitOfWork.Students.Update(student);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "Student data updated successfully. UpdatedBy={UserId}, StudentId={StudentId}, IP={IP}",
                userId, id, ip);

            return Ok("Student data updated successfully.");
        }

        // 5️⃣ Delete student (Admin Only)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var adminId = GetUserId();
            var ip = GetCallerIp();

            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
            {
                _logger.LogWarning(
                    "Admin action failed (target student not found). AdminId={AdminId}, Action=DeleteStudent, TargetStudentId={TargetStudentId}, IP={IP}",
                    adminId, id, ip);

                return NotFound($"Student with ID {id} was not found.");
            }

            _unitOfWork.Students.Delete(student);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "Student deleted successfully. AdminId={AdminId}, DeletedStudentId={DeletedStudentId}, IP={IP}",
                adminId, id, ip);

            return Ok("Student deleted successfully.");
        }

        // ==========================================
        // STUDENT PROFILE ENDPOINTS
        // ==========================================

        // 6️⃣ Get student profile
        [HttpGet("{id:int}/profile")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> GetStudentProfile(int id)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            if (!IsAuthorizedStudentOrAdmin(id))
            {
                _logger.LogWarning(
                    "Unauthorized attempt to access student profile. UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return Forbid();
            }

            var student = await _unitOfWork.Students.GetByIdAsync(id);
            if (student == null)
            {
                _logger.LogWarning(
                    "Student profile request failed (student not found). UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Student with ID: {id} was not found.");
            }

            var profile = (await _unitOfWork.StudentProfiles
                .FindAsync(p => p.StudentId == id))
                .FirstOrDefault();

            if (profile == null)
            {
                _logger.LogInformation(
                    "Student profile not found. UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Profile for Student ID: {id} was not found.");
            }

            var profileReadDto = _mapper.Map<StudentProfileReadDto>(profile);
            return Ok(profileReadDto);
        }

        // 7️⃣ Add or update student profile
        [HttpPut("{id:int}/profile")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> AddOrUpdateStudentProfile(int id, [FromBody] StudentProfileCreateDto profileDto)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            if (!IsAuthorizedStudentOrAdmin(id))
            {
                _logger.LogWarning(
                    "Unauthorized attempt to modify student profile. UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for AddOrUpdateStudentProfile. UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}", userId, id, ip);
                return BadRequest(ModelState);
            }

            var student = await _unitOfWork.Students.GetByIdAsync(id);
            if (student == null)
            {
                _logger.LogWarning(
                    "Profile action failed (student not found). UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Student with ID: {id} was not found.");
            }

            var existingProfile = (await _unitOfWork.StudentProfiles
                .FindAsync(p => p.StudentId == id))
                .FirstOrDefault();

            if (existingProfile == null)
            {
                var newProfile = _mapper.Map<StudentProfile>(profileDto);
                newProfile.StudentId = id;
                await _unitOfWork.StudentProfiles.AddAsync(newProfile);

                _logger.LogInformation("New student profile created. ModifiedBy={UserId}, StudentId={StudentId}, IP={IP}", userId, id, ip);
            }
            else
            {
                _mapper.Map(profileDto, existingProfile);
                _unitOfWork.StudentProfiles.Update(existingProfile);

                _logger.LogInformation("Existing student profile updated. ModifiedBy={UserId}, StudentId={StudentId}, IP={IP}", userId, id, ip);
            }

            await _unitOfWork.CompleteAsync();
            return Ok("Student profile saved successfully.");
        }

        // 8️⃣ Delete student profile
        [HttpDelete("{id:int}/profile")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> DeleteStudentProfile(int id)
        {
            var userId = GetUserId();
            var ip = GetCallerIp();

            if (!IsAuthorizedStudentOrAdmin(id))
            {
                _logger.LogWarning(
                    "Unauthorized attempt to delete student profile. UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return Forbid();
            }

            var profile = (await _unitOfWork.StudentProfiles
                .FindAsync(p => p.StudentId == id))
                .FirstOrDefault();

            if (profile == null)
            {
                _logger.LogWarning(
                    "Profile deletion failed (profile not found). UserId={UserId}, TargetStudentId={TargetStudentId}, IP={IP}",
                    userId, id, ip);

                return NotFound($"Profile for Student ID: {id} was not found.");
            }

            _unitOfWork.StudentProfiles.Delete(profile);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Student profile deleted successfully. DeletedBy={UserId}, StudentId={StudentId}, IP={IP}", userId, id, ip);

            return Ok("Student profile deleted successfully.");
        }
    }
}