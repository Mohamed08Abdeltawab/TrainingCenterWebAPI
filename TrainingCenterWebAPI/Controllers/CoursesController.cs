using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TrainingCenter.DTOs.Course;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;

namespace TrainingCenterWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoursesController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CoursesController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCourses()
        {
            var courses = await _unitOfWork.Courses.GetAllAsync();
            var coursesReadDto = _mapper.Map<IEnumerable<CourseReadDto>>(courses);

            return Ok(coursesReadDto);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCourseById(int id)
        {
            var course = await _unitOfWork.Courses.GetByIdAsync(id);

            if (course == null)
                return NotFound($"Course with ID: {id} not found.");

            var courseDto = _mapper.Map<CourseReadDto>(course);
            return Ok(courseDto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCourse([FromBody] CourseCreateDto courseCreateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var courseEntity = _mapper.Map<Course>(courseCreateDto);

            // تعيين القيم الافتراضية
            courseEntity.Status = "Draft";
            courseEntity.CreatedAt = DateTime.Now;
            if (string.IsNullOrEmpty(courseEntity.Level))
            {
                courseEntity.Level = "Beginner";
            }

            await _unitOfWork.Courses.AddAsync(courseEntity);
            await _unitOfWork.CompleteAsync();

            var courseReadDto = _mapper.Map<CourseReadDto>(courseEntity);
            return CreatedAtAction(nameof(GetCourseById), new { id = courseReadDto.CourseId }, courseReadDto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] CourseCreateDto courseUpdateDto)
        {
            var course = await _unitOfWork.Courses.GetByIdAsync(id);

            if (course == null)
                return NotFound($"Course with ID: {id} was not found.");

            _mapper.Map(courseUpdateDto, course);

            _unitOfWork.Courses.Update(course);
            await _unitOfWork.CompleteAsync();

            return Ok("Course updated successfully.");
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _unitOfWork.Courses.GetByIdAsync(id);

            if (course == null)
                return NotFound($"Course with ID: {id} was not found.");

            _unitOfWork.Courses.Delete(course);
            await _unitOfWork.CompleteAsync();

            return Ok("Course deleted successfully.");
        }
    }
}