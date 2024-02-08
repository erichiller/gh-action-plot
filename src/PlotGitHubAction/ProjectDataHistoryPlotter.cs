using System;
using System.Collections.Generic;
using System.Linq;
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
            Log.Info( $"Setting name from {name} to '{name[ ..^6 ]}'" );
            name = name[ ..^6 ];
        }
        var  palette    = new ScottPlot.Palettes.Tsitsulin();
        uint nameHash   = Utils.GetDeterministicHashCode( name );
        int  colorIndex = Math.Abs( ( int )nameHash );
        try {
            var color = palette.GetColor( colorIndex );
            Log.Info( $"Color:         {color.Red},{color.Green},{color.Blue} ; {color.ToStringRGB()}\n" +
                      $"ColorIndex:    '{colorIndex}'\n"                                                 +
                      $"Colors Length: {palette.Colors.Length}\n"                                        +
                      $"Name:          '{name}'\n"                                                       +
                      $"Name Hash:     '{nameHash}'\n"                                                   +
                      $"ColorIndex % ColorSize={colorIndex % palette.Colors.Length}" ); // TODO: decrease log level
            return color;
        } catch ( IndexOutOfRangeException ) {
            Log.Error( $"Error, could not find color for index: '{colorIndex}' within Colors of Length {palette.Colors.Length} using hash '{nameHash}' of name '{name}'. (ColorIndex % ColorSize={colorIndex % palette.Colors.Length})" );
            throw;
        }
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
                   .Select( g => {
                           bool isTest = g.Key.Contains( "Tests", StringComparison.InvariantCultureIgnoreCase );
                           return new XYData<DateTime>(
                               Title: g.Key,
                               X: g.Select( v => DateTime.Parse( v.time ) ).ToArray(),
                               Y: g.Select( v => ( double )v.lineCount ).ToArray()
                           ) {
                               LinePattern = isTest
                                   ? LinePattern.Dotted
                                   : LinePattern.Solid,
                               LineColor = getColorForProjectName( g.Key ),
                               LineWidth = isTest ? 1 : 2,
                               MarkerShape = isTest
                                   ? MarkerShape.OpenCircle
                                   : MarkerShape.FilledSquare,
                               MarkerSize = isTest ? 4 : 7
                           };
                       }
                   ).ToList();
        }
        if ( plotDataSelection.HasFlag( PlotDataSelection.Total ) ) {
            var totalSeries = history
                              .Select( h =>
                                           ( date: DateTime.Parse( h.Key ),
                                             total: h.Value.Sum( p => p.Value )
                                           ) ).ToArray();
            if ( plotDataSelection.HasFlag( PlotDataSelection.Recent ) ) {
                var recentCutoff = DateTime.Now - TimeSpan.FromDays( 30 );
                totalSeries = totalSeries.Where( x => x.date > recentCutoff ).ToArray();
            }
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
    Projects = 0b001,
    Total    = 0b010,
    Recent   = 0b100,
}