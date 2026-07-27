using System.Linq.Expressions;
using TrainingCenter.Entities;//using entites of database

namespace TrainingCenter.Interfaces
{
    public interface IUnitOfWork : IDisposable//end and clear after finishing
    {
        IGenericRepository<Student> Students { get; }
        IGenericRepository<Course> Courses { get; }
        IGenericRepository<Instructor> Instructors { get; }
        IGenericRepository<Enrollment> Enrollments { get; }
        IGenericRepository<StudentProfile> StudentProfiles { get; }
        IGenericRepository<User> Users { get; }

        Task<int> CompleteAsync();//to save memory data in database
    }
}