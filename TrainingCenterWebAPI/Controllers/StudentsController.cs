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

        public StudentsController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllStudents()
        {
            var students = await _unitOfWork.Students.GetAllAsync();

            var studentsDto = _mapper.Map<IEnumerable<StudentReadDto>>(students);

            return Ok(studentsDto);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetStudentById(int id)
        {
            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
                return NotFound($"Student with ID {id} was not found.");

            var studentDto = _mapper.Map<StudentReadDto>(student);
            return Ok(studentDto);
        }

        [HttpPost]
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

        [HttpPut("{id:int}/profile")]
        public async Task<IActionResult> UpdateProfile(int id, [FromBody] StudentProfileCreateDto profileCreateDto)
        {
            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
                return NotFound($"Student with ID {id} was not found.");

            var profile = (await _unitOfWork.StudentProfiles
                .FindAsync(p => p.StudentId == id))
                .FirstOrDefault();

            if (profile == null)
                return NotFound("Student profile was not found.");

            _mapper.Map(profileCreateDto, profile);

            _unitOfWork.StudentProfiles.Update(profile);
            await _unitOfWork.CompleteAsync();

            return Ok("Profile updated successfully.");
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _unitOfWork.Students.GetByIdAsync(id);

            if (student == null)
                return NotFound($"Student with ID {id} was not found.");

            _unitOfWork.Students.Delete(student);
            await _unitOfWork.CompleteAsync();

            return Ok("Student deleted successfully.");
        }
    }
}