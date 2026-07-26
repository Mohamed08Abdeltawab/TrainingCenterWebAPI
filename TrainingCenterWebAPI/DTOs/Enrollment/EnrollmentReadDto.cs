namespace TrainingCenter.DTOs.Enrollment
{
    public class EnrollmentReadDto
    {
        public int EnrollmentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public decimal ProgressPercent { get; set; }
        public decimal? FinalGrade { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}