using System;

namespace PlotGitHubAction;

// https://scottplot.net/cookbook/5.0/scottplot-5-quickstart/#scatter-plot
public static class Program {
    public static int Main( string[] args ) {
        Console.WriteLine( "Starting..." );
        Console.WriteLine( $"{ActionConfig.NAME} Version: {ActionConfig.VERSION}" );
        Console.WriteLine( $"Debug Logging is {( Log.ShouldLog ? "Enabled" : "Disabled" )}" );
        Log.Debug( $"Current directory is {System.Environment.CurrentDirectory}" );

        foreach ( System.Collections.DictionaryEntry kv in System.Environment.GetEnvironmentVariables() ) {
            Log.Debug( $"{kv.Key,-30} = {kv.Value}" );
        }

        ActionConfig config = ActionConfig.CreateFromEnvironment();
        Log.Debug( config );

        RepoAnalysis.Run( config );

        if ( config.PlotDefinitionsDir is { } ) {
            Log.Info( $"Scanning for Plot Definitions in {config.PlotDefinitionsDir}" );
            foreach ( var file in System.IO.Directory.EnumerateFiles( config.PlotDefinitionsDir ) ) {
                if ( file.EndsWith( ".json" ) ) {
                    Log.Debug( $"Loading configuration from {file}" );
                    string configJsonString = System.IO.File.ReadAllText( file );
                    PlotGen.CreatePlot( configJsonString, config.PlotOutputDir );
                } else {
                    Log.Debug( $"Skipping file {file}" );
                }
            }
        }
        return 0;
    }
}