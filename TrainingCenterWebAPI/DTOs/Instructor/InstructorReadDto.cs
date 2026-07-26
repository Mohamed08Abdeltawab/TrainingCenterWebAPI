namespace TrainingCenter.DTOs.Instructor
{
    public class InstructorReadDto
    {
        public int InstructorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateOnly HireDate { get; set; }
        public decimal Salary { get; set; }
        public bool IsActive { get; set; }
        public string? ManagerName { get; set; }
    }
}