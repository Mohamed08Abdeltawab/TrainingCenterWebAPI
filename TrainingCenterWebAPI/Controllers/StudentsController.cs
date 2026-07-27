using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        [HttpGet]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> GetAllStudents()
        {
            var students = await _unitOfWork.Students.GetAllAsync();

            var studentsDto = _mapper.Map<IEnumerable<StudentReadDto>>(students);

            return Ok(studentsDto);
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetStudentById(int id)
        {
            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
                return NotFound($"Student with ID {id} was not found.");

            var studentDto = _mapper.Map<StudentReadDto>(student);
            return Ok(studentDto);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateStudent([FromBody] StudentCreateDto studentCreateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var studentEntity = _mapper.Map<Student>(studentCreateDto);

            studentEntity.RegisteredAt = DateTime.Now;
            studentEntity.Status = "Active";

            await _unitOfWork.Students.AddAsync(studentEntity);
            await _unitOfWork.CompleteAsync();

            var studentReadDto = _mapper.Map<StudentReadDto>(studentEntity);

            return CreatedAtAction(nameof(GetStudentById), new { id = studentReadDto.StudentId }, studentReadDto);
        }

        // 6️⃣ تحديث بيانات الطالب نفسه (PUT: api/Students/{id})
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentCreateDto studentUpdateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 1. البحث عن الطالب في الداتا بيز
            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
                return NotFound($"Student with ID: {id} was not found.");

            // 2. عمل Mapping للبيانات الجديدة فوق الكائن الموجود
            _mapper.Map(studentUpdateDto, student);

            // 3. تحديث البيانات والحفظ
            _unitOfWork.Students.Update(student);
            await _unitOfWork.CompleteAsync();

            return Ok("Student data updated successfully.");
        }


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


        // 1️⃣ جلب بروفايل طالب معين (GET: api/Students/{id}/profile)
        [HttpGet("{id:int}/profile")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> GetStudentProfile(int id)
        {
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

        // 2️⃣ إضافة أو تحديث بروفايل الطالب (PUT: api/Students/{id}/profile)
        [HttpPut("{id:int}/profile")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> AddOrUpdateStudentProfile(int id, [FromBody] StudentProfileCreateDto profileDto)
        {
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
                // إنشاء جديد
                var newProfile = _mapper.Map<StudentProfile>(profileDto);
                newProfile.StudentId = id;
                await _unitOfWork.StudentProfiles.AddAsync(newProfile);
            }
            else
            {
                // تحديث الموجود
                _mapper.Map(profileDto, existingProfile);
                _unitOfWork.StudentProfiles.Update(existingProfile);
            }

            await _unitOfWork.CompleteAsync();
            return Ok("Student profile saved successfully.");
        }

        // 3️⃣ حذف بروفايل الطالب (DELETE: api/Students/{id}/profile)
        [HttpDelete("{id:int}/profile")]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> DeleteStudentProfile(int id)
        {
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