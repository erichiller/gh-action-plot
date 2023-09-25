using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PlotGitHubAction;

/*
 *
 */

public record ActionConfig(
    string  OutputDir,
    string? SourceScanDir,
    string  BuildLogFilePattern,
    /* lang=regex */
    string  LineCountFilePattern,
    string? CoverageHistoryDir,
    string? PlotDefinitionsDir,
    string? TestResultsDir
) {
    public string PlotOutputDir             => System.IO.Path.Combine( OutputDir, "charts" );
    public string ToDoOutputDir             => OutputDir;
    public string TestFailureOutputDir      => System.IO.Path.Combine( OutputDir, "test_failures" );
    public string BuildLogHistoryOutputDir  => MetaDataOutputDir;
    public string LineCountHistoryOutputDir => MetaDataOutputDir;
    public string MetaDataOutputDir         => System.IO.Path.Combine( OutputDir, "metadata" );
    public string? CoverageSummaryDir => CoverageHistoryDir is { }
        ? System.IO.Directory.GetParent( CoverageHistoryDir )?.Parent?.FullName
        : null;
    // format: erichiller/gh-action-plot
    public string Repository { get; init; } = System.Environment.GetEnvironmentVariable( "GITHUB_REPOSITORY" ) ?? String.Empty;
    public string CommitHash { get; init; } = System.Environment.GetEnvironmentVariable( "GITHUB_SHA" )        ?? String.Empty;

    public static readonly string NOW_STRING = DateTimeOffset.Now.ToString( "o" );
    public static readonly string VERSION    = $"{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version}";
    public static readonly string NAME       = $"{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";

    public bool IsTodoScanEnabled => this is {
                                                 SourceScanDir: { },
                                                 OutputDir    : { },
                                                 CommitHash   : { }
                                             };
    public bool IsCoverageHistoryEnabled => this.CoverageHistoryDir is { } && System.IO.Path.Exists( this.CoverageHistoryDir );

    public static ActionConfig CreateFromEnvironment( ) {
        var config = new ActionConfig(
            OutputDir: System.Environment.GetEnvironmentVariable( "INPUT_OUTPUT_DIR" ) is { Length: > 0 } outputDir
                ? outputDir
                : System.Environment.CurrentDirectory,
            SourceScanDir: System.Environment.GetEnvironmentVariable( "INPUT_SOURCE_SCAN_DIR" ) is { Length: > 0 } sourceScanDir
                ? sourceScanDir
                : System.Environment.CurrentDirectory,
            BuildLogFilePattern: "*-build.log",
            LineCountFilePattern: System.Environment.GetEnvironmentVariable( "INPUT_LINE_COUNT_FILE_PATTERN" ) is { Length: > 0 } lineCountFilePattern
                ? lineCountFilePattern
                : @"(?<!\.(verified|generated))\.(axaml|cs|ps1)$",
            CoverageHistoryDir: System.Environment.GetEnvironmentVariable( "INPUT_COVERAGE_HISTORY_DIR" ) is { Length: > 0 } coverageHistoryDir
                ? coverageHistoryDir
                : null,
            TestResultsDir: System.Environment.GetEnvironmentVariable( "INPUT_TEST_RESULTS_DIR" ) is { Length: > 0 } testResultsDir
                ? testResultsDir
                : null,
            PlotDefinitionsDir: System.Environment.GetEnvironmentVariable( "INPUT_PLOT_DEFINITIONS_DIR" ) is { Length: > 0 } plotDefinitionsDir
                ? plotDefinitionsDir
                : null
        );
        createDirectoryIfNotExists( config.OutputDir );
        createDirectoryIfNotExists( config.MetaDataOutputDir );
        createDirectoryIfNotExists( config.TestFailureOutputDir );
        createDirectoryIfNotExists( config.PlotOutputDir );
        if ( config.SourceScanDir is { } ) {
            config._gitRepoRoots = scanRepoRoots( config.SourceScanDir );
            config._csProjects   = config.scanForCsProjects( config.SourceScanDir );
        }
        return config;
    }

    private static void createDirectoryIfNotExists( string directoryPath ) {
        if ( !System.IO.Path.Exists( directoryPath ) ) {
            Log.Debug( $"Directory '{directoryPath}' does not exist, creating..." );
            System.IO.Directory.CreateDirectory( directoryPath );
        }
    }

    public string? GetGitHubSourceLink(
        string filePath,
        int?   lineStart    = null,
        bool   linkToBranch = false
    ) {
        if ( getUrlWithPath(
                filePath: filePath,
                linkToBranch: linkToBranch
            ) is not { } url ) {
            return null;
        }
        string append = lineStart is { } line
            ? $"#L{line}"
            : String.Empty;
        Log.Debug( $"[{nameof(GetGitHubSourceLink)}] filePath='{filePath}', lineStart='{lineStart}', linkToBranch='{linkToBranch}', url='{url}', append='{append}', returning='{url + append}'" );
        return url + append;
    }

    /// <param name="filePath"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="linkToBranch">
    /// Whether the link should use the commit hash (<c>false</c>) or the branch name (<c>true</c>).
    /// </param>
    // example: https://github.com/erichiller/gh-action-plot/blob/1588c4d1617e89e56335c9d5c533c8baa4fc918d/TodoScanner.cs#L161-L170
    // example: https://github.com/beto-rodriguez/LiveCharts2/blob/36ff845586da0bb85e75f5efafc8e3aa1a142820/src/skiasharp/LiveChartsCore.SkiaSharp/Drawing/Geometries/ColoredRectangleGeometry.cs#L14C3-L45C36
    public string? GetGitHubSourceLink(
        string        filePath,
        CharPosition? start        = null,
        CharPosition? end          = null,
        bool          linkToBranch = false
    ) {
        if ( getUrlWithPath(
                filePath: filePath,
                linkToBranch: linkToBranch
            ) is not { } url ) {
            return null;
        }
        return url
               + ( start is { } || end is { }
                   ? "#"
                     + ( start is { Line: { } line, Column: { } col }
                         ? $"L{line}C{col}"
                         : String.Empty
                     )
                     + ( ( start, end ) is ({ }, { }) && start?.Line != end?.Line
                         ? "-"
                         : String.Empty
                     )
                     + ( end is { Line: { } endLine, Column: { } endCol } && start?.Line != endLine
                         ? $"L{endLine}C{endCol}"
                         : String.Empty
                     )
                   : String.Empty );
    }

    private string? getUrlWithPath(
        string filePath,
        bool   linkToBranch
    ) {
        string originalFilePath = filePath;
        filePath = Path.GetFullPath( filePath );
        var repo = this.getRepoForFile( filePath );
        if ( repo is not { } ) {
            Log.Warn( $"Unable to find git repo for {filePath} (originally: {originalFilePath})" );
            return null;
        }
        string baseUrl = linkToBranch
            ? repo.GitHubBranchUrl
            : repo.GitHubCommitUrl;

        Log.Debug( $"[{nameof(getUrlWithPath)}] filePath='{filePath}', originalFilePath='{originalFilePath}', baseUrl='{baseUrl}', repo={repo}" );
        return baseUrl.TrimEnd( '/' )
               + filePath[ repo.RootDir.FullName.Length.. ];
    }

    public string GetFormattedSourcePosition( string filePath, CharPosition? start = null, CharPosition? end = null ) {
        return Path.GetFileName( filePath )
               + ( start is { Line: { } startLine, Column: { } startColumn } ? $" {startLine}:{startColumn}" : String.Empty )
               + ( ( start, end ) is ({ }, { })                        && start?.Line != end?.Line ? "-" : String.Empty )
               + ( end is { Line: { } endLine, Column: { } endColumn } && endLine     != start?.Line ? $"{endLine}:{endColumn}" : String.Empty )
            ;
    }

    public string GetFormattedGitHubSourceLink( string filePath, CharPosition? start = null, CharPosition? end = null, bool linkToBranch = false ) {
        // 
        return "["
               + GetFormattedSourcePosition( filePath, start, end )
               + "]("
               + GetGitHubSourceLink( filePath, start, end, linkToBranch )
               + ")"
            ;
    }

    public string GetMarkdownChartLink( string fileName ) {
        //
        string relPath = System.IO.Path.GetRelativePath(
            this.OutputDir,
            PlotGen.GetChartFilePath( this.PlotOutputDir, fileName )
        );
        if ( !relPath.EndsWith( ".png" ) ) {
            relPath += ".png";
        }
        return $"![{fileName}]({relPath})";
    }

    /*
     *
     */

    // map of project file names (without .csproj) to CsProjInfo
    public ImmutableList<CsProjInfo> GetCsProjectsCopy( ) => _csProjects.Values.Select( c => new CsProjInfo( c ) ).ToImmutableList();

    private Dictionary<string, CsProjInfo> _csProjects = new ();

    // use when the project path references in a file is no longer valid because the source has moved.
    // eg. when in the GitHub Action docker container
    public CsProjInfo GetProject( string fileNameOrPath ) {
        return new CsProjInfo( _csProjects.Values.Single( c => c.ProjectName == Path.GetFileNameWithoutExtension( fileNameOrPath ) ) );
    }

    private Dictionary<string, CsProjInfo> scanForCsProjects( string scanDir ) {
        Dictionary<string, CsProjInfo> projects = new ();
        Log.Info( $"Scanning for *.csproj in: {scanDir}" );
        foreach ( var filePath in System.IO.Directory.EnumerateFiles( scanDir, "*.csproj", System.IO.SearchOption.AllDirectories ) ) {
            if ( getRepoForFile( filePath ) is { } gitRepo ) {
                string repoRelativePath = Path.GetRelativePath( gitRepo.RootDir.FullName, filePath );
                Log.Info( $"  csproj: {filePath}\n    => {repoRelativePath}" );
                var proj = new CsProjInfo( filePath, gitRepo );
                projects.Add( proj.ProjectName, proj );
            } else {
                throw new RepoNotFoundException();
            }
        }
        return projects;
    }

    /*
     *
     */
    List<GitRepoInfo> _gitRepoRoots = new ();

    private static List<GitRepoInfo> scanRepoRoots( string directoryRoot ) {
        List<GitRepoInfo> gitRepoRoots = new ();
        if ( !Directory.Exists( directoryRoot ) ) {
            if ( File.Exists( directoryRoot ) && new FileInfo( directoryRoot ).Directory is { FullName: { } fileDirectoryFullName } ) {
                directoryRoot = fileDirectoryFullName;
            } else {
                Log.Warn( $"DirectoryRoot '{directoryRoot}' to scan for git repos does not exist" );
                return gitRepoRoots;
            }
        }
        Log.Info( $"Checking for .git directories in {directoryRoot}" );
        foreach ( var gitDirPath in System.IO.Directory.EnumerateDirectories( directoryRoot, ".git", System.IO.SearchOption.AllDirectories ) ) {
            var repo = GitRepoInfo.CreateFromGitDir( new DirectoryInfo( gitDirPath ) ) ?? throw new IOException( $"Unable to determine parent of {gitDirPath}" );
            gitRepoRoots.Add( repo );
            Log.Info( $"    .git directories: {gitDirPath}\n" +
                      $"        parent: {repo.RootDir.FullName}" );
        }
        // seek first git directory in parents
        DirectoryInfo directory = new DirectoryInfo( directoryRoot );
        while ( directory.Parent is { } parentDirectory ) {
            if ( parentDirectory.EnumerateDirectories( ".git", SearchOption.TopDirectoryOnly ).SingleOrDefault() is { } ) {
                gitRepoRoots.Add( GitRepoInfo.CreateFromGitDir( parentDirectory ) );
                break;
            }
            directory = parentDirectory;
        }
        return gitRepoRoots;
    }

    private GitRepoInfo? getRepoForFile( string filePath ) {
        filePath = Path.GetFullPath( filePath );
        var repo = _gitRepoRoots.Where( rd => filePath.StartsWith( rd.RootDir.FullName.TrimEnd( Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar ) ).MaxBy( d => d.RootDir.FullName.Length );
        if ( repo is not { } && this.SourceScanDir is { } && !filePath.StartsWith( this.SourceScanDir.TrimEnd( Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar ) ) {
            this._gitRepoRoots.AddRange( scanRepoRoots( filePath ) );
            repo = _gitRepoRoots.Where( rd => filePath.StartsWith( rd.RootDir.FullName.TrimEnd( Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar ) ).MaxBy( d => d.RootDir.FullName.Length );
        }
        return repo;
    }
}

public class RepoNotFoundException : System.Exception { }

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

/*
 *
 */

public readonly record struct CharPosition(
    int Line,
    int Column
);

readonly record struct SourceText(
    string       Text,
    string       Level,
    string       FilePath,
    CharPosition Start,
    CharPosition End
) {
    public string FormattedFileLocation( ) =>
        System.IO.Path.GetFileName( FilePath )
        + " " + Start.Line + ":" + Start.Column;

    public string SortableLocationString =>
        System.IO.Path.GetFileName( FilePath )
        + "_" + Start.Line.ToString().PadLeft( 6, '0' )
        + ":" + Start.Column.ToString().PadLeft( 6, '0' );

    public string MarkdownSafeText( ) =>
        Text.Replace( "\n", "<br />" );
}

/*
 *
 */

public interface INamedObject {
    public string Name { get; }
}

public class CsProjInfo : INamedObject, IEquatable<CsProjInfo> {
    [ SuppressMessage( "ReSharper", "MemberCanBePrivate.Global" ) ]
    public GitRepoInfo GitRepo { get; init; }

    [ JsonConstructor ]
    public CsProjInfo( string filePath, GitRepoInfo gitRepo ) {
        GitRepo                   = gitRepo;
        FilePath                  = filePath;
        DirectoryPath             = System.IO.Path.GetDirectoryName( filePath ) ?? throw new ArgumentException( $"Unable to determine directory name of {filePath}" );
        ProjectName               = System.IO.Path.GetFileNameWithoutExtension( filePath );
        RepoRelativePath          = Path.GetRelativePath( gitRepo.RootDir.FullName, filePath );
        RepoRelativeDirectoryPath = Path.GetDirectoryName( this.RepoRelativePath ) ?? throw new NullReferenceException();
    }

    public CsProjInfo( CsProjInfo toClone ) {
        FilePath                  = toClone.FilePath;
        DirectoryPath             = toClone.DirectoryPath;
        ProjectName               = toClone.ProjectName;
        GitRepo                   = toClone.GitRepo;
        RepoRelativePath          = toClone.RepoRelativePath;
        RepoRelativeDirectoryPath = Path.GetDirectoryName( this.RepoRelativePath ) ?? throw new NullReferenceException();
    }

    public string ProjectName   { get; }
    public string Name          => ProjectName;
    public string DirectoryPath { get; }
    [ SuppressMessage( "ReSharper", "MemberCanBePrivate.Global" ) ]
    public string RepoRelativePath { get; }
    public string RepoRelativeDirectoryPath { get; }
    public string FilePath                  { get; }

    public bool ContainsFile( string filePath ) =>
        filePath.StartsWith( this.DirectoryPath.TrimEnd( Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar );

    public bool Equals( CsProjInfo? other ) {
        return other?.FilePath == this.FilePath;
    }

    public override bool Equals( object? obj ) {
        return this.Equals( obj as CsProjInfo );
    }

    public static bool operator ==( CsProjInfo? left, CsProjInfo? right ) {
        return Equals( left, right );
    }

    public static bool operator !=( CsProjInfo? left, CsProjInfo? right ) {
        return !Equals( left, right );
    }

    public override int GetHashCode( ) {
        return this.FilePath.GetHashCode();
    }

    public override string ToString( ) =>
        $"CsProjInfo {{ {nameof(ProjectName)} = {ProjectName} {nameof(FilePath)} = {FilePath} }}";
}

/*
 *
 */

public class UrlMdShortUtils {
    private readonly Dictionary<string, string> _urlMap   = new (StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _usedUrls = new (StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _idStrToGenerated = new (StringComparer.OrdinalIgnoreCase);
    private readonly bool                       _generateIds;
    private readonly ActionConfig?              _config;

    public UrlMdShortUtils( ActionConfig? config = null, bool generateIds = false ) {
        _generateIds       = generateIds;
        _config            = config;
    }

    public string Add( string id, string url, bool isCode = false ) {
        string mdModifier = isCode ? "`" : String.Empty;
        if ( _generateIds ) {
            //
            if ( !_idStrToGenerated.TryGetValue( id, out string? generatedId ) ) {
                // generatedId = $"{_generatedIdPrefix}{_idSeq++}";
                generatedId = toBase36( getDeterministicHashCode( url ) );
                _urlMap.TryAdd( generatedId, url );
                _usedUrls.TryAdd( generatedId, url );
                _idStrToGenerated.TryAdd( id, generatedId );
            }

            return $"{mdModifier}[{id}][{generatedId}]{mdModifier}";
        }
        _urlMap.TryAdd( id, url );
        _usedUrls.TryAdd( id, url );
        return $"{mdModifier}[{id}]{mdModifier}";
    }

    public string GetFormattedLink( string id, bool isCode = false ) {
        string mdModifier = isCode ? "`" : String.Empty;
        if ( _generateIds ) {
            if ( _idStrToGenerated.TryGetValue( id, out string? generatedId ) ) {
                return $"{mdModifier}[{id}][{generatedId}]{mdModifier}";
            }
        } else if ( _urlMap.ContainsKey( id ) ) {
            return $"{mdModifier}[{id}]{mdModifier}";
        }
        return $"{mdModifier}{id}{mdModifier}";
    }

    public string AddSourceLink( string filePath, CharPosition? start = null, CharPosition? end = null, bool linkToBranch = false ) {
        if ( this._config is not { } config ) {
            throw new NullReferenceException( nameof(_config) );
        }
        string linkTitle = config.GetFormattedSourcePosition( filePath, start, end );
        if ( config.GetGitHubSourceLink( filePath, start, end, linkToBranch ) is not { } url ) {
            if ( !filePath.Contains( "Microsoft.NET.Sdk" ) ) {
                Log.Warn( $"Unable to create source link for {linkTitle}" );
            }
            return linkTitle;
        }
        // KILL
        try {
            Log.Debug( $"[{filePath}] [{start}] [{end}]: {new Uri( url ).AbsoluteUri}" );
        } catch ( System.UriFormatException e ) {
            Log.Error( $"Uri format failed for [{filePath}] [{start}] [{end}]: '{url}': {e.Message}" );
            throw;
        }
        return this.Add( linkTitle, url );
    }

    public void AddReferencedUrls( StringBuilder sb ) {
        foreach ( var (id, url) in _usedUrls.OrderByDescending( kv => kv.Key ) ) {
            try {
                sb.AppendLine( $"[{id}]: {new Uri( url ).AbsoluteUri}" );
            } catch ( System.UriFormatException e ) {
                Log.Error( $"Uri format failed for id '{id}': '{url}': {e.Message}" );
                throw;
            }
        }
    }


    private static string toBase36( uint h ) {
        uint         b     = 36;
        string       s     = String.Empty;
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        while ( h > 0 ) {
            s += chars[ ( int )( h % b ) ];
            h =  h / b;
        }
        return s.PadLeft( 7, '0' );
    }


    /// <summary>
    /// Create a predictable Hash code for input <paramref name="str"/>.
    /// This is a Hash code that remains the same across Hardware, OS, and program runs.
    /// </summary>
    /// <returns><see cref="uint"/> based Hash code</returns>
    private static uint getDeterministicHashCode( string str ) {
        unchecked {
            uint hash1 = ( 5381 << 16 ) + 5381;
            uint hash2 = hash1;

            for ( uint i = 0 ; i < str.Length ; i += 2 ) {
                hash1 = ( ( hash1 << 5 ) + hash1 ) ^ str[ ( int )i ];
                if ( i == str.Length - 1 ) {
                    break;
                }

                hash2 = ( ( hash2 << 5 ) + hash2 ) ^ str[ ( int )i + 1 ];
            }

            return hash1 + ( hash2 * 1566083941 );
        }
    }
}

/*
 *
 */

public static class Log {
    // only log if I'm testing in the action's repo
    // Note: Could also use Runner debug logging ; https://docs.github.com/en/actions/monitoring-and-troubleshooting-workflows/enabling-debug-logging
    public static bool ShouldLog { get; } =
        System.Environment.GetEnvironmentVariable( "GITHUB_ACTION_REPOSITORY" ) is not { Length: > 0 } actionRepo
        || ( System.Environment.GetEnvironmentVariable( "GITHUB_REPOSITORY" ) is { Length: > 0 } repo
             && repo == actionRepo ); // if no GITHUB_ACTION_REPOSITORY, then assume Debug mode

    public static void Debug( object msg ) {
        if ( ShouldLog ) {
            System.Console.WriteLine( msg );
        }
    }

    public static void Verbose( object msg ) {
        if ( ShouldLog ) {
            System.Console.WriteLine( msg );
        }
    }

    public static void Info( object msg ) {
        System.Console.WriteLine( msg );
    }

    public static void Warn( object msg ) {
        System.Console.WriteLine( $"WARNING: {msg}" );
    }

    public static void Error( object msg ) {
        System.Console.WriteLine( $"ERROR: {msg}" );
    }
}