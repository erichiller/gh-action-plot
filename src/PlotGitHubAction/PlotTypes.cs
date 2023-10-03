using System.Diagnostics.CodeAnalysis;
using System.Linq;

using ScottPlot;

namespace PlotGitHubAction;

public interface IXYData {
    public string       Title { get; }
    public double[]     GetChartXData( );
    public double[]     GetChartYData( );
    public LinePattern? LinePattern   { get; }
    public MarkerShape? MarkerShape { get; init; }
}

public interface IXYPlotConfig {
    public string    Title          { get; init; }
    public string    OutputFileName { get; init; }
    public PlotType  PlotType       { get; init; }
    public int       Width          { get; init; }
    public int       Height         { get; init; }
    public AxisType  XAxisType      { get; init; }
    public AxisType? YAxisType      { get; init; }
    public IXYData[] Data           { get; }
}

public record XYPlotConfig(
    string    Title,
    string    OutputFileName,
    PlotType  PlotType,
    int       Width,
    int       Height,
    AxisType  XAxisType,
    AxisType? YAxisType,
    IXYData[] Data
) : IXYPlotConfig;

public record XYPlotConfigAxisTypePartial( AxisType XAxisType );

[ SuppressMessage( "ReSharper", "InconsistentNaming" ) ]
public record XYPlotConfig<TXData>(
    string           Title,
    string           OutputFileName,
    PlotType         PlotType,
    int              Width,
    int              Height,
    AxisType         XAxisType,
    AxisType?        YAxisType,
    XYData<TXData>[] Data
) : IXYPlotConfig {
    IXYData[] IXYPlotConfig.Data => Data as IXYData[];
}

public enum PlotType {
    Scatter
}

public enum AxisType {
    /// <summary>
    /// Should be passed in as a double in the inclusive range 0 - 100
    /// </summary>
    Percent,
    /// <summary>
    /// Default double format
    /// </summary>
    Numeric,
    /// <summary>
    /// String based datetime, parsed with <see cref="System.DateTime.Parse(System.ReadOnlySpan{char},System.IFormatProvider?)"/>
    /// </summary>
    DateTime
}

public record XYData<TXData>(
    string   Title,
    TXData[] X,
    double[] Y
) : IXYData {
    public double[] GetChartXData( ) => X switch {
                                            double[] doubles        => doubles,
                                            System.DateTime[] dates => dates.Select( x => x.ToOADate() ).ToArray(),
                                            _                       => throw new System.Exception( "Invalid type" )
                                        };

    public double[]     GetChartYData( ) => Y;
    public LinePattern? LinePattern      { get; init; }
    public MarkerShape? MarkerShape      { get; init; }
}