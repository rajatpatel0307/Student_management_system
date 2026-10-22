using Microsoft.AspNetCore.Mvc.Rendering;

namespace Student_Management_System.Models
{
    public class SubjectViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Required]
        public int? ClassId { get; set; }
        public int? TeacherId { get; set; }
        public int? DepartmentId { get; set; }
        public int? SemesterId { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public IEnumerable<SelectListItem> Classes { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> Teachers { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> Departments { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> Semesters { get; set; } = Enumerable.Empty<SelectListItem>();
    }
}
