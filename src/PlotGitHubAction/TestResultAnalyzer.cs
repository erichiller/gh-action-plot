using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace PlotGitHubAction;

[ XmlRoot( "UnitTestResult", Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010", IsNullable = false ) ]
public record UnitTestResult {
    [ XmlIgnore ]
    public string ShortTestName =>
        Regex.Match( TestName, @"(?<ClassAndMethod>\w+\.\w+)($|\()" ).Groups[ "ClassAndMethod" ].Value;

    [ XmlAttribute( AttributeName = "executionId" ) ]
    public required string ExecutionId { get; set; } //="fda94305-b507-4079-9196-78b331b1f041" 

    [ XmlAttribute( AttributeName = "testId" ) ]
    public required string TestId { get; set; }
    //testId="98e23071-3a96-b37b-c807-6afac95180b5" 

    [ XmlAttribute( AttributeName = "testName" ) ]
    public required string TestName { get; set; }
    // testName="mkmrk.DataSource.Tests.Ibkr.DbStorageAgentTests.TickLast_MultiContract_SaveToDb_Should_ForceSave_OnDisposal" 

    [ XmlAttribute( AttributeName = "computerName" ) ]
    public required string ComputerName { get; set; }
    // computerName="fv-az471-492" 

    private System.TimeSpan _duration;

    [ XmlIgnore ]
    public TimeSpan Duration {
        get => _duration;
        set => _duration = value;
    }

    [ Browsable( false ) ]
    [ XmlAttribute( DataType = "duration", AttributeName = "duration" ) ]
    public required string DurationString {
        get => XmlConvert.ToString( _duration );
        set => _duration = String.IsNullOrEmpty( value )
            ? TimeSpan.Zero
            : TimeSpan.Parse( value );
    }


    [ XmlAttribute( AttributeName = "startTime" ) ]
    public required DateTimeOffset StartTime { get; set; }
    // startTime="2023-07-23T08:13:40.6752240-05:00" 

    [ XmlAttribute( AttributeName = "endTime" ) ]
    public required DateTimeOffset EndTime { get; set; }
    // endTime="2023-07-23T08:13:40.6752242-05:00" 

    [ XmlAttribute( AttributeName = "testType" ) ]
    public required string TestType { get; set; }
    // testType="13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b" 

    [ XmlAttribute( AttributeName = "outcome" ) ]
    public required string Outcome { get; set; }
    // outcome="Passed" 

    [ XmlAttribute( AttributeName = "testListId" ) ]
    public required string TestListId { get; set; }
    // testListId="8c84fa94-04c1-424b-9868-57a2d4851a1d" l

    [ XmlAttribute( AttributeName = "relativeResultsDirectory" ) ]
    public required string RelativeResultsDirectory { get; set; }
    // relativeResultsDirectory="fda94305-b507-4079-9196-78b331b1f041"

    public required Output Output { get; set; }

    /// <summary>
    /// Gets the highest path and line in the Exception StackTrace.
    /// </summary>
    public MethodPosition? GetErrorHighestLocalSource( ) {
        if ( this.Output is { ErrorInfo.StackTrace: { } stackTrace } ) {
            // 
            var match = Regex.Match(
                stackTrace,
                @"^ *at (?<MethodName>[^\)]+\)) in (?<FilePath>[^:]+):line (?<Line>[0-9]+)$",
                RegexOptions.Multiline
            );
            Log.Debug( $"[{nameof(GetErrorHighestLocalSource)}] Match? {match.Success}" );
            if ( match.Groups[ "MethodName" ].Value is { Length : > 0 } methodName
                 && match.Groups[ "FilePath" ].Value is { Length: > 0 } filePath
                 && match.Groups[ "Line" ].Value is { Length    : > 0 } line
               ) {
                return new MethodPosition(
                    methodName,
                    filePath,
                    Int32.Parse( line )
                );
            }
            Log.Warn( $"[{nameof(GetErrorHighestLocalSource)}] Invalid regex result: {match}\n\t"
                      + $"MethodName: '{match.Groups[ "MethodName" ].Value}'\n\t"
                      + $"FilePath: '{match.Groups[ "FilePath" ].Value}'\n\t"
                      + $"Line: '{match.Groups[ "Line" ].Value}'"
            );
        }
        Log.Warn( $"[{nameof(GetErrorHighestLocalSource)}] Could not determine Method Position for {this}" );
        return null;
    }
}

public record MethodPosition(
    string MethodName,
    string FilePath,
    int    Line
);

public record Output {
    public required string StdOut { get; set; }

    public ErrorInfo? ErrorInfo { get; set; }
}

[ SuppressMessage( "ReSharper", "ClassNeverInstantiated.Global" ) ]
public record ErrorInfo {
    public required string Message    { get; set; }
    public required string StackTrace { get; set; }
}

/*
 * TODO: Create GitHub Action check(s) to attach results to Action Run
 * see: GitHub REST API https://docs.github.com/en/free-pro-team@latest/rest/checks
 * get last check (eg. tests failed Delta)
 * example: https://github.com/LASER-Yi/workflow-status/tree/main
 */
public class TestResultAnalyzer {
    private readonly string               _outputDir;
    private readonly string               _directoryRoot;
    private readonly ActionConfig         _config;
    private readonly List<UnitTestResult> _testResults = new ();
    private readonly List<UnitTestResult> _failedTests = new ();

    public StringBuilder Sb { get; } = new StringBuilder();

    private readonly string _csvFilePath;
    public readonly  string MarkdownSummaryFilePath;

    private readonly JsonSerializerOptions _options = new JsonSerializerOptions { WriteIndented = true };

    public TestResultAnalyzer( ActionConfig config ) {
        ArgumentException.ThrowIfNullOrEmpty( config.TestResultsDir );
        ArgumentException.ThrowIfNullOrEmpty( config.TestFailureOutputDir );
        _config                 = config;
        _directoryRoot          = config.TestResultsDir;
        _outputDir              = config.TestFailureOutputDir;
        _csvFilePath            = System.IO.Path.Combine( _config.MetaDataOutputDir, "test_results.csv" );
        MarkdownSummaryFilePath = System.IO.Path.Combine( _config.OutputDir, "Test_Results.md" );
        Log.Info(
            $"==== {nameof(TestResultAnalyzer)} ====\n" +
            $"  DirectoryRoot: {_directoryRoot}\n"      +
            $"  OutputDir    : {_outputDir}\n" );
    }

    public void ScanForTrx( ) {
        //
        int trxFileCount = 0;
        Log.Debug( $"Scanning '{_directoryRoot}' and all subdirectories for trx files." );
        foreach ( var filePath in System.IO.Directory.EnumerateFiles( _directoryRoot, "*.trx", System.IO.SearchOption.AllDirectories ) ) {
            trxFileCount++;
            Log.Debug( $"trx: {filePath}" );
            this.extractTrx( filePath );
        }
        Log.Debug( $"Found {trxFileCount} trx files in {_directoryRoot}" );

        // CSV
        string commitSha = _config.CommitHash;
        if ( !System.IO.File.Exists( _csvFilePath ) ) {
            System.IO.File.WriteAllText( _csvFilePath,
                                         String.Join( ",", new string[] {
                                                          "Date",
                                                          "Commit SHA",
                                                          "Total Test Count",
                                                          "Failed Test Count",
                                                          "Failed Test Names"
                                                      } ) );
        }
        System.IO.File.AppendAllText( _csvFilePath,
                                      $"\n{ActionConfig.NOW_STRING},{commitSha},{_testResults.Count},{_failedTests.Count},{String.Join( ';', _failedTests.Select( f => f.ShortTestName ) )}" );
        writeMarkdownSummary();
    }

    private void writeMarkdownSummary( ) {
        UrlMdShortUtils sourceUrls = new (config: _config, generateIds: true);
        Sb.AppendLine( "# Test Failures\n" );

        Sb.AppendLine();
        Sb.Append( _failedTests.Count > 0 ? "❌" : "✅" );
        Sb.AppendLine( $" {_failedTests.Count} of {_testResults.Count} tests failed." );
        Sb.AppendLine();

        const string colDiv           = " | ";
        const int    firstColumnWidth = 50;
        if ( _failedTests.Count > 0 ) {
            Sb.AppendLine(
                "Test Name".PadRight( firstColumnWidth )
                + colDiv
                + "Output".PadRight( 15 )
                + colDiv
                + "Exception"
            );
            Sb.AppendLine(
                String.Empty.PadRight( firstColumnWidth, '-' )
                + colDiv
                + String.Empty.PadRight( 15, '-' )
                + colDiv
                + String.Empty.PadRight( 60, '-' )
            );
            foreach ( var failure in _failedTests ) {
                var (methodName, linkText, url) = getFormattedErrorSourceLink( failure );
                var testResultOutputRelPath = Path.GetRelativePath(
                    Path.GetDirectoryName( MarkdownSummaryFilePath )!,
                    getTestResultOutputFilePath( failure )
                );
                Log.Debug( $"methodName={methodName}, linkText={linkText}, url={url}" );
                Sb.Append( ( $"`{markdownEscape( methodName )}` in "
                             + ( linkText is not null && url is not null
                                 ? sourceUrls.Add(
                                     id: linkText,
                                     url: url,
                                     isCode: false
                                 )
                                 : String.Empty )
                           ).PadRight( firstColumnWidth ) );
                Sb.AppendLine(
                    colDiv
                    + $"[Output]({testResultOutputRelPath})".PadRight( 15 )
                    + colDiv
                    + singleLineMdCode( failure.Output.ErrorInfo?.Message )
                );
            }

            Sb.AppendLine();
            Sb.AppendLine( "**********" );
            Sb.AppendLine();
            sourceUrls.AddReferencedUrls( Sb );
            Sb.AppendLine();
        }

        System.IO.File.WriteAllText( MarkdownSummaryFilePath, Sb.ToString() );
    }

    private string markdownEscape( string inputString ) {
        return inputString.Replace( @"|", @"\|" );
    }

    private void extractTrx( string filePath ) {
        XDocument     xd = XDocument.Load( filePath );
        XmlSerializer sx = new XmlSerializer( typeof(UnitTestResult) );

        var results = xd.Elements().First().Elements()
                        .Single( static el => el.Name.LocalName == "Results" )
                        .Elements().ToArray();
        string[] nonFailedResultOutcomes = { "Passed", "NotExecuted" /* = Skipped */ };
        var failedResults = results.Where( el => !nonFailedResultOutcomes.Contains(
                                               el.Attributes()
                                                 .Single( attr => attr.Name == "outcome" )
                                                 .Value )
        );
        foreach ( var result in results ) {
            _testResults.Add( ( sx.Deserialize( result.CreateReader() ) as UnitTestResult )! );
        }

        foreach ( var result in failedResults ) {
            _failedTests.Add( ( sx.Deserialize( result.CreateReader() ) as UnitTestResult )! );
        }

        writeToFile( _failedTests );
    }

    private string singleLineMdCode( string? rawCode ) =>
        rawCode is { }
            ? ( "<pre>"
                + Regex.Replace(
                    rawCode,
                    @"\n *",
                    s => {
                        string[] spacesArr = new string[ s.Length - 1 ];
                        Array.Fill<string>( spacesArr, "&nbsp;" );
                        string spaces = String.Join( String.Empty, spacesArr );
                        return $"<br />{spaces}";
                    },
                    RegexOptions.Multiline
                ).TrimEnd()
                + "</pre>" )
            : String.Empty;


    private void writeToFile( List<UnitTestResult> failures ) {
        Log.Debug( $"==================\n== writeToFile (# failures: {failures.Count}) \n==============" );
        foreach ( var failure in failures ) {
            var str = $"{failure.ShortTestName}\n"
                      + String.Empty.PadLeft( failure.ShortTestName.Length, '=' ) + "\n\n"
                      + String.Join(
                          "\n",
                          JsonSerializer.Deserialize<Dictionary<string, object>>( JsonSerializer.Serialize( failure, _options ) )?
                                        .Where( kv => kv.Key != "Output" )
                                        .Select( kv => $"{kv.Key,-25} | {kv.Value}" ) ?? Array.Empty<string>() )
                      + "\n"
                      + ( failure.Output is { ErrorInfo.Message: { } errMsg }
                          ? $"\n\nError Message\n{String.Empty.PadLeft( 40, '-' )}\n{errMsg}\n"
                          : String.Empty )
                      + ( failure.Output is { ErrorInfo.StackTrace: { } stackTrace }
                          ? $"\n\nStack Trace\n{String.Empty.PadLeft( 40, '-' )}\n{stackTrace}\n"
                          : String.Empty )
                      + ( failure.Output is { StdOut: { } stdOut }
                          ? $"\n\nStandard Output\n{String.Empty.PadLeft( 40, '-' )}\n{stdOut}\n"
                          : String.Empty );
            Log.Debug( str + "\n" );
            string outFilePath = getTestResultOutputFilePath( failure );
            Log.Debug( $"Writing to file: {outFilePath}" );
            System.IO.File.WriteAllText( outFilePath, str );
        }
    }

    private string getTestResultOutputFilePath( UnitTestResult failure ) {
        return System.IO.Path.Combine( _outputDir, failure.ShortTestName + ".log" );
    }

    private string? getErrorSourceLink( UnitTestResult failure ) {
        if ( failure.GetErrorHighestLocalSource() is { } errorPosition ) {
            var resolvedPath = getUnitTestPath( errorPosition.FilePath );
            Log.Debug( $"[{nameof(getErrorSourceLink)}] resolvedPath='{resolvedPath}', errorPosition.Line='{errorPosition.Line}'" );
            if ( this._config.GetGitHubSourceLink( resolvedPath, errorPosition.Line ) is { } url ) {
                Log.Debug( $"[{nameof(getErrorSourceLink)}] url={url}, errorPosition={errorPosition}" );
                return url;
            }
            Log.Warn( $"[{nameof(getErrorSourceLink)}] Unable to get SourceLink via {nameof(ActionConfig.GetGitHubSourceLink)} for {errorPosition}" );
        }
        return null;
    }

    private (string methodName, string? linkText, string? url) getFormattedErrorSourceLink( UnitTestResult failure ) {
        if ( failure.GetErrorHighestLocalSource() is { } errorPosition
             && getErrorSourceLink( failure ) is { } url ) {
            return ( errorPosition.MethodName, $"`{errorPosition.FilePath}` line {errorPosition.Line}", url );
        }
        Log.Warn( $"[{nameof(getFormattedErrorSourceLink)}] Unable to extract regex via {nameof(UnitTestResult.GetErrorHighestLocalSource)} for {this}" );
        return ( methodName: failure.ShortTestName, null, null );
    }

    private string getUnitTestPath( string unitTestPathFromTrx ) {
        var csProjects = _config.GetCsProjectsCopy();
        var csProj = csProjects.Where( p => unitTestPathFromTrx.Contains( p.RepoRelativeDirectoryPath ) )
                               .MaxBy( p => p.RepoRelativeDirectoryPath.Length );
        if ( csProj is { } ) {
            if ( unitTestPathFromTrx.Split( csProj.RepoRelativeDirectoryPath ) is [ { }, { } relativePath ] ) {
                Log.Verbose( $"[{nameof(getUnitTestPath)}] Found match\n\t"                                           +
                             $"Split is {String.Join( ", ", unitTestPathFromTrx.Split( csProj.DirectoryPath ) )}\n\t" +
                             $"CsProject: {csProj}\n\t\twith DirectoryPath: {csProj.DirectoryPath}\n\t"               +
                             $"relativePath: {relativePath}\n\t"                                                      +
                             $"Combined Path: {Path.Combine( csProj.DirectoryPath, relativePath.TrimStart( System.IO.Path.DirectorySeparatorChar ) )}" );
                return Path.Combine( csProj.DirectoryPath, relativePath.TrimStart( System.IO.Path.DirectorySeparatorChar ) );
            }
            Log.Warn( $"[{nameof(getUnitTestPath)}] Found csProj: {csProj}, but was unable to determine the relative path." );
        }
        Log.Warn( $"[{nameof(getUnitTestPath)}] No {nameof(CsProjInfo)} matched {unitTestPathFromTrx}" );
        throw new Exception();
    }
}