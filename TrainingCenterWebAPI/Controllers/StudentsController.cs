using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TrainingCenter.DTOs.Student;
using TrainingCenter.DTOs.StudentProfile;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;

namespace TrainingCenter.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        // بنعمل Inject للـ UnitOfWork والـ AutoMapper
        public StudentsController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        // 1️⃣ إرجاع كل الطلاب (GET: api/students)
        [HttpGet]
        public async Task<IActionResult> GetAllStudents()
        {
            var students = await _unitOfWork.Students.GetAllAsync();

            // تحويل قائمة الـ Entities إلى قائمة DTOs
            var studentsDto = _mapper.Map<IEnumerable<StudentReadDto>>(students);

            return Ok(studentsDto);
        }

        // 2️⃣ إرجاع طالب برقم الـ ID (GET: api/students/{id})
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetStudentById(int id)
        {
            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
                return NotFound($"Student with ID {id} was not found.");

            var studentDto = _mapper.Map<StudentReadDto>(student);
            return Ok(studentDto);
        }

        // 3️⃣ إضافة طالب جديد (POST: api/students)
        [HttpPost(Name = "AddNewStudent")]
        public async Task<IActionResult> CreateStudent([FromBody] StudentCreateDto studentCreateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 1. تحويل الـ DTO إلى Entity
            var studentEntity = _mapper.Map<Student>(studentCreateDto);

            // 2. تعيين القيم الافتراضية للكيان
            studentEntity.RegisteredAt = DateTime.Now;
            studentEntity.Status = "Active";

            // 3. الحفظ في الداتا بيز عبر الـ Unit of Work
            await _unitOfWork.Students.AddAsync(studentEntity);
            await _unitOfWork.CompleteAsync();

            // 4. تحويل الناتج لـ ReadDto لإرجاعه للزبون
            var studentReadDto = _mapper.Map<StudentReadDto>(studentEntity);

            return CreatedAtAction(nameof(GetStudentById), new { id = studentReadDto.StudentId }, studentReadDto);
        }

        [HttpPost("{id:int}/profile")]
        public async Task<IActionResult> AddOrUpdateProfile(int id,[FromBody] StudentProfileCreateDto profileDto)
        {
            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
                return NotFound($"Student with ID {id} was not found.");

            var existingProfile = (await _unitOfWork.StudentProfiles
                .FindAsync(p => p.StudentId == id))
                .FirstOrDefault();

            if (existingProfile == null)
            {
                var profile = _mapper.Map<StudentProfile>(profileDto);
                profile.StudentId = id;

                await _unitOfWork.StudentProfiles.AddAsync(profile);
            }
            else
            {
                _mapper.Map(profileDto, existingProfile);
                _unitOfWork.StudentProfiles.Update(existingProfile);
            }

            await _unitOfWork.CompleteAsync();

            return Ok("Profile saved successfully.");
        }
    }
}