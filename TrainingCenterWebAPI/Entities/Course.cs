using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TrainingCenter.Entities
{
    public partial class Course
    {
        public int CourseId { get; set; }

        public string Title { get; set; } = null!;

        public string Code { get; set; } = null!;

        public string? Description { get; set; }

        public decimal Price { get; set; }

        public string Level { get; set; } = null!;

        public int DurationHours { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }

        public string Status { get; set; } = null!;

        public int InstructorId { get; set; }

        public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

        public virtual Instructor Instructor { get; set; } = null!;
    }
}
