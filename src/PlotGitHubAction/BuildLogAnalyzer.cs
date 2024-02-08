using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PlotGitHubAction;

record WarnLogEntry(
    string        File,
    CharPosition? Position,
    string        Level,
    string?       Id,
    string        Message,
    // ReSharper disable once NotAccessedPositionalProperty.Global
    CsProjInfo Project
) {
    private readonly string? _idMarkdown = null;

    public string? IdMarkdown {
        get => _idMarkdown ?? Id;
        init => _idMarkdown = value;
    }

    public string SortableLocationString =>
        System.IO.Path.GetFileName( File )
        + "_" + ( Position?.Line.ToString()   ?? String.Empty ).PadLeft( 6, '0' )
        + ":" + ( Position?.Column.ToString() ?? String.Empty ).PadLeft( 6, '0' )
        + "-" + ( Id ?? String.Empty );
}

public class BuildLogAnalyzer {
    private readonly ActionConfig       _config;
    private readonly string             _directoryRoot;
    private readonly JsonHistoryPlotter _historyPlotter;
    public readonly  string             MarkdownPath;
    private const    string             _build_warnings_per_project_chart_file_name = "build_warnings";
    public const     string             BUILD_WARNINGS_TOTAL_CHART_FILE_NAME        = "build_warnings_total";
    public const     string             BUILD_WARNINGS_TOTAL_RECENT_CHART_FILE_NAME = "build_warnings_total_recent";
    private          string             _filePattern => _config.BuildLogFilePattern;

    private readonly Dictionary<CsProjInfo, HashSet<WarnLogEntry>> _projWarningsHs = new ();

    public BuildLogAnalyzer( ActionConfig config ) {
        Log.Info( "==== Build Log ====" );
        ArgumentNullException.ThrowIfNull( config.SourceScanDir );
        _config        = config;
        _directoryRoot = System.IO.Path.GetFullPath( config.SourceScanDir );
        _historyPlotter = new JsonHistoryPlotter(
            jsonHistoryPath: Path.Combine( config.BuildLogHistoryOutputDir, "build_log_history.json" )
        );
        MarkdownPath = Path.Combine( config.OutputDir, "BuildLogHistory.md" );
    }

