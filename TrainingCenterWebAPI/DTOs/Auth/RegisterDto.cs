using System.ComponentModel.DataAnnotations;

namespace TrainingCenter.DTOs.Auth
{
    public class RegisterDto
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Student"; // Admin, Instructor, or Student

        public int? InstructorId { get; set; }
        public int? StudentId { get; set; }
    }
}