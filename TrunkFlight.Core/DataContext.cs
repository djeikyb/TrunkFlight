using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TrunkFlight.Core;

public class Project
{
    public int ProjectId { get; init; }

    public required int GitRepoId { get; init; }
    public GitRepo? GitRepo { get; init; }

    [MaxLength(2048)] public required string Name { get; init; }
    [MaxLength(2048)] public required string Command { get; set; }
}

public class GitRepo
{
    public int GitRepoId { get; set; }

    /// relative to <see cref="AppData.UserAppDataDir"/>
    [MaxLength(1024)]
    public required string RepoPath { get; init; }

    [MaxLength(1024)] public required string GitUrl { get; init; }
    [MaxLength(256)] public string? Username { get; set; }
    [MaxLength(256)] public string? Password { get; set; }
}

public class DataContext(IConfiguration config) : DbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<GitRepo> GitRepos => Set<GitRepo>();

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options
            .UseSqlite(config.GetConnectionString("db"))
            .UseSnakeCaseNamingConvention();
}
