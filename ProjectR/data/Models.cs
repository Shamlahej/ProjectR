using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProjectR.Data;

// 1) Brugere
public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Username { get; set; } = "";

    // super simpelt login (til skoleprojekt ok)
    // I rigtig verden: hash + salt
    [Required]
    public string PasswordHash { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SortingEvent> SortingEvents { get; set; } = new();
}

// 2) Typer af komponenter (M3 screw, pen, osv.)
public class ComponentType
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";  // fx "M3 screw"

    public string? Description { get; set; }
}

// 3) Et “sorterings-event” hver gang robotten sorterer en ting
public class SortingEvent
{
    [Key]
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // hvem gjorde det
    public int? UserId { get; set; }
    public User? User { get; set; }

    // hvad blev sorteret
    public int ComponentTypeId { get; set; }
    public ComponentType? ComponentType { get; set; }

    // blev den godkendt eller sendt til fejl-boks?
    public bool IsOk { get; set; }
}

// 4) Counter (kun én række) - her ligger "hvor mange items sorteret"
public class Counter
{
    [Key]
    public int Id { get; set; } = 1;   // vi bruger altid ID=1

    public long ItemsSortedTotal { get; set; }
    public long ItemsOkTotal { get; set; }
    public long ItemsRejectedTotal { get; set; }
}