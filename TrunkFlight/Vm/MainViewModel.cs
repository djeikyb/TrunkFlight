using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using ObservableCollections;
using R3;
using Serilog;
using Serilog.Events;
using TrunkFlight.Core;

namespace TrunkFlight.Vm;

public class MainViewModel : IDisposable
{
    private DisposableBag _disposable;

    public void Dispose()
    {
        TearDown();
        _disposable.Dispose();
    }

    // HACK
    // 1. load first project
    // 2. load that project's git repo
    // 3. run fetch
    // 4. load that repo's first commit
    // 5.

    public MainViewModel()
    {
        var config = new ConfigurationBuilder().AddDefaultConfig().AddEnvironmentVariables().Build();
        var db = new DataContext(config);
        db.Database.EnsureCreated();
        var projects = new ProjectService(AppData.Default, db);
        var logger = Log.ForContext<MainViewModel>();

        Project = new BindableReactiveProperty<Project?>().AddTo(ref _disposable);

        ProcessOutput = new BindableReactiveProperty<string>();
        SandboxPath = new BindableReactiveProperty<string?>();

        var commits = new ObservableList<string>(["HEAD"]);
        GitCommitOptions = commits.ToNotifyCollectionChangedSlim();
        GitCommitSelected = new BindableReactiveProperty<string>("HEAD");

        ProjectLoadCommand = new ReactiveCommand();
        ProjectLoadCommand.SubscribeAwait((async (_, ct) =>
        {
            logger.Information("Command: " + nameof(ProjectLoadCommand));
            Project.Value = await projects.First(ct);
        }));

        ProjectUnloadCommand = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(ProjectUnloadCommand));
            Project.Value = null;
        });

        ProjectUpdateCommand = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(ProjectUpdateCommand));

            var p = Project.Value;
            if (p is null) return;
            if (p.GitRepo is null) return;

            var git = new Git(AppData.Default, p.GitRepo);
            git.Fetch();
            commits.Clear();
            var latestCommits = git.LatestCommits()
                .Select(x => x.ShaShort + " " + x.MessageShort);
            commits.AddRange(latestCommits);
        });

        SandboxCreateCommand = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(SandboxCreateCommand));

            var p = Project.Value;
            if (p is null) return;
            if (p.GitRepo is null) return;

            var git = new Git(AppData.Default, p.GitRepo);

            var tmpDir = Directory.CreateTempSubdirectory("merviche.trunkflight.");
            var tmpDirPath = tmpDir.FullName;
            // lol. lmao.
            // libgit2sharp requires an empty dir
            // but the csharp api to create a temp dir _path_ is private
            tmpDir.Delete(); // this is ridiculous

            git.Worktree.Add(tmpDirPath, GitCommitSelected.Value);
            SandboxPath.Value = tmpDirPath;
        });

        SandboxDestroyCommand = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(SandboxDestroyCommand));
            if (SandboxPath.Value is not { } path) return;

            var p = Project.Value;
            if (p is null) return;
            if (p.GitRepo is null) return;

            var git = new Git(AppData.Default, p.GitRepo);
            git.Worktree.Remove(path);
            SandboxPath.Value = null;
            ProcessOutput.Value = string.Empty;
        });

        SandboxRunAppCommand = new ReactiveCommand();
        SandboxRunAppCommand.SubscribeAwait(async (_, ct) =>
        {
            logger.Information("Command: " + nameof(SandboxRunAppCommand));

            if (Project.Value is not { } proj) return;
            if (SandboxPath.Value is not { } path) return;

            var firstSpace = proj.Command.IndexOf(' ');

            Process proc = new Process();
            proc.AddTo(ref _disposable);

            // set up the command
            proc.StartInfo.WorkingDirectory = path;
            proc.StartInfo.FileName = firstSpace > 0
                ? proj.Command[..firstSpace]
                : proj.Command;
            if (firstSpace > 0) proc.StartInfo.Arguments = proj.Command[firstSpace..];

            // get ready to capture stdout
            proc.StartInfo.RedirectStandardOutput = true;
            Observable.FromEvent<DataReceivedEventHandler, DataReceivedEventArgs>(
                    h => (sender, e) => h(e),
                    e => proc.OutputDataReceived += e,
                    e => proc.OutputDataReceived -= e)
                .Subscribe(args =>
                {
                    ProcessOutput.Value += args.Data + Environment.NewLine;
                });

            proc.Start();
            proc.BeginOutputReadLine();
            await proc.WaitForExitAsync(ct);
        }, AwaitOperation.Drop);

        InitRepo = new ReactiveCommand();

        TearDownCommand = new ReactiveCommand(_ =>
        {
            logger.Information("Command: " + nameof(TearDownCommand));
            TearDown();
        });

        View = App.LogsSink.Logs.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

        TintOpacity = new BindableReactiveProperty<decimal>(1m).AddTo(ref _disposable);
        MaterialOpacity = new BindableReactiveProperty<decimal>(1m).AddTo(ref _disposable);
    }

    private void TearDown()
    {
        if (Project.Value?.GitRepo?.RepoPath is { } repoPath)
        {
            var hack = Path.Combine(AppData.Default.UserAppDataDir.FullName, repoPath);
            try
            {
                Directory.Delete(hack, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // whatever man
            }
        }

        var myTempDirRoot = "/private/var/folders/9m/25nqg0sd5b51pm8dz78j31740000gn"; // TODO hack
        var tempDirs = Directory.EnumerateDirectories(myTempDirRoot, "merviche.trunkflight.*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MatchCasing = MatchCasing.CaseSensitive,
            MatchType = MatchType.Simple,
            MaxRecursionDepth = 2,
            ReturnSpecialDirectories = false,
            IgnoreInaccessible = true,
        });

        foreach (var dir in tempDirs)
        {
            if (!Path.IsPathRooted(dir)) continue;
            if (!Path.IsPathFullyQualified(dir)) continue;
            var dirname = Path.GetFileName(dir);
            if (!dirname.StartsWith("merviche.trunkflight.")) continue;
            Directory.Delete(dir, true);
        }
    }

    public BindableReactiveProperty<string?> SandboxPath { get; }
    public BindableReactiveProperty<string> ProcessOutput { get; }
    public BindableReactiveProperty<string> GitCommitSelected { get; }
    public INotifyCollectionChangedSynchronizedViewList<string> GitCommitOptions { get; }

    public ReactiveCommand<Unit> ProjectLoadCommand { get; }
    public ReactiveCommand<Unit> ProjectUnloadCommand { get; }
    public ReactiveCommand<Unit> ProjectUpdateCommand { get; }


    public ReactiveCommand<Unit> SandboxCreateCommand { get; }
    public ReactiveCommand<Unit> SandboxDestroyCommand { get; }
    public ReactiveCommand<Unit> SandboxRunAppCommand { get; }

    public ReactiveCommand<Unit> InitRepo { get; }
    public ReactiveCommand<Unit> TearDownCommand { get; }


    public BindableReactiveProperty<Project?> Project { get; }

    public BindableReactiveProperty<decimal> TintOpacity { get; }
    public BindableReactiveProperty<decimal> MaterialOpacity { get; }


    public INotifyCollectionChangedSynchronizedViewList<LogEvent> View { get; }
}
