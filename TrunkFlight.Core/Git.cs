using System;
using System.IO;
using LibGit2Sharp;

namespace TrunkFlight.Core;

public class Git(AppData appData, GitRepo gr)
{
    public void Fetch()
    {
        var absolutePathToBareGitRepo = Path.Combine(appData.UserAppDataDir.FullName, gr.RepoPath);
        if (!Path.IsPathRooted(absolutePathToBareGitRepo)) return; // HRM error?
        if (!Path.IsPathFullyQualified(absolutePathToBareGitRepo)) return; // HRM error?
        using var repo = new Repository(absolutePathToBareGitRepo);
        foreach (var remote in repo.Network.Remotes)
        {
            Commands.Fetch(repo, remote.Name, [], new FetchOptions
            {
                // HRM oooooo OnCheckoutProgress ✨
                Prune = true,
                CredentialsProvider = gr.Creds()
            }, "TrunkFlight fetch");
        }
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
