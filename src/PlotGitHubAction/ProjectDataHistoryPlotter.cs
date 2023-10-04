using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

using ScottPlot;

namespace PlotGitHubAction;

public class JsonHistoryPlotter {
    private string _jsonHistoryPath { get; }

    public JsonHistoryPlotter( string jsonHistoryPath ) {
        _jsonHistoryPath = jsonHistoryPath;
    }

    readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions {
        WriteIndented = true,
    };

    private Dictionary<string, Dictionary<string, int>> getHistory( ) {
        Dictionary<string, Dictionary<string, int>> history = new ();
        if ( System.IO.Path.Exists( _jsonHistoryPath ) ) {
            string historyFileStr = System.IO.File.ReadAllText( _jsonHistoryPath );
            history = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>( historyFileStr ) ?? throw new JsonException();
        }
        return history;
    }

    public void AddToHistory( Dictionary<string, int> newData ) {
        var history = getHistory();
        history[ ActionConfig.NOW_STRING ] = newData;
        var jsonOutput = JsonSerializer.Serialize( history, _jsonOptions );
        Log.Debug( jsonOutput );
        System.IO.File.WriteAllText( _jsonHistoryPath, jsonOutput );
    }
    
    private static Color getColorForProjectName( string name ) {
        if ( name.EndsWith( ".Tests" ) ) {
            Log.Info( $"Setting name from {name} to '{name[..^6]}'" );
            name = name[ ..^6 ];
        }
        var palette = new ScottPlot.Palettes.Tsitsulin();
        return palette.GetColor( (int) Utils.GetDeterministicHashCode( name ) );
    }


    public XYPlotConfig<DateTime> AddDataToPlottable( XYPlotConfig<DateTime> plottable, PlotDataSelection plotDataSelection = PlotDataSelection.Projects | PlotDataSelection.Total ) {
        Log.Info( "\n\n==== AddDataToPlottable ====\n" );
        var                    history = getHistory();
        List<XYData<DateTime>> data    = new ();
        if ( plotDataSelection.HasFlag( PlotDataSelection.Projects ) ) {
            data = history
                   .SelectMany( kv => Enumerable.Repeat( kv.Key, kv.Value.Count )
                                                .Zip( kv.Value, ( a, b ) => ( time: a, proj: b.Key, lineCount: b.Value ) ) )
                   .GroupBy( t => t.proj )
                   .Select( g =>
                                new XYData<DateTime>(
                                    Title: g.Key,
                                    X: g.Select( v => DateTime.Parse( v.time ) ).ToArray(),
                                    Y: g.Select( v => ( double )v.lineCount ).ToArray()
                                ) {
                                    LinePattern = g.Key.Contains( "Tests", StringComparison.InvariantCultureIgnoreCase )
                                        ? LinePattern.Dot
                                        : LinePattern.Solid,
                                    LineColor = getColorForProjectName(g.Key),
                                    MarkerShape = g.Key.Contains( "Tests", StringComparison.InvariantCultureIgnoreCase )
                                        ? MarkerShape.FilledSquare
                                        : MarkerShape.FilledCircle
                                }
                   ).ToList();
        }
        if ( plotDataSelection.HasFlag( PlotDataSelection.Total ) ) {
            var totalSeries = history
                              .Select( h =>
                                           ( date: DateTime.Parse( h.Key ),
                                             total: h.Value.Sum( p => p.Value )
                                           ) ).ToArray();
            data.Add(
                new XYData<DateTime>(
                    Title: "Total",
                    X: totalSeries.Select( t => t.date ).ToArray(),
                    Y: totalSeries.Select( t => ( double )t.total ).ToArray()
                ) );
        }
        return plottable with { Data = data.ToArray() };
    }
}

[ Flags ]
public enum PlotDataSelection {
    Projects = 0b01,
    Total    = 0b10
}