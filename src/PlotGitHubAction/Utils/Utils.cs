using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using GitHubActionOutputId = System.String;

namespace PlotGitHubAction;

public static class Utils {
    public static readonly JsonSerializerOptions SERIALIZER_OPTIONS = new JsonSerializerOptions {
        Converters = {
            new JsonStringEnumConverter(),
            new ScottPlotColorConverter()
        },
        WriteIndented = true
    };

    /// <summary>
    /// Create a predictable Hash code for input <paramref name="str"/>.
    /// This is a Hash code that remains the same across Hardware, OS, and program runs.
    /// </summary>
    /// <returns><see cref="uint"/> based Hash code</returns>
    public static uint GetDeterministicHashCode( string str ) {
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

    public static void WriteToGitHubActionMessage( LogLevel logLevel, object msg, string? filePath, CharPosition? start, CharPosition? end, string? title = null ) =>
        WriteToGitHubActionMessage( logLevel, msg, filePath, start?.Line, end?.Line, start?.Column, end?.Column, title );

    public static void WriteToGitHubActionMessage( LogLevel logLevel, object msg, string? filePath = null, int? startLine = null, int? endLine = null, int? startColumn = null, int? endColumn = null, string? title = null ) {
        // https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#
        // ::notice file={name},line={line},endLine={endLine},title={title}::{message}
        StringBuilder sb = new StringBuilder( "::" );
        sb.Append( logLevel switch {
                       LogLevel.Error => "error",
                       LogLevel.Warn  => "warning",
                       LogLevel.Info  => "notice",
                       _              => throw new ArgumentException( $"Invalid log level: {logLevel}", nameof(logLevel) )
                   } );

        if ( filePath is { } || startColumn is { } || endColumn is { } || startLine is { } || endLine is { } || title is { } ) {
            sb.Append( ' ' );
            if ( filePath?.Split( System.IO.Path.DirectorySeparatorChar ) is [ .., var fileName ] ) {
                sb.Append( "file=" );
                sb.Append( fileName );
            }
            if ( startLine is { } ) {
                sb.Append( ",line=" );
                sb.Append( startLine ); // starts at 1
                if ( startColumn is { } ) {
                    sb.Append( ",col=" );
                    sb.Append( startColumn ); // starts at 1
                }
            }
            if ( endLine is { } ) {
                sb.Append( ",endLine=" );
                sb.Append( endLine ); // starts at 1
                if ( endColumn is { } ) {
                    sb.Append( ",endColumn=" );
                    sb.Append( endColumn ); // starts at 1
                }
            }
            if ( title is { } ) {
                sb.Append( ",title=" );
                sb.Append( title );
            }
        }
        sb.Append( "::" );
        sb.AppendLine( msg.ToString() );
        if ( !Log.ShouldLogToGitHubActions( LogLevel.Info ) ) {
            System.Console.Write( "WOULD LOG TO GITHUB:" );
        }
        System.Console.WriteLine( sb.ToString() );
    }

    // https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#adding-a-job-summary
    public static void WriteJobSummary( string summary ) { // TODO

        // echo "{markdown content}" >> $GITHUB_STEP_SUMMARY
        var githubStepSummaryFile = Environment.GetEnvironmentVariable( "GITHUB_STEP_SUMMARY", EnvironmentVariableTarget.Process );
        if ( !String.IsNullOrWhiteSpace( githubStepSummaryFile ) ) {
            System.Console.WriteLine( $"Writing Job Summary to: {githubStepSummaryFile}" );
            using var textWriter = new StreamWriter( path: githubStepSummaryFile, append: true, encoding: Encoding.UTF8 );
            textWriter.WriteLine( summary );
            // textWriter.WriteLine( $"updated-metrics={updatedMetrics}" );
            // textWriter.WriteLine( $"summary-title={title}" );
            // textWriter.WriteLine( "summary-details<<EOF" );
            // textWriter.WriteLine( summary );
            // textWriter.WriteLine( "EOF" );
        } else {
            Log.Warn( "GITHUB_STEP_SUMMARY environment variable not set" );
        }
    }

    // https://docs.github.com/actions/reference/workflow-commands-for-github-actions#setting-an-output-parameter
    public static void SetGitHubActionsOutput( GitHubActionOutputId name, string value ) {
        var githubOutputFile = Environment.GetEnvironmentVariable( "GITHUB_OUTPUT", EnvironmentVariableTarget.Process );
        if ( !String.IsNullOrWhiteSpace( githubOutputFile ) ) {
            Log.Debug( $"Writing output '{name}' with value '{value}' to: {githubOutputFile}" );
            using var textWriter = new StreamWriter( githubOutputFile, true, Encoding.UTF8 );
            if( value.Contains(System.Environment.NewLine)) {
                // for multiline content:
                textWriter.WriteLine( $"{name}<<EOF" );
                textWriter.WriteLine( value );
                textWriter.WriteLine( "EOF" );
            } else {
                // singleline
                textWriter.WriteLine( $"{name}={value}" );
            }
        } else {
            Log.Warn( "GITHUB_OUTPUT environment variable not set" );
        }
    }
    
}

internal static class GitHubActionOutputIds {
    public const GitHubActionOutputId TEST_RESULTS = "test_result";
    public const GitHubActionOutputId TEST_SUMMARY = "test_summary";
}