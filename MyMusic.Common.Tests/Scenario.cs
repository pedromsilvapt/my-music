using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using NSubstitute;

namespace MyMusic.Common.Tests;

public class Scenario
{
    public IFileSystem FileSystem { get; set; }
    
    public MusicDbContext DbContext { get; set; }
    
    public User AdminUser { get; set; }

    public Scenario()
    {
        FileSystem = CreateFileSystem();
        DbContext = CreateDbContext();
        AdminUser = CreateUser("Administrator", "admin");
    }
    
    #region Seeding Data

    public User CreateUser(string name, string username)
    {
        var user = new User
        {
            Name = name,
            Username = username,
        };
        
        DbContext.Add(user);
        DbContext.SaveChanges();
        
        return user;
    }
    
    #endregion Seeding Data
    
    #region Static Methods
    
    public static MusicDbContext CreateDbContext()
    {
        var keepAliveConnection = new SqliteConnection("DataSource=:memory:");
        keepAliveConnection.Open();
        
        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseSqlite(keepAliveConnection)
            .LogTo(Console.WriteLine)
            .Options;
        var context = new MusicDbContext(options);
        context.Database.EnsureCreated();
        context.Database.Migrate();
        context.SaveChanges();
        return context;
    }

    public static IFileSystem CreateFileSystem()
    {
        return new MockFileSystem();
    }

    public MusicService CreateMusicService()
    {
        return new MusicService(FileSystem, Options.Create(new Config
        {
            MusicRepositoryPath = "/data",
        }), Substitute.For<ILogger<MusicService>>());
    }

    #endregion Static Methods

}