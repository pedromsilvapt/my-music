using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class User
{
    public long Id { get; set; }
    
    [MaxLength(256)]
    public required string Username { get; set; }
    
    [MaxLength(256)]
    public required string Name { get; set; } 
}