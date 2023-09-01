using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using FluentAssertions;

using Xunit;

namespace PlotGitHubAction.Tests;

/*
 * XML
 */
 // URGENT
public class TodoXmlRegexTests {
    const string    filePath   = @"./data/Test.xml";
    
    int             totalFound = 0;
    HashSet<string> hs         = new ();
    List<SourceText> result;
    
    public TodoXmlRegexTests(){
        result = TodoScanner.ScanFileForTodos( filePath, ref totalFound, hs );
    }

    SourceText[] expected = new SourceText[] {
        new ( // [0] 1
            Text: @"single line to do 1",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 3, Column: 13 ),
            End: new CharPosition( Line: 3, Column: 37 )
        ),
        new ( // [1] 2
            Text: @"same line 2, left",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 5, Column: 10 ),
            End: new CharPosition( Line: 5, Column: 32 )
        ),
        new ( // [2] 3
            Text: @"same line 3, right",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 5, Column: 43 ),
            End: new CharPosition( Line: 5, Column: 66 )
        ),
        new ( // [3] 4
            Text: @"multiline 4-1" + "\n"
                + @"         line2 4-2",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 7, Column: 10 ),
            End: new CharPosition( Line: 8, Column: 18 )
        ),
        new ( // [4] 5
            Text: @"multiline 5-1  " + "\n"
                + @"         line2 5-2",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 10, Column: 10 ),
            End: new CharPosition( Line: 11, Column: 18 )
        ),
        new ( // [5] 6
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 16, Column: 10 ),
            End: new CharPosition( Line: 16, Column: 13 )
        ),
        new ( // [6] 7
            Text: String.Empty,
            Level: @"URGENT",
            FilePath: filePath,
            Start: new CharPosition( Line: 18, Column: 10 ),
            End: new CharPosition( Line: 18, Column: 15 )
        ),
        new ( // [7] 8
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 18, Column: 24 ),
            End: new CharPosition( Line: 18, Column: 27 )
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
    
    [ Fact ]
    public void MatchTest6( ) {
        int idx = 6;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest7( ) {
        int idx = 7;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
}
