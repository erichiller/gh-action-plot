using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace PlotGitHubAction;

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
            BuildLogFilePattern: System.Environment.GetEnvironmentVariable( "INPUT_BUILD_LOG_FILE_PATTERN" ) is { Length: > 0 } buildLogFilePattern
                ? buildLogFilePattern
                : "*-build.log",
            LineCountFilePattern: System.Environment.GetEnvironmentVariable( "INPUT_LINE_COUNT_FILE_PATTERN" ) is { Length: > 0 } lineCountFilePattern
                ? lineCountFilePattern
                : @"(?<!\.(verified|generated))\.(axaml|cs|dib|ps1)$",
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

    /// <summary>
    /// If <paramref name="sourcePath"/> is <c>null</c> use the <see cref="OutputDir"/>.
    /// </summary>
    public string GetMarkdownChartLink( string fileName, string? sourcePath = null ) {
        //
        string relPath = System.IO.Path.GetRelativePath(
            sourcePath ?? this.OutputDir,
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