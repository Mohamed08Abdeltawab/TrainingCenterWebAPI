using AutoMapper;
using TrainingCenter.DTOs.Course;
using TrainingCenter.DTOs.Enrollment;
using TrainingCenter.DTOs.Instructor;
using TrainingCenter.DTOs.Student;
using TrainingCenter.DTOs.StudentProfile;
using TrainingCenter.Entities;

namespace TrainingCenter.Profiles
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Student
            CreateMap<Student, StudentReadDto>()//source , destination
                //FullName in destination i need to fill it with src (second parametar)
                .ForMember(dest => dest.FullName,opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
                .ForMember(dest => dest.Profile,opt => opt.MapFrom(src => src.StudentProfile));
            CreateMap<StudentCreateDto, Student>();

            // Course
            CreateMap<Course, CourseReadDto>()
                .ForMember(dest => dest.InstructorName, opt => opt.MapFrom(src => src.Instructor != null ? $"{src.Instructor.FirstName} {src.Instructor.LastName}" : "N/A"));
            CreateMap<CourseCreateDto, Course>();

            // Instructor
            CreateMap<Instructor, InstructorReadDto>()
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
                .ForMember(dest => dest.ManagerName, opt => opt.MapFrom(src => src.Manager != null ? $"{src.Manager.FirstName} {src.Manager.LastName}" : "None"));
            CreateMap<InstructorCreateDto, Instructor>();

            // Enrollment
            CreateMap<Enrollment, EnrollmentReadDto>()
                .ForMember(dest => dest.StudentName, opt => opt.MapFrom(src => src.Student != null ? $"{src.Student.FirstName} {src.Student.LastName}" : "N/A"))
                .ForMember(dest => dest.CourseTitle, opt => opt.MapFrom(src => src.Course != null ? src.Course.Title : "N/A"));
            CreateMap<EnrollmentCreateDto, Enrollment>();


            // StudentProfile Mappings
            CreateMap<StudentProfile, StudentProfileReadDto>();
            CreateMap<StudentProfileCreateDto, StudentProfile>();

            
        }
    }
}