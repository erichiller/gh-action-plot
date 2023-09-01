using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using FluentAssertions;

using Xunit;

namespace PlotGitHubAction.Tests;


/*
 * Shell scripts
 */

public class TodoShellRegexTests {
    const string    filePath   = @"./data/Test.sh";
    
    int             totalFound = 0;
    HashSet<string> hs         = new ();
    List<SourceText> result;
    
    public TodoShellRegexTests(){
        result = TodoScanner.ScanFileForTodos( filePath, ref totalFound, hs );
    }

    SourceText[] expected = new SourceText[] {
        new ( // [0] 1
            Text: @"this is the first TODO",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 4, Column: 3 ),
            End: new CharPosition( Line: 4, Column: 30 )
        ),
        new ( // [1] 2
            Text: @"2nd TODO",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 8, Column: 3 ),
            End: new CharPosition( Line: 8, Column: 16 )
        ),
        new ( // [2] 3
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 10, Column: 3 ),
            End: new CharPosition( Line: 10, Column: 6 )
        ),
        new ( // [3] 4
            Text: @"4th",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 12, Column: 3 ),
            End: new CharPosition( Line: 12, Column: 11 )
        ),
        new ( // [4] 5
            Text: String.Empty,
            Level: @"URGENT",
            FilePath: filePath,
            Start: new CharPosition( Line: 12, Column: 15 ),
            End: new CharPosition( Line: 12, Column: 20 )
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
}