using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace PlotGitHubAction;

public class CoverageHistoryPlotter {
    readonly     string                                                   _filePattern = @"*_CoverageHistory.xml";
    readonly     string                                                   _directoryRoot;
    readonly     Dictionary<DateTime, Dictionary<string, List<Coverage>>> _dtAssemblyClasses = new ();
    public const string                                                   CHART_OUTPUT_PATH  = "Coverage";

    public CoverageHistoryPlotter( string coverageHistoryDir ) {
        _directoryRoot = coverageHistoryDir;
    }

    public XYPlotConfig<DateTime> ScanAndPlot( ) {
        this.scan();
        return this.plot();
    }

    private void scan( ) {
        Log.Debug( $"Scanning {_directoryRoot} for coverage history" );
        foreach ( var filePath in System.IO.Directory.EnumerateFiles( _directoryRoot, _filePattern, System.IO.SearchOption.AllDirectories ) ) {
            Log.Debug( $"coverage history: {filePath}" );
            parse( filePath );
        }
    }

    private void parse( string filePath ) {
        XDocument     xd              = XDocument.Load( filePath );
        XmlSerializer sx              = new XmlSerializer( typeof(Coverage) );
        var           coverageElement = xd.Elements().First();
        DateTime dt = DateTime.ParseExact( coverageElement.Attribute( "date" )?.Value ?? throw new XmlException( "Unable to retrieve date" ),
                                           "yyyy-MM-dd_H-mm-ss",
                                           System.Globalization.CultureInfo.InvariantCulture );
        Log.Debug( $"\tCoverage History Date: {dt}" );
        _dtAssemblyClasses[ dt ] = coverageElement.Elements().ToDictionary(
            el => el.Attribute( "name" )?.Value ?? throw new XmlException( "Unable to retrieve date" ),
            el => el.Elements().Select(
                c => sx.Deserialize( c.CreateReader() ) as Coverage ).ToList()
        )!;
        Log.Debug( $"\tFound {_dtAssemblyClasses[ dt ].Count} Assemblies" );
        foreach ( var el in _dtAssemblyClasses[ dt ] ) {
            Log.Debug( $"\t{el.Key}:  {el.Value.Count} classes" );
        }
    }

    private XYPlotConfig<DateTime> plot( ) {
        Log.Debug( $"==== {nameof(CoverageHistoryPlotter)}.{nameof(plot)} ====" );
        var asmKeyedData = _dtAssemblyClasses
                           .SelectMany( kv => Enumerable.Repeat( kv.Key, kv.Value.Count )
                                                        .Zip( kv.Value, ( a, b ) => ( time: a, asm: b.Key, coverages: b.Value, b.Value.Count ) ) )
                           .GroupBy( tasm => tasm.asm )
                           .ToDictionary(
                               g => g.Key,
                               g => g.ToDictionary(
                                   tp => tp.time,
                                   tp => new CoverageDate( tp.coverages )
                               )
                           );

        foreach ( var asmData in asmKeyedData ) {
            foreach ( var dtA in asmData.Value ) {
                Log.Debug( "  =>" + dtA );
            }
        }
        // TODO: FUTURE: Output coverable lines as alternative measure to FileLineCounter
        var plt = new XYPlotConfig<DateTime>(
            Title: "Coverage",
            OutputFileName: CoverageHistoryPlotter.CHART_OUTPUT_PATH,
            PlotType.Scatter,
            Width: 1200,
            Height: 1200,
            XAxisType: AxisType.DateTime,
            YAxisType: AxisType.Percent,
            Data: asmKeyedData.Select(
                ad => new XYData<DateTime>(
                    Title: ad.Key,
                    X: ad.Value.Select( cd => cd.Key ).ToArray(),
                    Y: ad.Value.Select( cd => Math.Round( ( double )cd.Value.Total.CoveredLines / cd.Value.Total.CoverableLines, digits: 2 ) ).ToArray()
                )
            ).ToArray()
        );
        Log.Debug( "== Plot ==\n" + plt );
        return plt;
    }
}

// ReSharper disable once NotAccessedPositionalProperty.Global
public record CoverageDate( IList<Coverage> Classes ) {
    public Coverage Total { get; } = new Coverage {
        Name                = "Total for Assembly",
        CoveredLines        = Classes.Sum( c => c.CoveredLines ),
        CoverableLines      = Classes.Sum( c => c.CoverableLines ),
        TotalLines          = Classes.Sum( c => c.TotalLines ),
        CoveredBranches     = Classes.Sum( c => c.CoveredBranches ),
        TotalBranches       = Classes.Sum( c => c.TotalBranches ),
        CoveredCodeElements = Classes.Sum( c => c.CoveredCodeElements ),
        TotalCodeElements   = Classes.Sum( c => c.TotalCodeElements )
    };
}

[ XmlRoot( "class" ) ]
public record Coverage {
    [ XmlAttribute( AttributeName = "name" ) ]
    public required string Name { get; set; }

    [ XmlAttribute( AttributeName = "coveredlines" ) ]
    public required int CoveredLines { get; set; }

    [ XmlAttribute( AttributeName = "coverablelines" ) ]
    public required int CoverableLines { get; set; }

    [ XmlAttribute( AttributeName = "totallines" ) ]
    public required int TotalLines { get; set; }

    [ XmlAttribute( AttributeName = "coveredbranches" ) ]
    public required int CoveredBranches { get; set; }

    [ XmlAttribute( AttributeName = "totalbranches" ) ]
    public required int TotalBranches { get; set; }

    [ XmlAttribute( AttributeName = "coveredcodeelements" ) ]
    public required int CoveredCodeElements { get; set; }

    [ XmlAttribute( AttributeName = "totalcodeelements" ) ]
    public required int TotalCodeElements { get; set; }
}