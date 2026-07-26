using System.ComponentModel.DataAnnotations;

namespace TrainingCenter.DTOs.Student
{
    public class StudentCreateDto
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        public DateOnly DateOfBirth { get; set; }

        [StringLength(30)]
        public string? PhoneNumber { get; set; }
    }
}