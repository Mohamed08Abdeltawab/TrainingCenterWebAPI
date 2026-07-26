using System;
using System.Collections.Generic;
using System.Text;

namespace TrainingCenter.Entities
{
    public partial class Instructor
    {
        public int InstructorId { get; set; }

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        public string Email { get; set; } = null!;

        public DateOnly HireDate { get; set; }

        public decimal Salary { get; set; }

        public int? ManagerId { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<Course> Courses { get; set; } = new List<Course>();

        public virtual ICollection<Instructor> InverseManager { get; set; } = new List<Instructor>();

        public virtual Instructor? Manager { get; set; }
    }
}
