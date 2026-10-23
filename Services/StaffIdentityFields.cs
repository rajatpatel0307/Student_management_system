using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Student_Management_System.Services;

public static class StaffIdentityFields
{
    public static string NormalizeMobile(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new StaffValidationException("Phone number is required to create a staff account.");
        var number = Regex.Replace(phone.Trim(), @"[\s().-]", "");
        if (number.StartsWith("00")) number = "+" + number[2..];
        if (Regex.IsMatch(number, @"^[0-9]{10}$")) number = "+91" + number;
        else if (Regex.IsMatch(number, @"^91[0-9]{10}$")) number = "+" + number;
        if (!Regex.IsMatch(number, @"^\+[1-9][0-9]{7,14}$"))
            throw new StaffValidationException("Enter a 10-digit mobile number or an international number with its country code.");
        if (number.StartsWith("+91") && !Regex.IsMatch(number, @"^\+91[6-9][0-9]{9}$"))
            throw new StaffValidationException("Enter a valid Indian mobile number: 10 digits starting with 6, 7, 8 or 9.");
        return number;
    }

    public static string SaveError(DbUpdateException error)
    {
        if (error.InnerException is SqlException sql)
            return sql.Number switch
            {
                2601 or 2627 => "An account with these details already exists. Check the email and mobile number.",
                51002 => "This mobile number is already registered. Please use a different mobile number.",
                51003 => "Enter a valid mobile number before saving the staff account.",
                51001 => "The generated user ID could not be saved. Please try again or contact your administrator.",
                _ => "Staff could not be saved because of a database error. Please try again or contact your administrator."
            };
        return "Staff could not be saved. Please try again or contact your administrator.";
    }
}
