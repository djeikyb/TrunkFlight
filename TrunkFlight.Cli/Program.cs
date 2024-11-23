using Microsoft.Extensions.Configuration;
using TrunkFlight.Core;

namespace TrunkFlight.Cli;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        var config = new ConfigurationBuilder()
            .AddDefaultConfig()
            .AddEnvironmentVariables()
            .Build();
        Console.WriteLine(config.GetConnectionString("db"));
        var db = new Db(config);

        var result = db.EnsureDeleted();
        Console.WriteLine("deleted:\n" + result);

        var created = db.EnsureCreated();
        Console.WriteLine("created:\n" + created);
        Console.WriteLine(db.GetConnectionString());


        var gr = new GitRepo
        {
            GitUrl = "changeme",
            Username = "changeme",
            Password = "changeme",
            RepoPath = AppData.Default.GenerateRepoPath("changeme"),
        };
        db.Save(gr);

        var proj = new Project
        {
            Name = "changeme",
            GitRepoId = gr.GitRepoId,
            Command = "changeme",
        };
        db.Save(proj);

        var git = new Git(AppData.Default, gr);
        git.Fetch();
    }
}
