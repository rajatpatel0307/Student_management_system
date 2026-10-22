namespace Student_Management_System.Services;

public static class GradeCalculator
{
    public static string FromScore(decimal score, decimal maximumMarks = 100)
    {
        if (maximumMarks <= 0 || score < 0 || score > maximumMarks)
            throw new ArgumentOutOfRangeException(nameof(score));
        var percentage = score * 100m / maximumMarks;
        return percentage switch
        {
            >= 90 => "A+",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            >= 50 => "D",
            _ => "F"
        };
    }
}