    public void Analyze( ) {
        Log.Debug( $"  Scanning for build logs with pattern '{_filePattern}' in {_directoryRoot}" );
        //
        const string colDiv = " | ";

        UrlMdShortUtils helpUrls   = new ();
        UrlMdShortUtils sourceUrls = new (_config, true);

        StringBuilder summary  = new ("# Build Warnings\n\n");
        StringBuilder warnings = new ("## Warning Listing\n\n");

        // Charts
        summary.AppendLine();
        summary.AppendLine();
        summary.AppendLine( _config.GetMarkdownChartLink( BUILD_WARNINGS_TOTAL_RECENT_CHART_FILE_NAME ) );
        summary.AppendLine();
        summary.AppendLine( _config.GetMarkdownChartLink( BUILD_WARNINGS_TOTAL_CHART_FILE_NAME ) );
        summary.AppendLine();
        summary.AppendLine( _config.GetMarkdownChartLink( _build_warnings_per_project_chart_file_name ) );
        summary.AppendLine();

        warnings.AppendLine(
            "File".PadRight( 50 )
            + colDiv
            + "Id".PadRight( 9 )
            + colDiv
            + "Level".PadRight( 10 )
            + colDiv
            + "Message" );
        warnings.AppendLine(
            String.Empty.PadRight( 50, '-' )
            + colDiv
            + String.Empty.PadRight( 9, '-' )
            + colDiv
            + String.Empty.PadRight( 10, '-' )
            + colDiv
            + String.Empty.PadRight( 40, '-' ) );

        // filePath has the FullName (fully rooted/resolved path)
        Log.Info( $"Searching '{_directoryRoot}' for build log files matching pattern '{_filePattern}'" );
        foreach ( string filePath in System.IO.Directory.EnumerateFiles( _directoryRoot, _filePattern, System.IO.SearchOption.AllDirectories ).Order() ) {
            Log.Info( $"build log: {filePath}" );
            string logString = System.IO.File.ReadAllText( filePath );
            // Debug only - output all build logs into a single concat file
            if ( Log.ShouldLog ) {
                System.IO.File.AppendAllText(
                    Path.Combine( _config.OutputDir, "Combined.log" ),
                    logString + "\n"
                );
            }

            CsProjInfo? lastCsProj = null;
            foreach ( Match m in Regex.Matches(
                         logString,
                         @"^[ 0-9:]+>(?<File>[^(]+)\((?<Line>[0-9]+),(?<Column>[0-9]+)\): (?<Level>\w+) (?<Id>\w+)?: (?<Message>.+?)(?> \((?<DiagnosticHelpUrl>https?://.+/(?<DiagnosticHelpId>[^/.]+)(?>\.[a-zA-Z]+)?\)?)\))? \[(?<ProjectPath>[^\]\[]*)\]$",
                         RegexOptions.Multiline
                     ) ) {
                string  projPath  = m.Groups[ "ProjectPath" ].Value;
                string  issuePath = m.Groups[ "File" ].Value;
                string  message   = m.Groups[ "Message" ].Value;
                string? id        = m.Groups[ "Id" ].Value is { Length: > 0 } ? m.Groups[ "Id" ].Value : null;
                string? idMdText  = id;
                string? helpUrl   = m.Groups[ "DiagnosticHelpUrl" ].Value is { Length: > 0 } ? m.Groups[ "DiagnosticHelpUrl" ].Value : null;
                string? helpId    = m.Groups[ "DiagnosticHelpId" ].Value is { Length : > 0 } ? m.Groups[ "DiagnosticHelpId" ].Value : null;

                /* Parse message to enclose code in backticks */
                message = Regex.Replace( message, @"( |^)['""]", " `" );
                message = Regex.Replace( message, @"['""]( |$)", "` " );

                CsProjInfo proj;
                try {
                    proj = _config.GetProject( projPath );
                } catch ( Exception ) {
                    Log.Error( $"Failed to find project for project path '{projPath}' in [{String.Join( ", ", _config.GetCsProjectsCopy().Select( x => $"{x.ProjectName} = {x.FilePath}" ) )}" );
                    throw;
                }
                lastCsProj ??= proj;

                if ( ( helpId?.Equals( id, System.StringComparison.OrdinalIgnoreCase ) ?? false ) && helpUrl is { } ) {
                    idMdText = helpUrls.Add( id, helpUrl );
                } else if ( helpUrl is { Length: > 0 } ) {
                    message += $" ({helpUrl})"; // restore line if there was no id match
                }

                var fileRelProj = System.IO.Path.GetRelativePath( System.IO.Path.GetDirectoryName( projPath )!, issuePath );
                var fileRelRepoRoot = System.IO.Path.Combine(
                    proj.DirectoryPath,
                    fileRelProj );
                _projWarningsHs.TryAdd( proj, new HashSet<WarnLogEntry>() );
                _projWarningsHs[ proj ].Add(
                    new WarnLogEntry(
                        File: fileRelRepoRoot,
                        Position: ( ( Int32.TryParse( m.Groups[ "Line" ].Value, out int line ), Int32.TryParse( m.Groups[ "Column" ].Value, out var col ) ) is (true, true)
                            ? new CharPosition( line, col )
                            : null ),
                        Level: m.Groups[ "Level" ].Value,
                        Id: id,
                        Message: message,
                        Project: proj
                    ) { IdMarkdown = idMdText }
                );
            }
        }

        foreach ( var (proj, warnLogEntries) in _projWarningsHs.OrderBy( kv => kv.Key.ProjectName ) ) {
            warnings.AppendLine( $"""
                                  **{proj.ProjectName}** <a id="{proj.MarkdownId}"></a>
                                  """.PadRight( 50 ) + " |||" );
            foreach ( var entry in warnLogEntries.OrderBy( t => t.SortableLocationString ) ) {
                warnings.AppendLine(
                    sourceUrls.AddSourceLink(
                        filePath: entry.File,
                        start: entry.Position,
                        linkToBranch: true ).PadRight( 50 )
                    + $" | {entry.IdMarkdown,-9} | {entry.Level,-10} | {entry.Message}" );
            }
        }
        // per-project table
        Dictionary<string, (string projectId, int warningCount)> buildLogStats = _projWarningsHs.ToDictionary( p => p.Key.ProjectName, p => ( projectId: p.Key.MarkdownId, warningCount: p.Value.Count ) );
        summary.AppendLine();
        summary.AppendLine( "Project".PadRight( 50 )         + colDiv + "Warnings" );
        summary.AppendLine( String.Empty.PadRight( 50, '-' ) + colDiv + String.Empty.PadRight( 20, '-' ) );
        foreach ( var (projectName, (projectId, warningCount)) in buildLogStats.OrderBy( kv => kv.Key ) ) {
            summary.AppendLine( $"[{projectName}](#{projectId})".PadRight( 50 ) + colDiv + warningCount );
        }
        summary.AppendLine( "**TOTAL**".PadRight( 50 ) + colDiv + $"**{_projWarningsHs.SelectMany( p => p.Value ).Count()}**" );
        summary.AppendLine();

        // per Analyzer ID table
        Dictionary<string, int> analyzerIdStats = _projWarningsHs
                                                  .SelectMany( kv => kv.Value )
                                                  .Where( v => v.Id is { } )
                                                  .GroupBy( t => t.Id! )
                                                  .ToDictionary(
                                                      g => g.Key,
                                                      g => g.Count()
                                                  );

        summary.AppendLine();
        summary.AppendLine( "Analyzer ID".PadRight( 25 )     + colDiv + "Count" );
        summary.AppendLine( String.Empty.PadRight( 25, '-' ) + colDiv + String.Empty.PadRight( 20, '-' ) );
        foreach ( var (analyzerId, warningCount) in analyzerIdStats.OrderBy( kv => kv.Key ) ) {
            summary.AppendLine( helpUrls.GetFormattedLink( analyzerId ).PadRight( 25 ) + colDiv + warningCount );
        }
        summary.AppendLine();

        warnings.AppendLine();
        warnings.AppendLine();
        warnings.AppendLine( "**********" );
        warnings.AppendLine();
        sourceUrls.AddReferencedUrls( warnings );
        warnings.AppendLine();
        helpUrls.AddReferencedUrls( warnings );

        _historyPlotter.AddToHistory( buildLogStats.ToDictionary( kv => kv.Key, kv => kv.Value.warningCount ) );

        System.IO.File.WriteAllText( MarkdownPath, $"{summary}\n{warnings}" );
    }

