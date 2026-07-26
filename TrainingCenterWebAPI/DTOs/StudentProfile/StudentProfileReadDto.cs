namespace TrainingCenter.DTOs.StudentProfile
{
    public class StudentProfileReadDto
    {
        public int StudentId { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Bio { get; set; }
        public string? LinkedInUrl { get; set; }
    }
}