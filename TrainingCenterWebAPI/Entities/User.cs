namespace TrainingCenter.Entities
{
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Student";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // 🔐 Security & Refresh Token Fields
        public string? RefreshTokenHash { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
        public DateTime? RefreshTokenRevokedAt { get; set; }

        // Foreign Keys & Navigation Properties
        public int? InstructorId { get; set; }
        public Instructor? Instructor { get; set; }

        public int? StudentId { get; set; }
        public Student? Student { get; set; }
    }
}