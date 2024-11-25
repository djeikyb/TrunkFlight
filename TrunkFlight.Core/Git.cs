using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace TrunkFlight.Core;

public class SimpleCommit(LibGit2Sharp.Commit from)
{
    public string ShaShort { get; } = from.Sha[..7];
    public string MessageShort { get; } = from.MessageShort;
}

public class Git(AppData appData, GitRepo gr)
{
    public void Clone()
    {
        var absolutePathToBareGitRepo = Path.Combine(appData.UserAppDataDir.FullName, gr.RepoPath);
        if (!Path.IsPathRooted(absolutePathToBareGitRepo)) return; // HRM error?
        if (!Path.IsPathFullyQualified(absolutePathToBareGitRepo)) return; // HRM error?

        if (!Directory.Exists(absolutePathToBareGitRepo))
        {
            var cloneOptions = new CloneOptions
            {
                IsBare = true,
                RecurseSubmodules = false
            };
            if (new Uri(gr.GitUrl).Scheme.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
            {
                cloneOptions.FetchOptions.CredentialsProvider = gr.Creds(); // OnCheckoutProgress =  // HRM oooh✨
            }

            Repository.Clone(gr.GitUrl, absolutePathToBareGitRepo, cloneOptions);
        }
    }

    public void Fetch()
    {
        var absolutePathToBareGitRepo = Path.Combine(appData.UserAppDataDir.FullName, gr.RepoPath);
        if (!Path.IsPathRooted(absolutePathToBareGitRepo)) return; // HRM error?
        if (!Path.IsPathFullyQualified(absolutePathToBareGitRepo)) return; // HRM error?
        using var repo = new Repository(absolutePathToBareGitRepo);
        foreach (var remote in repo.Network.Remotes)
        {
            var fetchOptions = new FetchOptions
            {
                // HRM oooooo OnCheckoutProgress ✨
                Prune = true
            };
            if (new Uri(gr.GitUrl).Scheme.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
            {
                fetchOptions.CredentialsProvider = gr.Creds();
            }

            Commands.Fetch(repo, remote.Name, [], fetchOptions, "TrunkFlight fetch");
        }
    }

    /// But only from the remote named "origin"
    public string[] RemoteBranchNames()
    {
        var absolutePathToBareGitRepo = Path.Combine(appData.UserAppDataDir.FullName, gr.RepoPath);
        if (!Path.IsPathRooted(absolutePathToBareGitRepo)) return []; // HRM error?
        if (!Path.IsPathFullyQualified(absolutePathToBareGitRepo)) return []; // HRM error?
        using var repo = new Repository(absolutePathToBareGitRepo);
        return repo.Branches
            .Where(x => x.IsRemote)
            .Where(x => x.RemoteName.Equals("origin", StringComparison.InvariantCultureIgnoreCase))
            .OrderByDescending(x => x.Tip.Committer.When)
            .Select(x => x.FriendlyName[7..])
            .Where(x => !"HEAD".Equals(x))
            .ToArray(); // cf LatestCommits()
    }

    public SimpleCommit[] LatestCommits(string originBranch)
    {
        var absolutePathToBareGitRepo = Path.Combine(appData.UserAppDataDir.FullName, gr.RepoPath);
        if (!Path.IsPathRooted(absolutePathToBareGitRepo)) return []; // HRM error?
        if (!Path.IsPathFullyQualified(absolutePathToBareGitRepo)) return []; // HRM error?
        using var repo = new Repository(absolutePathToBareGitRepo);
        return repo.Branches
            .Where(x => x.IsRemote)
            .Where(x => x.FriendlyName.Equals($"origin/{originBranch}"))
            .SelectMany(x => x.Commits.Take(10))
            .Select(x => new SimpleCommit(x))
            .ToArray(); // must be called, and must be called last

        // 1. Why is a projection needed
        //
        // The libgit2sharp.Commit type is lazy. A commit is a node in a
        // potentially massive directed acyclic graph. The lib chooses to wait
        // to populate a Commit object's properties until the first access.
        // This means each property on a Commit needs a reference to a
        // Repository object. Which, if you'll note above, is disposed at the
        // end of this function.
        //
        // Libgit2sharp's behaviour is probably ideal for a ui that probes thru
        // a lot of git history. But I just wanna see the last few commits,
        // given some ref; eg head~10 or ff2b567~10.
        //
        // 2. Why not return the IEnumerable? Why ToArray?
        //
        // This linq expression produces an IEnumerable with deferred
        // execution. The projection itself would not be executed in this
        // function. Instead, the adapter from Commit -> SimpleCommit runs
        // somewhere up the stack, long after the Repository object was
        // disposed down here.
    }

    public class WorktreeCommand(AppData appData, GitRepo gr)
    {
        public void Add(string absolutePathToNewWorkTree, string committish)
        {
            if (!Path.IsPathRooted(absolutePathToNewWorkTree)) return; // HRM error?
            if (!Path.IsPathFullyQualified(absolutePathToNewWorkTree)) return; // HRM error?

            var worktreeId = Ulid.NewUlid();
            var worktreeName = worktreeId.ToString();

            var absolutePathToBareGitRepo = Path.Combine(appData.UserAppDataDir.FullName, gr.RepoPath);
            if (!Path.IsPathRooted(absolutePathToBareGitRepo)) return; // HRM error?
            if (!Path.IsPathFullyQualified(absolutePathToBareGitRepo)) return; // HRM error?

            using var repo = new Repository(absolutePathToBareGitRepo);

            var commit = repo.Lookup<Commit>(committish);
            if (commit is null) throw new Exception($"Couldn't find commit matching: {committish}");

            var worktree = repo.Worktrees.Add(
                committishOrBranchSpec: "HEAD",
                name: $"{worktreeName}",
                path: $"{absolutePathToNewWorkTree}",
                isLocked: false
            );

            // worktree should be detached head right now
            // but, it's not.
            using (var wtRepo = worktree.WorktreeRepository)
            {
                Commands.Checkout(wtRepo, commit, new CheckoutOptions
                {
                    CheckoutModifiers = CheckoutModifiers.Force
                });
                wtRepo.Branches.Remove(worktreeName);
            }

            // libgit2sharp creates an extra branch that no one needs or wants
            var lesigh = repo.Branches[worktreeName];
            if (lesigh is not null) repo.Branches.Remove(lesigh);
        }

        public void Remove(string absolutePathToWorkTree)
        {
            // I have to implement this from scratch because
            // libgit2sharp forces you to keep track of a worktree name. The
            // git cli lets you think in terms of worktree location + ref,
            // which is what I want. And, I suppose is the fundamental reason
            // for this whole worktree command class in the first place.

            if (!Path.IsPathRooted(absolutePathToWorkTree)) return; // HRM error?
            if (!Path.IsPathFullyQualified(absolutePathToWorkTree)) return; // HRM error?

            var absolutePathToBareGitRepo = Path.Combine(appData.UserAppDataDir.FullName, gr.RepoPath);
            if (!Path.IsPathRooted(absolutePathToBareGitRepo)) return; // HRM error?
            if (!Path.IsPathFullyQualified(absolutePathToBareGitRepo)) return; // HRM error?

            var theDotGitWorktreeInfoGitDirs = Directory.EnumerateFiles(
                path: Path.Combine(absolutePathToBareGitRepo, "worktrees"),
                searchPattern: "gitdir",
                new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                    MatchType = MatchType.Simple,
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 2,
                    ReturnSpecialDirectories = false,
                }
            );

            var realpathWorktree = new DirectoryInfo(absolutePathToWorkTree).Realpath();

            foreach (var worktreeInfoGitDir in theDotGitWorktreeInfoGitDirs)
            {
                var gitDir = File.ReadAllText(worktreeInfoGitDir);
                var worktreeDir = Path.GetDirectoryName(gitDir);
                if (worktreeDir is null) continue;
                var realpathWorktreeDir = new DirectoryInfo(worktreeDir).Realpath();
                if (!realpathWorktreeDir.FullName.Equals(realpathWorktree.FullName)) continue;

                var worktreeInfoDir = Path.GetDirectoryName(worktreeInfoGitDir);
                if (worktreeInfoDir is null)
                    throw new Exception("It's a deep absolute path. How could there be a null parent dir?");
                try
                {
                    Directory.Delete(worktreeInfoDir, recursive: true);
                }
                catch (DirectoryNotFoundException)
                {
                    // weird but don't care.
                    // maybe the user/me is tampering with the app data folder?
                }

                // there's only one match
                break;
            }

            // The git database's knowledge of the worktree is gone.
            // Maybe it was already gone, maybe we just deleted it.
            // Either way, time to delete the worktree itself.
            realpathWorktree.Delete(recursive: true);
        }
    }

    public WorktreeCommand Worktree = new WorktreeCommand(appData, gr);
}
