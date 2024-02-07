using System;
using System.Runtime.CompilerServices;

namespace PlotGitHubAction;

public static class Log {
    // only log if I'm testing in the action's repo
    // Note: Could also use Runner debug logging ; https://docs.github.com/en/actions/monitoring-and-troubleshooting-workflows/enabling-debug-logging
    public static bool ShouldLog { get; } =
        System.Environment.GetEnvironmentVariable( "GITHUB_ACTION_REPOSITORY" ) is not { Length: > 0 } actionRepo // if no GITHUB_ACTION_REPOSITORY, then assume Debug mode
        || ( System.Environment.GetEnvironmentVariable( "GITHUB_REPOSITORY" ) is { Length: > 0 } repo             // or if the current repo is the action repo (eg. for tests)
             && repo == actionRepo )
        || ( System.Environment.GetEnvironmentVariable( "INPUT_DEBUG" )?.Equals( "true", StringComparison.OrdinalIgnoreCase ) ?? false ); // or the user explicitly sets 


    private static readonly bool _is_git_hub_action = System.Environment.GetEnvironmentVariable( "GITHUB_ACTIONS" ) == "true";

    private static readonly LogLevel _git_hub_actions_log_level =
        System.Environment.GetEnvironmentVariable( "INPUT_LOG_LEVEL" )?.ToLowerInvariant() switch {
            "verbose"                 => LogLevel.Verbose,
            "debug"                   => LogLevel.Debug,
            "info" or "notice"        => LogLevel.Info,
            "warn" or "warning"       => LogLevel.Warn,
            "error"                   => LogLevel.Error,
            _ when _is_git_hub_action => LogLevel.Info,
            _                         => LogLevel.None
        };

    public static bool ShouldLogToGitHubActions( LogLevel logLevel ) => _is_git_hub_action && _git_hub_actions_log_level != 0 && _git_hub_actions_log_level <= logLevel;

    public static void Verbose( object msg ) {
        if ( ShouldLog ) {
            System.Console.WriteLine( msg );
        }
        if ( ShouldLogToGitHubActions( LogLevel.Verbose ) ) {
            // https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#setting-a-debug-message
            System.Console.WriteLine( $"::debug::{msg}" );
        }
    }

    public static void Debug( object msg ) {
        if ( ShouldLog ) {
            System.Console.WriteLine( msg );
        }
        if ( ShouldLogToGitHubActions( LogLevel.Debug ) ) {
            // https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#setting-a-debug-message
            System.Console.WriteLine( $"::debug::{msg}" );
        }
    }


    public static void Info( object msg, [ CallerArgumentExpression( nameof(msg) ) ] string? title = null, [ CallerFilePath ] string? filePath = null, [ CallerLineNumber ] int? lineNumber = null ) {
        System.Console.WriteLine( msg );
        if ( ShouldLogToGitHubActions( LogLevel.Info ) ) {
            Utils.WriteToGitHubActionMessage( LogLevel.Info, msg, title: title, filePath: filePath, startLine: lineNumber );
        }
    }

    public static void Warn( object msg, [ CallerArgumentExpression( nameof(msg) ) ] string? title = null, [ CallerFilePath ] string? filePath = null, [ CallerLineNumber ] int? lineNumber = null ) {
        System.Console.WriteLine( $"WARNING: {msg}" );
        if ( ShouldLogToGitHubActions( LogLevel.Warn ) ) {
            Utils.WriteToGitHubActionMessage( LogLevel.Warn, msg, title: title, filePath: filePath, startLine: lineNumber );
        }
    }

    public static void Error( object msg, [ CallerArgumentExpression( nameof(msg) ) ] string? title = null, [ CallerFilePath ] string? filePath = null, [ CallerLineNumber ] int? lineNumber = null ) {
        System.Console.WriteLine( $"ERROR: {msg}" );
        if ( ShouldLogToGitHubActions( LogLevel.Error ) ) {
            Utils.WriteToGitHubActionMessage( LogLevel.Error, msg, title: title, filePath: filePath, startLine: lineNumber );
        }
    }
}

public enum LogLevel {
    None    = 0,
    Verbose = 1,
    Debug   = 2,
    Info    = 3,
    Warn    = 4,
    Error   = 5,
}