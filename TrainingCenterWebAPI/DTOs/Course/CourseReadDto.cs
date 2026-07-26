namespace TrainingCenter.DTOs.Course
{
    public class CourseReadDto
    {
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string Level { get; set; } = string.Empty;
        public int DurationHours { get; set; }
        public string Status { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
    }
}