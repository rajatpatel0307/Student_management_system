using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

public enum ExamLifecycle
{
    Draft = 1,
    OpenForEntry,
    Locked,
    Published,
    Archived
}

public class Exam
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = ""; // Mid, Final

    public DateTime ExamDate { get; set; }
    [MaxLength(50)] public string ExamType { get; set; } = "Internal";
    public ExamLifecycle Status { get; set; } = ExamLifecycle.Draft;
    [Range(0.01, 1000000)] public decimal MaximumMarks { get; set; } = 100;
    [Range(0, 1000000)] public decimal PassMarks { get; set; } = 40;
    [Range(0, 100)] public decimal WeightPercent { get; set; } = 100;
    public DateTime? EntryOpensUtc { get; set; }
    public DateTime? EntryClosesUtc { get; set; }
    public DateTime? PublicationScheduledUtc { get; set; }
    public DateTime? PublishedUtc { get; set; }
    [MaxLength(450)] public string? PublishedByUserId { get; set; }
    public bool IsSupplementary { get; set; }
    [MaxLength(40)] public string ModerationStatus { get; set; } = "Pending";

    // Retained for a data-compatible transition. New code uses Status.
    [Obsolete("Use Status.")]
    public bool IsLocked { get; set; }
    [Obsolete("Use Status.")]
    public bool IsApproved { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];

    // Add this navigation property to fix CS1061
    public virtual ICollection<Mark> Marks { get; set; } = [];
}
