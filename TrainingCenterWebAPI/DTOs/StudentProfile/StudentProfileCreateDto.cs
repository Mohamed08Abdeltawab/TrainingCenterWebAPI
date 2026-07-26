using System.ComponentModel.DataAnnotations;

namespace TrainingCenter.DTOs.StudentProfile
{
    public class StudentProfileCreateDto
    {
        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        [StringLength(200)]
        [Url(ErrorMessage = "Invalid LinkedIn URL format")]
        public string? LinkedInUrl { get; set; }
    }
}