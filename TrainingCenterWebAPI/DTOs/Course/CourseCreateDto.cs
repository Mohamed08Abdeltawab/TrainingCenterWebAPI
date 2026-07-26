using System.ComponentModel.DataAnnotations;

namespace TrainingCenter.DTOs.Course
{
    public class CourseCreateDto
    {
        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Code {  get; set;} = string.Empty;

        [StringLength(30)]
        public string? Description { get; set;}


        [Range(0,99999.99)]
        public decimal Price { get; set;}

        [Required]
        public string Level { get; set;} = string.Empty;

        [Range(1,1000)]
        public int DurationHours { get; set;}

        [Range(1,int.MaxValue)]
        public int InstructorId { get; set;}


    }
}