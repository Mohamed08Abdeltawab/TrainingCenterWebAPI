using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TrainingCenter.DTOs.Instructor;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;

namespace TrainingCenterWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InstructorsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public InstructorsController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllInstructors()
        {
            var instructors = await _unitOfWork.Instructors.GetAllAsync();
            var instructorsReadDto = _mapper.Map<IEnumerable<InstructorReadDto>>(instructors);

            return Ok(instructorsReadDto);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetInstructorById(int id)
        {
            var instructor = await _unitOfWork.Instructors.GetByIdAsync(id);

            if (instructor == null)
                return NotFound($"Instructor with ID: {id} was not found.");

            var instructorReadDto = _mapper.Map<InstructorReadDto>(instructor);
            return Ok(instructorReadDto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateInstructor([FromBody] InstructorCreateDto instructorCreateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var instructorEntity = _mapper.Map<Instructor>(instructorCreateDto);

            await _unitOfWork.Instructors.AddAsync(instructorEntity);
            await _unitOfWork.CompleteAsync();

            var instructorReadDto = _mapper.Map<InstructorReadDto>(instructorEntity);
            return CreatedAtAction(nameof(GetInstructorById), new { id = instructorReadDto.InstructorId }, instructorReadDto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateInstructor(int id, [FromBody] InstructorCreateDto instructorUpdateDto)
        {
            var instructor = await _unitOfWork.Instructors.GetByIdAsync(id);

            if (instructor == null)
                return NotFound($"Instructor with ID: {id} was not found.");

            _mapper.Map(instructorUpdateDto, instructor);

            _unitOfWork.Instructors.Update(instructor);
            await _unitOfWork.CompleteAsync();

            return Ok("Instructor updated successfully.");
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteInstructor(int id)
        {
            var instructor = await _unitOfWork.Instructors.GetByIdAsync(id);

            if (instructor == null)
                return NotFound($"Instructor with ID: {id} was not found.");

            _unitOfWork.Instructors.Delete(instructor);
            await _unitOfWork.CompleteAsync();

            return Ok("Instructor deleted successfully.");
        }
    }
}