namespace TrainingCenter.Entities
{
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Student";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        // Foreign Keys & Navigation Properties
        public int? InstructorId { get; set; }
        public Instructor? Instructor { get; set; }

        public int? StudentId { get; set; }
        public Student? Student { get; set; }
    }
}