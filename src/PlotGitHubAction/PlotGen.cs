using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;

using SkiaSharp;

namespace PlotGitHubAction;

public static class PlotGen {
    private static readonly JsonSerializerOptions _serializer_options = Utils.SERIALIZER_OPTIONS;

    public static string GetChartFilePath( string plotDefinitionsDir, string outputFileName ) => Path.Join( plotDefinitionsDir, outputFileName ) is var path && path.EndsWith( ".png" ) ? path : $"{path}.png";

    public static void CreatePlot( string jsonString, string plotDefinitionsDir ) {
        Log.Info( $"==== {nameof(CreatePlot)} ====" );
        Log.Debug( $"Source JSON:\n{jsonString}" );
        if ( JsonDocument.Parse( jsonString ).RootElement is { ValueKind: JsonValueKind.Array } jsonDocument ) {
            Log.Verbose( "Enumerating multiple plots." );
            foreach ( var individualPlotJson in jsonDocument.EnumerateArray() ) {
                Log.Verbose( "  Component Plot:\n" + individualPlotJson.GetRawText() );
                createPlot( individualPlotJson.GetRawText(), plotDefinitionsDir );
            }
        } else {
            createPlot( jsonString, plotDefinitionsDir );
        }
    }

    private static void createPlot( string jsonString, string plotDefinitionsDir ) {
        Log.Info( $"==== {nameof(CreatePlot)} ====" );
        Log.Debug( $"Source JSON:\n{jsonString}" );

        var plt = new Plot();
        plt.Font.Automatic();

        foreach ( var font in SKFontManager.Default.FontFamilies ) {
            Log.Debug( font );
        }
        Log.Debug( $"Default   = {Fonts.Default}" );
        Log.Debug( $"Sans      = {Fonts.Sans}" );
        Log.Debug( $"Serif     = {Fonts.Serif}" );
        Log.Debug( $"Monospace = {Fonts.Monospace}" );
        Log.Debug( $"System    = {Fonts.System}" );

        Log.Debug( "---" );
        IXYPlotConfig config;
        AxisType      xAxisType = ( JsonSerializer.Deserialize<XYPlotConfigAxisTypePartial>( jsonString, _serializer_options ) ?? throw new JsonException( "Unable to parse JSON" ) ).XAxisType;
        if ( xAxisType == AxisType.DateTime ) {
            Log.Debug( "XAxis is DateTime" );
            plt.Axes.DateTimeTicksBottom();
            config = JsonSerializer.Deserialize<XYPlotConfig<string>>( jsonString, _serializer_options ) ?? throw new JsonException( "Unable to parse JSON" );
        } else {
            config = JsonSerializer.Deserialize<XYPlotConfig<double>>( jsonString, _serializer_options ) ?? throw new JsonException( "Unable to parse JSON" );
        }
        if ( config is not { OutputFileName: { } outputFileName } ) {
            Log.Error( "Unable to parse." );
            return;
        }

        string outputPath = GetChartFilePath( plotDefinitionsDir, outputFileName );
        if ( !outputPath.EndsWith( ".png" ) ) {
            outputPath += ".png";
        }


        plt.Title( config.Title );

        foreach ( IXYData data in config.Data ) {
            double[] xData = xAxisType switch {
                                 AxisType.DateTime when data is XYData<string> { X: var dateStrings } =>
                                     dateStrings.Select( x => DateTime.Parse( x ).ToOADate() ).ToArray(),
                                 _ => data.GetChartXData()
                             };
            var     sorted = xData.Zip( data.GetChartYData() ).OrderBy( t => t.First ).ToArray();
            Scatter series = plt.Add.Scatter( sorted.Select( t => t.First ).ToArray(), sorted.Select( t => t.Second ).ToArray() );
            if ( data.LinePattern is { } linePattern ) {
                Log.Debug( $"Setting {nameof(IXYData.LinePattern)} for {data.Title} to {linePattern}" );
                series.LineStyle.Pattern = linePattern;
            }
            if ( data.LineColor is { } lineColor ) {
                Log.Debug( $"Setting {nameof(IXYData.LineColor)} for {data.Title} to {lineColor} ({lineColor.Red},{lineColor.Green},{lineColor.Blue} ; {lineColor.ToStringRGB()})" );
                series.LineStyle.Color = lineColor;
            }
            if ( data.LineWidth is { } lineWidth ) {
                Log.Debug( $"Setting {nameof(IXYData.LineWidth)} for {data.Title} to {lineWidth}" );
                series.LineStyle.Width = lineWidth;
            }
            if ( data is { MarkerShape: { } markerShape, MarkerSize: var markerSize } ) {
                markerSize ??= 7;
                Log.Debug( $"Setting Marker for {data.Title} to (Shape={markerShape} Size={markerSize})" );
                series.MarkerStyle = new MarkerStyle(
                    shape: markerShape,
                    size: markerSize.Value,
                    color: series.LineStyle.Color );
            }
            series.LegendText = data.Title;
            // diag
            for ( int i = 0 ; i < xData.Length ; i++ ) {
                Log.Verbose( $"#{i}\t{sorted[ i ].First} x {sorted[ i ].Second}" );
            }
        }

        /*
         * Y-Axis Tick Generator
         */

        if ( config.YAxisType switch {
                 AxisType.Percent => new NumericAutomatic { LabelFormatter = static v => $"{v:p1}" },
                 // AxisType.Numeric => new ScottPlot.TickGenerators.NumericFixedInterval { Interval = 1 },
                 // https://github.com/ScottPlot/ScottPlot/blob/main/src/ScottPlot5/ScottPlot5/TickGenerators/NumericAutomatic.cs
                 AxisType.Numeric => new NumericAutomatic { IntegerTicksOnly = true },
                 _                => ( ITickGenerator? )null
             } is { } tickGenerator ) {
            plt.Axes.Left.TickGenerator = tickGenerator;
        }

        // If the Y-Axis is a Percentage, constrain bounds to 0-100
        // https://scottplot.net/cookbook/5.0/axis-and-ticks/#manually-set-axis-limits
        if ( config.YAxisType == AxisType.Percent ) {
            plt.Axes.Left.Max = 1;
            Log.Debug( $"Left  Axis max={plt.Axes.Left.Max}" );
            Log.Debug( $"Right Axis max={plt.Axes.Right.Max}" );
        }
        // plt.YAxis.Min = 0;
        plt.Axes.SetLimits( bottom: 0 );
        Log.Debug( $"Axes.Left min={plt.Axes.Left.Min}" );
        Log.Debug( $"Axes.Right min={plt.Axes.Right.Min}" );
        // plt.Margins( horizontal: 0.05D, vertical: 0.0, apply: true ); // URGENT: restore?
        plt.Axes.Margins( left: 0, right: 0.01, bottom: 0.01, top: 0 );
        // plt.Margins( 0, 0 ); // URGENT: restore?
        foreach ( var axis in plt.Axes.GetAxes() ) {
            axis.FrameLineStyle.Color = new Color( 0, 0, 0, 0 );
        }
        // plt.XAxis.FrameLineStyle.Color = new Color(0, 0, 0, 0);
        // plt.YAxis.FrameLineStyle.Color = new Color(0, 0, 0, 0);
        // plt.XAxis.FrameLineStyle.IsVisible = false; // KILL ??
        // plt.YAxis.FrameLineStyle.IsVisible = false; // KILL ??

        /*
         * Legend
         * https://scottplot.net/cookbook/5.0/configuring-legends/#legend-customization
         */
        plt.Legend.IsVisible = true;
        plt.Legend.Alignment = Alignment.LowerLeft;

        Log.Info( $"Writing to path: {outputPath}\n"          +
                  $"YAxis:           {plt.Axes.Left.Range}\n" +
                  $"Size:            {config.Width} × {config.Height}\n" );


        plt.SavePng( outputPath, config.Width, config.Height );
        if ( OperatingSystem.IsLinux() ) {
            // allow the user to change the files if they need
            ( new FileInfo( outputPath ) ).UnixFileMode =
                UnixFileMode.UserRead   |
                UnixFileMode.UserWrite  |
                UnixFileMode.GroupRead  |
                UnixFileMode.GroupWrite |
                UnixFileMode.OtherRead  |
                UnixFileMode.OtherWrite;
        }
    }
}