using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using ScottPlot;
using ScottPlot.Plottables;

namespace PlotGitHubAction;

public static class PlotGen {
    private static readonly JsonSerializerOptions _serializer_options = Utils.SERIALIZER_OPTIONS;

    public static string GetChartFilePath( string plotDefinitionsDir, string outputFileName ) => System.IO.Path.Join( plotDefinitionsDir, outputFileName ) is var path && path.EndsWith( ".png" ) ? path : $"{path}.png";

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

        var plt = new ScottPlot.Plot();

        foreach ( var font in SkiaSharp.SKFontManager.Default.FontFamilies ) {
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
            plt.AxisStyler.DateTimeTicks( Edge.Bottom );
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
            series.Label = data.Title;
            // diag
            for ( int i = 0 ; i < xData.Length ; i++ ) {
                Log.Verbose( $"#{i}\t{sorted[ i ].First} x {sorted[ i ].Second}" );
            }
        }

        /*
         * Y-Axis Tick Generator
         */

        Func<double, string>? tickGen = config.YAxisType switch {
                                            AxisType.Percent => static v => $"{v:p1}",
                                            AxisType.Numeric => static v => $"{v:n0}",
                                            _                => null
                                        };
        if ( tickGen is { } ) {
            plt.YAxis.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic { LabelFormatter = tickGen };
        }

        // If the Y-Axis is a Percentage, constrain bounds to 0-100
        // https://scottplot.net/cookbook/5.0/axis-and-ticks/#manually-set-axis-limits
        if ( config.YAxisType == AxisType.Percent ) {
            plt.YAxis.Max = 1;
            Log.Debug( $"YAxis max={plt.YAxis.Max}" );
            Log.Debug( $"LeftAxis max={plt.LeftAxis.Max}" );
        }
        // plt.YAxis.Min = 0;
        plt.SetAxisLimits( bottom: 0 );
        Log.Debug( $"YAxis min={plt.YAxis.Min}" );
        Log.Debug( $"LeftAxis min={plt.LeftAxis.Min}" );
        plt.Margins( horizontal: 0.05D, vertical: 0.0, apply: true ); // URGENT: restore?
        // plt.Margins( 0, 0 ); // URGENT: restore?
        foreach ( var axis in plt.XAxes ){
            axis.FrameLineStyle.Color = new Color(0, 0, 0, 0);
        }
        foreach ( var axis in plt.YAxes ){
            axis.FrameLineStyle.Color = new Color(0, 0, 0, 0);
        }
        // plt.XAxis.FrameLineStyle.Color = new Color(0, 0, 0, 0);
        // plt.YAxis.FrameLineStyle.Color = new Color(0, 0, 0, 0);
        // plt.XAxis.FrameLineStyle.IsVisible = false; // KILL ??
        // plt.YAxis.FrameLineStyle.IsVisible = false; // KILL ??

        /*
         * Legend
         * https://scottplot.net/cookbook/5.0/configuring-legends/#legend-customization
         */
        plt.Legend();
        var legend = plt.GetLegend();
        legend.Alignment = Alignment.LowerLeft;

        Log.Info( $"Writing to path: {outputPath}\n"      +
                  $"YAxis:           {plt.YAxis.Range}\n" +
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
