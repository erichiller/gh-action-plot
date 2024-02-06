using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PlotGitHubAction;

[ SuppressMessage( "ReSharper", "NotAccessedPositionalProperty.Global" ) ]
public record GitRepoInfo(
    string Name,
    string GitHubCommitUrl,
    string RootDirPath,
    string CommitSha,
    string Branch,
    string GitHubBranchUrl
) {
    [ JsonIgnore ]
    public DirectoryInfo RootDir => new DirectoryInfo( RootDirPath );

    // , bool useBranch = false 
    public static GitRepoInfo CreateFromGitDir( DirectoryInfo gitRoot ) {
        if ( gitRoot.Name == @".git" ) {
            gitRoot = gitRoot.Parent!;
        }
        var match = Regex.Match(
            System.IO.File.ReadAllText( System.IO.Path.Combine( gitRoot.FullName, ".git", "FETCH_HEAD" ) ),
            @"(?<CommitSha>[0-9a-f]+).*github.com[/:](?<Name>.+$)"
        );
        string repoName  = match.Groups[ "Name" ].Value.Trim();
        string commitSha = match.Groups[ "CommitSha" ].Value;
        match = Regex.Match(
            System.IO.File.ReadAllText( System.IO.Path.Combine( gitRoot.FullName, ".git", "HEAD" ) ),
            @"^ref: refs/heads/(?<Branch>.*)$"
        );
        string branch = match.Groups[ "Branch" ].Value.Trim();
        string gitHubCommitUrlBase =
            @"https://github.com/"
            + repoName
            + "/blob/"
            + commitSha
            + "/";
        string gitHubBranchUrlBase =
            @"https://github.com/"
            + repoName
            + "/tree/"
            + branch
            + "/";
        return new GitRepoInfo(
            Name: repoName,
            GitHubCommitUrl: gitHubCommitUrlBase,
            RootDirPath: gitRoot.FullName,
            CommitSha: commitSha,
            Branch: branch,
            GitHubBranchUrl: gitHubBranchUrlBase
        );
    }
}