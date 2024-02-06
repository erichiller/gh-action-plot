namespace PlotGitHubAction;

readonly record struct SourceText(
    string       Text,
    string       Level,
    string       FilePath,
    CharPosition Start,
    CharPosition End
) {
    public string FormattedFileLocation( ) =>
        System.IO.Path.GetFileName( FilePath )
        + " " + Start.Line + ":" + Start.Column;

    public string SortableLocationString =>
        System.IO.Path.GetFileName( FilePath )
        + "_" + Start.Line.ToString().PadLeft( 6, '0' )
        + ":" + Start.Column.ToString().PadLeft( 6, '0' );

    public string MarkdownSafeText( ) =>
        Text.Replace( "\n", "<br />" );
}

public readonly record struct CharPosition(
    int Line,
    int Column
);