using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using System.Text.Json;

using ScottPlot;


namespace PlotGitHubAction;

public class TodoScanner {
    [ SuppressMessage( "ReSharper", "MemberCanBePrivate.Global", Justification = "Possible future API use." ) ]
    public string JsonHistoryFileName { get; init; } = "todo_history.json";
    [ SuppressMessage( "ReSharper", "MemberCanBePrivate.Global", Justification = "Possible future API use." ) ]
    public string MarkdownSummaryFileName { get; init; } = "TO-DO.md";

    private const    string                               _chart_filename_per_project  = @"todo_history";
    private const    string                               _chart_filename_per_category = @"todo_history_categories";
    public const     string                               CHART_FILENAME_TOTAL         = @"todo_history_total";
    public readonly  string                               MarkdownOutputPath;
    private readonly string                               _directoryRoot;
    private readonly string                               _jsonHistoryPath;
    private readonly ActionConfig                         _config;
    private          int                                  _totalFound   = 0;
    private readonly Dictionary<string, List<SourceText>> _todos        = new (); // filename => list of Todos
    private readonly List<ProjectCount>                   _csProjCounts = new ();
    private readonly HashSet<string>                      _categories   = new ();

    public TodoScanner( ActionConfig config ) {
        Log.Info( $"\n==== {nameof(TodoScanner)} ====" );
        ArgumentNullException.ThrowIfNull( config.SourceScanDir );
        this._directoryRoot     = config.SourceScanDir;
        this._config            = config;
        this._jsonHistoryPath   = Path.Join( config.MetaDataOutputDir, this.JsonHistoryFileName );
        this.MarkdownOutputPath = Path.Join( config.OutputDir, this.MarkdownSummaryFileName );

        System.IO.Directory.CreateDirectory( config.MetaDataOutputDir );
        System.IO.Directory.CreateDirectory( config.ToDoOutputDir );
    }

    public void ScanForTodos( ) {
        foreach ( var filePath in System.IO.Directory.EnumerateFiles( _directoryRoot, "*", System.IO.SearchOption.AllDirectories ) ) {
            Log.Verbose( $"    Scanning file: {filePath}" );
            if ( Regex.IsMatch( filePath, @"(\.(cs|a?xa?ml|csproj|props|ps1|psm|jsonc?|md|sql|ya?ml|sh|editorconfig)|Dockerfile)$" ) ) {
                Log.Verbose( $"      File pattern match. Scanning file for TO-DOs: {filePath}" );
                _todos[ filePath ] = ScanFileForTodos( filePath, ref _totalFound, _categories );
            }
        }
        Log.Info( "      Found {_totalFound:n0} total To-Do items. Outputting Result" );
        string markdownOutput = getMarkdownResult();
        System.IO.File.WriteAllText( this.MarkdownOutputPath, markdownOutput );

        Log.Info( "  Updating JSON History" );
        updateJsonHistory();
    }

