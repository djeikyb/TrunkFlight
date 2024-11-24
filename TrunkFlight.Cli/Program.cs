using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TrunkFlight.Core;

namespace TrunkFlight.Cli;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        var config = new ConfigurationBuilder()
            .AddDefaultConfig()
            .AddEnvironmentVariables()
            .Build();
        Console.WriteLine(config.GetConnectionString("db"));
        var db = new DataContext(config);

        var result = db.Database.EnsureDeleted();
        Console.WriteLine("deleted:\n" + result);

        var created = db.Database.EnsureCreated();
        Console.WriteLine("created:\n" + created);
        Console.WriteLine(db.Database.GetConnectionString());


        var gr = db.GitRepos.Add(new GitRepo
        {
            GitUrl = "changeme",
            Username = "changeme",
            Password = "changeme",
            RepoPath = AppData.Default.GenerateRepoPath("changeme"),
        }).Entity;
        db.SaveChanges();

        var proj = db.Projects.Add(new Project
        {
            Name = "changeme",
            GitRepoId = gr.GitRepoId,
            Command = "changeme",
        }).Entity;
        db.SaveChanges();

        var service = new Git(AppData.Default, gr);
        service.Fetch();
    }
}
