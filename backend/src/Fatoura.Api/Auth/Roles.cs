namespace Fatoura.Api.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Cashier = "Cashier";

    public static readonly string[] All = [Admin, Cashier];
}

public static class Policies
{
    /// <summary>Admin only: settings, users, purchases, suppliers, P&amp;L and VAT reports, invoice edit/void.</summary>
    public const string Admin = "Admin";

    /// <summary>Any active staff member (Admin or Cashier).</summary>
    public const string Staff = "Staff";
}

/// <summary>JWT claim names (inbound claim mapping is disabled, so these are read as-is).</summary>
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}
