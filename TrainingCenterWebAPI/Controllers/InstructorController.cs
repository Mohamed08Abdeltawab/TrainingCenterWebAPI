using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrainingCenter.DTOs.Instructor;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;

namespace TrainingCenterWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InstructorsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public InstructorsController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        // ==========================================
        // HELPER METHOD FOR OWNERSHIP CHECK
        // ==========================================
        private bool IsAuthorizedInstructorOrAdmin(int targetInstructorId)
        {
            // Admins have full access
            if (User.IsInRole("Admin"))
                return true;

            // Instructors can only edit their OWN profile
            if (User.IsInRole("Instructor"))
            {
                var claimInstructorId = User.FindFirst("InstructorId")?.Value;
                if (int.TryParse(claimInstructorId, out int currentInstructorId))
                {
                    return currentInstructorId == targetInstructorId;
                }
            }

            return false;
        }

        // 1️⃣ إرجاع كل المحاضرين (Admin, Instructor, Student)
        [HttpGet]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetAllInstructors()
        {
            var instructors = await _unitOfWork.Instructors.GetAllAsync();
            var instructorsReadDto = _mapper.Map<IEnumerable<InstructorReadDto>>(instructors);

            return Ok(instructorsReadDto);
        }

        // 2️⃣ إرجاع محاضر برقم الـ ID (Admin, Instructor, Student)
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Instructor,Student")]
        public async Task<IActionResult> GetInstructorById(int id)
        {
            var instructor = await _unitOfWork.Instructors.GetByIdAsync(id);

            if (instructor == null)
                return NotFound($"Instructor with ID: {id} was not found.");

            var instructorReadDto = _mapper.Map<InstructorReadDto>(instructor);
            return Ok(instructorReadDto);
        }

        // 3️⃣ إضافة محاضر جديد (Admin Only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
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

        // 4️⃣ تحديث بيانات محاضر (Admin, Instructor - Owner Only)
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> UpdateInstructor(int id, [FromBody] InstructorCreateDto instructorUpdateDto)
        {
            // 🔐 Ownership Check
            if (!IsAuthorizedInstructorOrAdmin(id))
                return Forbid(); // 403 Forbidden if instructor tries to edit another instructor's profile

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var instructor = await _unitOfWork.Instructors.GetByIdAsync(id);

            if (instructor == null)
                return NotFound($"Instructor with ID: {id} was not found.");

            _mapper.Map(instructorUpdateDto, instructor);

            _unitOfWork.Instructors.Update(instructor);
            await _unitOfWork.CompleteAsync();

            return Ok("Instructor updated successfully.");
        }

        // 5️⃣ حذف محاضر (Admin Only)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
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