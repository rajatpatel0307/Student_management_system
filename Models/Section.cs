using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Student_Management_System.Models;

namespace Student_Management_System.Models
{
    public class Section
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string Name { get; set; } = ""; // A, B, C

        // 🔗 Belongs to Class
        public int ClassId { get; set; }

        [ForeignKey("ClassId")]
        public virtual Class Class { get; set; } = null!;
        public bool IsArchived { get; set; }
        public DateTime? ArchivedUtc { get; set; }
        [MaxLength(450)] public string? ArchivedByUserId { get; set; }
        [MaxLength(500)] public string? ArchiveReason { get; set; }
        public DateTime? RestoredUtc { get; set; }
        [MaxLength(450)] public string? RestoredByUserId { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; } = [];
    }
}
