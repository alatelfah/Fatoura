namespace Fatoura.Api.Data.Entities;

public sealed class Client : IAudited
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Trn { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Supplier : IAudited
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Trn { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Copy of a party's details taken when a document is saved, so reprints never change.</summary>
public sealed class PartySnapshot
{
    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Trn { get; set; } = string.Empty;

    public static PartySnapshot From(Client c) =>
        new() { Name = c.Name, Address = c.Address, Phone = c.Phone, Email = c.Email, Trn = c.Trn };

    public static PartySnapshot From(Supplier s) =>
        new() { Name = s.Name, Address = s.Address, Phone = s.Phone, Email = s.Email, Trn = s.Trn };

    public PartySnapshot Clone() =>
        new() { Name = Name, Address = Address, Phone = Phone, Email = Email, Trn = Trn };
}

/// <summary>Copy of the issuing company's details at issue time (tax invoice requirement).</summary>
public sealed class CompanySnapshot
{
    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Website { get; set; } = string.Empty;

    public string Trn { get; set; } = string.Empty;

    public static CompanySnapshot From(CompanySettings s) => new()
    {
        Name = s.Name, Address = s.Address, Phone = s.Phone, Email = s.Email, Website = s.Website, Trn = s.Trn,
    };

    public CompanySnapshot Clone() => new()
    {
        Name = Name, Address = Address, Phone = Phone, Email = Email, Website = Website, Trn = Trn,
    };
}
