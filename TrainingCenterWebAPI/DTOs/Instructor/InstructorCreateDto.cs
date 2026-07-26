using System.ComponentModel.DataAnnotations;

namespace TrainingCenter.DTOs.Instructor
{
    public class InstructorCreateDto
    {
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        public DateOnly HireDate { get; set; }

        public decimal Salary { get; set; }

        public int? ManagerId { get; set; }
    }
}