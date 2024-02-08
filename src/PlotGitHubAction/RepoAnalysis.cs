using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlotGitHubAction;

public static class RepoAnalysis {
    public static void Run( ActionConfig config ) {
        StringBuilder readme = new StringBuilder();

        StringBuilder links = new StringBuilder();

        if ( config.TestResultsDir is { } ) {
            var testResultAnalyzer = new TestResultAnalyzer( config );
            testResultAnalyzer.ScanForTrx();
            links.AppendLine( $"- [Test Results]({relPath( testResultAnalyzer.MarkdownSummaryFilePath )})" );
            readme.Append( Regex.Replace( testResultAnalyzer.Sb.ToString(), "^#", "##" ) );
            // Write SVG test result badge
            File.WriteAllText( Path.Combine( config.OutputDir, "test_result_badge.svg" ), testResultAnalyzer.GenerateSvgBadge() );
        }

        if ( config is { IsCoverageHistoryEnabled: true, CoverageHistoryDir: { }, CoverageSummaryDir: { } } ) {
            var coverageHistoryPlotter = new CoverageHistoryPlotter( config.CoverageHistoryDir );
            var coveragePlot           = coverageHistoryPlotter.ScanAndPlot();
            PlotGen.CreatePlot(
                JsonSerializer.Serialize( coveragePlot, Utils.SERIALIZER_OPTIONS ),
                config.PlotOutputDir );
            if ( Path.Combine( config.CoverageSummaryDir, "SummaryGithub.md" ) is { } coverageGitHubSummaryPath
                 && File.Exists( coverageGitHubSummaryPath ) ) {
                links.AppendLine( $"- [Coverage Summary]({relPath( coverageGitHubSummaryPath )})" );
                /* Add charts to SummaryGithub.md */
                string coverageSummaryMdTxt = File.ReadAllText( coverageGitHubSummaryPath );
                string newText = $"""

                                  {config.GetMarkdownChartLink( sourcePath: config.CoverageSummaryDir, fileName: CoverageHistoryPlotter.TOTAL_CHART_OUTPUT_PATH )}

                                  {config.GetMarkdownChartLink( sourcePath: config.CoverageSummaryDir, fileName: CoverageHistoryPlotter.TOTAL_RECENT_CHART_OUTPUT_PATH )}

                                  {config.GetMarkdownChartLink( sourcePath: config.CoverageSummaryDir, fileName: CoverageHistoryPlotter.LINE_TOTAL_RECENT_CHART_OUTPUT_PATH )}

                                  {config.GetMarkdownChartLink( sourcePath: config.CoverageSummaryDir, fileName: CoverageHistoryPlotter.COVERABLE_CHART_OUTPUT_PATH )}

                                  {config.GetMarkdownChartLink( sourcePath: config.CoverageSummaryDir, fileName: CoverageHistoryPlotter.ASSEMBLY_CHART_OUTPUT_PATH )}


                                  ## Coverage
                                  """;
                if ( !coverageSummaryMdTxt.Contains( newText ) ) {
                    coverageSummaryMdTxt = Regex.Replace(
                        coverageSummaryMdTxt,
                        "## Coverage",
                        newText );
                    File.WriteAllText( coverageGitHubSummaryPath, coverageSummaryMdTxt );
                }
            }
            readme.AppendLine( "\n## Coverage\n\n" );
            readme.AppendLine( config.GetMarkdownChartLink( CoverageHistoryPlotter.TOTAL_RECENT_CHART_OUTPUT_PATH ) );
        }

        if ( config.IsTodoScanEnabled ) {
            var todoScanner = new TodoScanner( config );
            todoScanner.ScanForTodos();
            PlotGen.CreatePlot(
                JsonSerializer.Serialize( todoScanner.GetPlottable(), Utils.SERIALIZER_OPTIONS ),
                config.PlotOutputDir );
            links.AppendLine( $"- [TO-DOs]({relPath( todoScanner.MarkdownOutputPath )})" );
            readme.AppendLine( "\n## To-Do\n\n" );
            readme.AppendLine( config.GetMarkdownChartLink( TodoScanner.CHART_FILENAME_TOTAL ) );
        }

        if ( config.SourceScanDir is { } ) {
            var lineCounter = new FileLineCounter( config );
            lineCounter.Analyze();
            XYPlotConfig<DateTime>[] lineCountPlottable = lineCounter.GetPlottable();
            PlotGen.CreatePlot(
                JsonSerializer.Serialize( lineCountPlottable, Utils.SERIALIZER_OPTIONS ),
                config.PlotOutputDir );
            links.AppendLine( $"- [Line Counts]({relPath( lineCounter.MarkdownOutputPath )})" );
            readme.AppendLine( "\n## Line Counts\n\n" );
            readme.AppendLine( config.GetMarkdownChartLink( FileLineCounter.LINE_COUNT_HISTORY_TOTAL_FILENAME ) );


            var buildLogAnalyzer = new BuildLogAnalyzer( config );
            buildLogAnalyzer.Analyze();
            PlotGen.CreatePlot(
                JsonSerializer.Serialize( buildLogAnalyzer.GetPlottable(), Utils.SERIALIZER_OPTIONS ),
                config.PlotOutputDir );
            links.AppendLine( $"- [Build Warnings]({relPath( buildLogAnalyzer.MarkdownPath )})" );
            readme.AppendLine( "\n## Build Warnings\n\n" );
            readme.AppendLine( config.GetMarkdownChartLink( BuildLogAnalyzer.BUILD_WARNINGS_TOTAL_RECENT_CHART_FILE_NAME ) );
        }

        links.Insert( 0, "# README\n\n" );
        links.AppendLine();
        links.Append( readme );
        File.WriteAllText(
            Path.Combine( config.OutputDir, "README.md" ),
            links.ToString()
        );

        string relPath( string dest ) =>
            Path.GetRelativePath( config.OutputDir, dest );
    }
}