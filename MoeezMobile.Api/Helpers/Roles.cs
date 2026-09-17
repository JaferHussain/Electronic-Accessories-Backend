namespace MoeezMobile.Api.Helpers;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Salesman = "Salesman";
}

public static class Policies
{
    /// <summary>Master data, voiding invoices, reports, settings.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Day-to-day selling - both roles.</summary>
    public const string AnyStaff = "AnyStaff";
}