    public XYPlotConfig<DateTime>[] GetPlottable( ) => new[] {
        _historyPlotter.AddDataToPlottable(
            new XYPlotConfig<DateTime>(
                Title: "Build Warnings per Project",
                OutputFileName: _build_warnings_per_project_chart_file_name,
                PlotType: PlotType.Scatter,
                Width: 1024,
                Height: 800,
                XAxisType: AxisType.DateTime,
                YAxisType: AxisType.Numeric,
                Data: Array.Empty<XYData<DateTime>>()
            ), PlotDataSelection.Projects ),
        _historyPlotter.AddDataToPlottable(
            new XYPlotConfig<DateTime>(
                Title: "Build Warnings Total",
                OutputFileName: BUILD_WARNINGS_TOTAL_CHART_FILE_NAME,
                PlotType: PlotType.Scatter,
                Width: 1024,
                Height: 800,
                XAxisType: AxisType.DateTime,
                YAxisType: AxisType.Numeric,
                Data: Array.Empty<XYData<DateTime>>()
            ), PlotDataSelection.Total ),
        _historyPlotter.AddDataToPlottable(
            new XYPlotConfig<DateTime>(
                Title: "Build Warnings Total - Recent",
                OutputFileName: BUILD_WARNINGS_TOTAL_RECENT_CHART_FILE_NAME,
                PlotType: PlotType.Scatter,
                Width: 1024,
                Height: 800,
                XAxisType: AxisType.DateTime,
                YAxisType: AxisType.Numeric,
                Data: Array.Empty<XYData<DateTime>>()
            ), PlotDataSelection.Total | PlotDataSelection.Recent )
    };
}