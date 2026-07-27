using System.ComponentModel.DataAnnotations;

namespace TrainingCenter.DTOs.Auth
{
    public class RefreshRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}