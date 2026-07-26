using TrainingCenter.Data;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;

namespace TrainingCenter.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        public IGenericRepository<Student> Students { get; private set; }
        public IGenericRepository<Course> Courses { get; private set; }
        public IGenericRepository<Instructor> Instructors { get; private set; }
        public IGenericRepository<Enrollment> Enrollments { get; private set; }
        public IGenericRepository<StudentProfile> StudentProfiles { get; private set; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Students = new GenericRepository<Student>(_context);
            Courses = new GenericRepository<Course>(_context);
            Instructors = new GenericRepository<Instructor>(_context);
            Enrollments = new GenericRepository<Enrollment>(_context);
            StudentProfiles = new GenericRepository<StudentProfile>(_context);
        }

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}