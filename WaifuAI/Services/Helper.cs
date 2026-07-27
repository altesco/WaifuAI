using System;

namespace WaifuAI.Services;

public static class Helper
{
    public static int GetAge(DateOnly birthday)
    {
        var today = DateTime.Today;
        var age = today.Year - birthday.Year;
        if (birthday.Month > today.Month ||
            (birthday.Month == today.Month && birthday.Day > today.Day))
            age--;
        return age;
    }
}