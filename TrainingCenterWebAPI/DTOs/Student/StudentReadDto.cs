using TrainingCenter.DTOs.StudentProfile;

namespace TrainingCenter.DTOs.Student
{
    public class StudentReadDto
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }
        public DateTime RegisteredAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }

        // إرجاع البروفايل مع بيانات الطالب
        public StudentProfileReadDto? Profile { get; set; }
    }
}