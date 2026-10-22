namespace Student_Management_System.Services;

public static class FinancialAllocationCalculator
{
    public static decimal Refundable(
        decimal paymentAmount,
        IEnumerable<decimal> completedRefundAmounts)
    {
        var returned = completedRefundAmounts.Sum(x => Math.Abs(x));
        return decimal.Round(
            Math.Max(0, paymentAmount - returned),
            2,
            MidpointRounding.AwayFromZero);
    }

    public static IReadOnlyList<decimal> AllocateNetPayment(
        decimal paymentAmount,
        IEnumerable<decimal> completedRefundAmounts,
        IReadOnlyList<decimal> allocationAmounts)
    {
        var remaining = Refundable(paymentAmount, completedRefundAmounts);
        var result = new decimal[allocationAmounts.Count];
        for (var index = 0; index < allocationAmounts.Count; index++)
        {
            var allocation = decimal.Round(allocationAmounts[index], 2);
            result[index] = Math.Min(allocation, remaining);
            remaining -= result[index];
            if (remaining <= 0) break;
        }
        return result;
    }
}
