using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using FluentAssertions;

using Xunit;

namespace PlotGitHubAction.Tests;


/*
 * Markdown
 */
public class TodoMarkdownRegexTests {
    const string    filePath   = @"./data/Test.md";
    
    int             totalFound = 0;
    HashSet<string> hs         = new ();
    List<SourceText> result;
    
    public TodoMarkdownRegexTests(){
        result = TodoScanner.ScanFileForTodos( filePath, ref totalFound, hs );
    }


    SourceText[] expected = new SourceText[] {
        new ( // [0] 1
            Text: @"Todo #1",
            Level: String.Empty,
            FilePath: filePath,
            Start: new CharPosition( Line: 3, Column: 7 ),
            End: new CharPosition( Line: 3, Column: 13 )
        ),
        new ( // [1] 2
            Text: @"Todo #2",
            Level: String.Empty,
            FilePath: filePath,
            Start: new CharPosition( Line: 4, Column: 7 ),
            End: new CharPosition( Line: 4, Column: 13 )
        ),
        new ( // [2] 3
            Text: @"Todo #2-A",
            Level: String.Empty,
            FilePath: filePath,
            Start: new CharPosition( Line: 5, Column: 11 ),
            End: new CharPosition( Line: 5, Column: 19 )
        ),
        new ( // [3] 4
            Text: "Todo #2-B" + "\n" +
                  "      second line",
            Level: String.Empty,
            FilePath: filePath,
            Start: new CharPosition( Line: 6, Column: 11 ),
            End: new CharPosition( Line: 7, Column: 17 )
        ),
        new ( // [4] 5
            Text: @"Todo #3",
            Level: String.Empty,
            FilePath: filePath,
            Start: new CharPosition( Line: 9, Column: 7 ),
            End: new CharPosition( Line: 9, Column: 13 )
        ),
        new ( // [5] 6
            Text: @"Todo #4",
            Level: String.Empty,
            FilePath: filePath,
            Start: new CharPosition( Line: 13, Column: 7 ),
            End: new CharPosition( Line: 13, Column: 13 )
        )
    };
    
    
    [ Fact ]
    public void TotalsTest( ) {
        result.Should().HaveCount( expected.Length );
        totalFound.Should().Be( expected.Length );
    }
    
    [ Fact ]
    public void MatchTest0( ) {
        int idx = 0;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest1( ) {
        int idx = 1;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest2( ) {
        int idx = 2;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest3( ) {
        int idx = 3;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest4( ) {
        int idx = 4;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest5( ) {
        int idx = 5;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    //...
}