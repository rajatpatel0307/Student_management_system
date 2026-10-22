using System;
using System.ComponentModel.DataAnnotations;


namespace Student_Management_System.Models
{
    public class Notice
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = "";

        public string Message { get; set; } = "";

        public DateTime CreatedDate { get; set; }

        // 👥 Target audience
        public string TargetRole { get; set; } = "";
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }
    }
}