    public static MatchCollection GetMatches( string lineText, string fileExtension ) =>
        Regex.Matches(
            lineText,
            fileExtension.TrimStart( '.' ) switch {
                @"cs" or @"json" or @"jsonc"                                   => /* language=regexp */ @"((?<MultiLineOpen>/\*+)[\s\*]*|//[ ]*)(?<Level>TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2}):? ?(?<Content>(?(MultiLineOpen)(.*?\n?)+|[^\n]*?))[ ]*(?(MultiLineOpen)(?<MultiLineClose>\*/)|($|(?=// ?(TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2}))))",
                @"axaml" or @"xaml" or @"xml" or @"csproj" or @"props"         => /* language=regexp */ @"(?<MultiLineOpen><!--)[ \t]*(?<Level>TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2})(?: *: *(?<Content>(.+?\n?)*?))?\s*(?<MultiLineClose>-->)",
                @"ps1" or @"psm"                                               => /* language=regexp */ @"((?<MultiLineOpen>\<#\s*#?)|#)[ ]*(?<Level>TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2}):? ?(?<Content>(?(MultiLineOpen)(.+?\n??)+?|[^\n]*?))(?(MultiLineOpen)(?<MultiLineClose>\s*\#\>)| *$)",
                @"sh" or @"yaml" or @"yml" or @"Dockerfile" or @"editorconfig" => /* language=regexp */ @"#[ ]*(?<Level>TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2})(?:: *(?<Content>.+?))?[ ]*(?=# *(TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2})|$)",
                @"md"                                                          => /* language=regexp */ @"^[ \t]*- \[ \][ ]+(?<Level>TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2})?:? *(?<Content>(.+?(\n {2,}[a-zA-Z0-9])?)+)[ ]*$",
                @"sql"                                                         => /* language=regexp */ @"((?<MultiLineOpen>/\*+)[\s\*]*|--[ ]*)(?<Level>TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2}):? ?(?<Content>(?(MultiLineOpen)(.*?\n?)+|[^\n]*?))[ ]*(?(MultiLineOpen)(?<MultiLineClose>\*/)|((?=-- ?(TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2})|$)))",
                @"py"                                                          => /* language=regexp */ """"((?<MultiLineOpen>""")[\s\*]*|#[ ]*)(?<Level>TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2}):? ?(?<Content>(?(MultiLineOpen)(.*?\n?)+|[^\n]*?))[ ]*(?(MultiLineOpen)(?<MultiLineClose>""")|($|(?=# ?(TODO|NOTE|BUG|KILL|URGENT|NET[0-9]{0,2}))))"""",
                _                                                              => throw new ArgumentException( $"Unsupported file type {fileExtension}" )
            },
            RegexOptions.Multiline
        );
    
    
    private static CharPosition getLineForPos( int[] lineEnds, int index ) {
        int lineCnt = 0;
        while ( lineEnds[ lineCnt ] <= index ) {
            lineCnt++;
        }
        return new CharPosition(
            lineCnt + 1,
            index   - ( lineCnt > 0 ? lineEnds[ lineCnt - 1 ] : 0 )
        );
    }

    internal static List<SourceText> ScanFileForTodos( string filePath, ref int totalFound, HashSet<string> categories ) {
        List<SourceText> todos        = new ();
        int              foundAtStart = totalFound;
        string           allText      = System.IO.File.ReadAllText( filePath );

        int totalChars = 0;
        var lineEnds   = allText.Split( "\n" ).Select( ln => totalChars += ln.Length + 1 ).ToArray();
        foreach ( Match match in GetMatches( allText, System.IO.Path.GetExtension( filePath ) is { Length: > 0 } extension ? extension : System.IO.Path.GetFileName( filePath ) ) ) {
            var todo = new SourceText(
                match.Groups[ "Content" ].Value.TrimEnd(),
                match.Groups[ "Level" ].Value,
                filePath,
                Start: getLineForPos( lineEnds, ( match.Groups[ "Level" ] is { Length: >0 } level 
                                                    ? level.Index
                                                    : match.Groups[ "Content" ].Index ) + 1 ), // 1 start, not 0
                End: getLineForPos( lineEnds, ( String.IsNullOrWhiteSpace( match.Groups[ "Content" ].Value ) 
                                                    ? match.Groups[ "Level" ].Index + match.Groups[ "Level" ].Length
                                                    : match.Groups[ "Content" ].Index + match.Groups[ "Content" ].Value.TrimEnd().Length ) )
                                                    
            );
            categories.Add( match.Groups[ "Level" ].Value );
            todos.Add( todo );
            totalFound++;
        }
        Log.Debug( $"{filePath} - Checked {lineEnds.Length} lines" +
                   $"  Found {totalFound - foundAtStart}" );
        return todos;
    }
    /* TODO:
    Could remove up to the amount of whitespace equal to the col of the
    Level start or Content start to better align subsequent lines
    */

    private record ProjectCount( string ProjectName ) {
        public int                     TotalCount { get; set; }
        public Dictionary<string, int> LevelCount { get; set; } = new ();
    }
    
    private record TodoGroup( string Name ) : INamedObject;
    
    private string getMarkdownResult( ) {
        UrlMdShortUtils sourceUrls = new (_config, true);
        StringBuilder   links      = new ("# TO-DOs\n\n");
        StringBuilder   summary    = new ("## Summary\n\n");
        HashSet<string> filesNotInProjects = new( _todos.Keys );
        summary.AppendLine( _config.GetMarkdownChartLink( CHART_FILENAME_TOTAL ) );
        summary.AppendLine( _config.GetMarkdownChartLink( _chart_filename_per_project ) );
        summary.AppendLine( _config.GetMarkdownChartLink( _chart_filename_per_category ) );
        summary.AppendLine();

        StringBuilder sections = new ("\n## Projects\n");

        Dictionary<string, ( INamedObject info, ProjectCount counts)> csProjectInfos =
            _config.GetCsProjectsCopy()
                   .OrderBy( p => p.ProjectName )
                   .ToDictionary( p => p.ProjectName,
                                  p => ( p as INamedObject, new ProjectCount( p.ProjectName ) ) );
        const string noProjectName = @"Outside Any Project";
        csProjectInfos[noProjectName] = ( new TodoGroup(noProjectName), new ProjectCount(noProjectName ) );
        var categories = _categories.ToArray();
        _csProjCounts.AddRange( csProjectInfos.Values.Select( tpl => tpl.counts ) );

        List<string> cols = new () {
            "Project",
            "Total"
        };
        cols.AddRange( categories );
        List<string> colsSpaced = new () { "Project".PadRight( csProjectInfos.Keys.Select( k => k.Length ).Max() ) };
        colsSpaced.AddRange( cols.Skip( 1 ).Select( c => c ).ToList() );
        summary.AppendLine( String.Join( " | ", colsSpaced ) );
        string tableHeaderDivLine = String.Join( "-|-", colsSpaced.Select( ( _, idx ) => "-".PadLeft( colsSpaced[ idx ].Length, '-' ) ) );
        summary.AppendLine( tableHeaderDivLine );

        const int firstColumnWidth = 50;

        foreach ( var (csProjInfo, projectCount) in csProjectInfos.Values ) {
            string sectionLink = Regex.Replace( csProjInfo.Name.ToLower(), @"[ ]", "-" );
            sectionLink = Regex.Replace( sectionLink, @"[^a-z0-9\-]+", String.Empty );
            links.AppendLine( $"- [{csProjInfo.Name}](#{sectionLink})" );
            sections.AppendLine( $"\n### {csProjInfo.Name}\n" );
            sections.AppendLine( $"{"Source",-firstColumnWidth} | {"Category",-10} | Text" );
            sections.AppendLine(
                String.Empty.PadLeft( firstColumnWidth, '-' ) + "-|-" +
                String.Empty.PadLeft( 10, '-' )               + "-|-" +
                String.Empty.PadLeft( 60, '-' ) );

            foreach ( var category in categories ) {
                projectCount.LevelCount[ category ] = 0;
            }
            IEnumerable<KeyValuePair<string, List<SourceText>>> todosInGroup;
            if ( csProjInfo is CsProjInfo cspi ){
                todosInGroup = _todos.Where( t => t.Key.StartsWith( cspi.DirectoryPath.TrimEnd( Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar ) );
            } else {
                todosInGroup = _todos.Where( t => filesNotInProjects.Contains( t.Key ) ).ToArray();
            }
            foreach ( var file in todosInGroup.OrderBy( t => Path.GetFileName( t.Key ) ) ) {
                // 
                filesNotInProjects.Remove( file.Key );
                projectCount.TotalCount += file.Value.Count;
                foreach ( var category in _categories ) {
                    projectCount.LevelCount[ category ] += file.Value.Count( t => t.Level == category );
                }
                foreach ( var todo in file.Value.OrderBy( t => t.SortableLocationString ) ) {
                    //
                    sections.Append( sourceUrls.AddSourceLink(
                                         filePath: todo.FilePath,
                                         start: todo.Start,
                                         end: todo.End,
                                         linkToBranch: true
                                     ).PadRight( firstColumnWidth ) );
                    sections.Append( " | " );
                    sections.Append( todo.Level.PadRight( 10 ) );
                    sections.Append( " | " );
                    sections.AppendLine( todo.MarkdownSafeText() );
                }
            } // end of iterating over files
            summary.Append( projectCount.ProjectName.PadRight( colsSpaced[ 0 ].Length ) + " | " + projectCount.TotalCount.ToString().PadLeft( colsSpaced[ 1 ].Length ) + " | " );
            summary.AppendJoin( " | ", projectCount.LevelCount.Values.Select( ( val, idx ) => val.ToString().PadLeft( colsSpaced[ idx + 2 ].Length ) ) );
            summary.AppendLine();
        } // end of iterating over projects
        
        summary.Append( "**Total**".PadRight( colsSpaced[ 0 ].Length ) 
                      + " | " + _todos.Values.Sum( x => x.Count ).ToString().PadLeft( colsSpaced[ 1 ].Length ) 
                      + " | " );
        summary.AppendJoin( " | ", categories.Select( ( category, idx ) => _todos.Values.SelectMany( f => f.Where( t => t.Level == category )).Count().ToString().PadLeft( colsSpaced[ idx + 2 ].Length ) ) );

        Log.Debug( $"\nOutput Markdown ({categories.Length} Categories)\n**********" );
        Log.Debug( summary );

        sections.AppendLine();
        sections.AppendLine( "**********" );
        sections.AppendLine();
        sourceUrls.AddReferencedUrls( sections );
        sections.AppendLine();

        links.AppendLine();

        return links.Append( summary.Append( sections ) ).ToString();
    }
    
    /*
    private string getMarkdownResult( ) {
        UrlMdShortUtils sourceUrls = new (_config, true);
        StringBuilder   links      = new ("# TO-DOs\n\n");
        StringBuilder   summary    = new ("## Summary\n\n");
        HashSet<string> filesNotInProjects = new( );
        filesNotInProjects.AddRange( _todos.Keys );
        summary.AppendLine( _config.GetMarkdownChartLink( CHART_FILENAME_TOTAL ) );
        summary.AppendLine( _config.GetMarkdownChartLink( _chart_filename_per_project ) );
        summary.AppendLine( _config.GetMarkdownChartLink( _chart_filename_per_category ) );
        summary.AppendLine();

        StringBuilder sections = new ("\n## Projects\n");

        Dictionary<string, ( CsProjInfo info, ProjectCount counts)> csProjectInfos =
            _config.GetCsProjectsCopy()
                   .OrderBy( p => p.ProjectName )
                   .ToDictionary( p => p.ProjectName,
                                  p => ( p, new ProjectCount( p.ProjectName ) ) );
        var categories = _categories.ToArray();
        _csProjCounts.AddRange( csProjectInfos.Values.Select( tpl => tpl.counts ) );

        List<string> cols = new () {
            "Project",
            "Total"
        };
        cols.AddRange( categories );
        List<string> colsSpaced = new () { "Project".PadRight( csProjectInfos.Keys.Select( k => k.Length ).Max() ) };
        colsSpaced.AddRange( cols.Skip( 1 ).Select( c => c ).ToList() );
        summary.AppendLine( String.Join( " | ", colsSpaced ) );
        string tableHeaderDivLine = String.Join( "-|-", colsSpaced.Select( ( _, idx ) => "-".PadLeft( colsSpaced[ idx ].Length, '-' ) ) );
        summary.AppendLine( tableHeaderDivLine );

        const int firstColumnWidth = 50;

        foreach ( var (csProjInfo, projectCount) in csProjectInfos.Values ) {
            string sectionLink = Regex.Replace( csProjInfo.ProjectName.ToLower(), @"[ ]", "-" );
            sectionLink = Regex.Replace( sectionLink, @"[^a-z0-9]", String.Empty );
            links.AppendLine( $"- [{csProjInfo.ProjectName}](#{sectionLink})" );
            sections.AppendLine( $"\n### {csProjInfo.ProjectName}\n" );
            sections.AppendLine( $"{"Source",-firstColumnWidth} | {"Category",-10} | Text" );
            sections.AppendLine(
                String.Empty.PadLeft( firstColumnWidth, '-' ) + "-|-" +
                String.Empty.PadLeft( 10, '-' )               + "-|-" +
                String.Empty.PadLeft( 60, '-' ) );

            foreach ( var category in categories ) {
                projectCount.LevelCount[ category ] = 0;
            }
            foreach ( var file in _todos.Where( t => t.Key.StartsWith( csProjInfo.DirectoryPath.TrimEnd( Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar ) )
                                        .OrderBy( t => Path.GetFileName( t.Key ) )
                    ) {
                // 
                filesNotInProjects.Remove( file );
                projectCount.TotalCount += file.Value.Count;
                foreach ( var category in _categories ) {
                    projectCount.LevelCount[ category ] += file.Value.Count( t => t.Level == category );
                }
                foreach ( var todo in file.Value.OrderBy( t => t.SortableLocationString ) ) {
                    //
                    sections.Append( sourceUrls.AddSourceLink(
                                         filePath: todo.FilePath,
                                         start: todo.Start,
                                         end: todo.End,
                                         linkToBranch: true
                                     ).PadRight( firstColumnWidth ) );
                    sections.Append( " | " );
                    sections.Append( todo.Level.PadRight( 10 ) );
                    sections.Append( " | " );
                    sections.AppendLine( todo.MarkdownSafeText() );
                }
            } // end of iterating over files
            summary.Append( projectCount.ProjectName.PadRight( colsSpaced[ 0 ].Length ) + " | " + projectCount.TotalCount.ToString().PadLeft( colsSpaced[ 1 ].Length ) + " | " );
            summary.AppendJoin( " | ", projectCount.LevelCount.Values.Select( ( val, idx ) => val.ToString().PadLeft( colsSpaced[ idx + 2 ].Length ) ) );
            summary.AppendLine();
        } // end of iterating over projects

        Log.Debug( $"\nOutput Markdown ({categories.Length} Categories)\n**********" );
        Log.Debug( summary );

        sections.AppendLine();
        sections.AppendLine( "**********" );
        sections.AppendLine();
        sourceUrls.AddReferencedUrls( sections );
        sections.AppendLine();

        links.AppendLine();

        return links.Append( summary.Append( sections ) ).ToString();
    }
    */

    private static readonly JsonSerializerOptions _json_options = new JsonSerializerOptions {
        WriteIndented = true,
    };


    private Dictionary<string, List<ProjectCount>> getHistory( ) {
        Dictionary<string, List<ProjectCount>> history = new ();
        if ( System.IO.Path.Exists( _jsonHistoryPath ) ) {
            history = JsonSerializer.Deserialize<Dictionary<string, List<ProjectCount>>>(
                System.IO.File.ReadAllText( _jsonHistoryPath ),
                _json_options
            )!;
        }
        return history;
    }

    private void updateJsonHistory( ) {
        var history = getHistory();
        history[ ActionConfig.NOW_STRING ] = _csProjCounts;
        string jsonOutput = JsonSerializer.Serialize( history, _json_options );
        Log.Debug( $"Writing updated JSON history to {_jsonHistoryPath}" );
        Log.Verbose( $"Updated History JSON:\n{jsonOutput}" );
        System.IO.File.WriteAllText( _jsonHistoryPath, jsonOutput );
    }

    public XYPlotConfig<DateTime>[] GetPlottable( ) {
        Log.Debug( $"Plotting {nameof(TodoScanner)}" );
        var history = getHistory();

        List<XYData<DateTime>> data = history
                                      .SelectMany( kv => Enumerable.Repeat( kv.Key, kv.Value.Count )
                                                                   .Zip( kv.Value, ( a, b ) => ( time: a, proj: b.ProjectName, total: b.TotalCount ) ) )
                                      .GroupBy( t => t.proj )
                                      .Select( g =>
                                                   new XYData<DateTime>(
                                                       Title: g.Key,
                                                       X: g.Select( v => DateTime.Parse( v.time ) ).ToArray(),
                                                       Y: g.Select( v => ( double )v.total ).ToArray()
                                                   ) { LinePattern = ( g.Key.Contains( "Tests" ) ? LinePattern.Dotted : null ) }
                                      ).ToList();
        var totalSeries = history
                          .Select( h =>
                                       ( date: DateTime.Parse( h.Key ),
                                         total: h.Value.Sum( p => p.TotalCount )
                                       ) ).ToArray();

        List<XYData<DateTime>> categoryData = new ();
        foreach ( var category in this._categories ) {
            var catSeries = history
                            .Select( h =>
                                         ( date: DateTime.Parse( h.Key ),
                                           total: h.Value.SelectMany( p => p.LevelCount.Where( lc => lc.Key == category ).Select( lc => ( double )lc.Value ) ).Sum()
                                         ) ).ToArray();
            // data.Add(
            categoryData.Add(
                new XYData<DateTime>(
                    Title: $"{category} Total",
                    X: catSeries.Select( t => t.date ).ToArray(),
                    Y: catSeries.Select( t => ( double )t.total ).ToArray()
                ) );
        }

        return new[] {
            new XYPlotConfig<DateTime>(
                Title: "TO-DO per Project",
                OutputFileName: _chart_filename_per_project,
                PlotType: PlotType.Scatter,
                Width: 1024,
                Height: 800,
                XAxisType: AxisType.DateTime,
                YAxisType: AxisType.Numeric,
                Data: data.ToArray()
            ),
            new XYPlotConfig<DateTime>(
                Title: "TO-DO per Category",
                OutputFileName: _chart_filename_per_category,
                PlotType: PlotType.Scatter,
                Width: 1024,
                Height: 800,
                XAxisType: AxisType.DateTime,
                YAxisType: AxisType.Numeric,
                Data: categoryData.ToArray()
            ),
            new XYPlotConfig<DateTime>(
                Title: "TO-DO - Total",
                OutputFileName: CHART_FILENAME_TOTAL,
                PlotType: PlotType.Scatter,
                Width: 1024,
                Height: 800,
                XAxisType: AxisType.DateTime,
                YAxisType: AxisType.Numeric,
                Data: new[] {
                    new XYData<DateTime>(
                        Title: "Total",
                        X: totalSeries.Select( t => t.date ).ToArray(),
                        Y: totalSeries.Select( t => ( double )t.total ).ToArray()
                    )
                }
            )
        };
    }
}