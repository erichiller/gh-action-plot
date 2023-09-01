using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using FluentAssertions;

using Xunit;

namespace PlotGitHubAction.Tests;

/*
 * Python
 */
public class TodoPythonRegexTests {
    const string    filePath   = @"./data/Test.py";
    
    int             totalFound = 0;
    HashSet<string> hs         = new ();
    List<SourceText> result;
    
    public TodoPythonRegexTests(){
        result = TodoScanner.ScanFileForTodos( filePath, ref totalFound, hs );
    }


    SourceText[] expected = new SourceText[] {
        new ( // [0] 1
            Text: @"number 1, single line",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 2, Column: 3 ),
            End: new CharPosition( Line: 2, Column: 29 )
        ),
        new ( // [1] 2
            Text: @"number 2",
            Level: @"URGENT",
            FilePath: filePath,
            Start: new CharPosition( Line: 4, Column: 3 ),
            End: new CharPosition( Line: 4, Column: 18 )
        ),
        new ( // [2] 3
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 8, Column: 3 ),
            End: new CharPosition( Line: 8, Column: 6 )
        ),
        new ( // [3] 4
            Text: @"number 4, multi line" + "\n"
                + @"line 4, 2" + "\n"
                + @"line 4, 3",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 11, Column: 1 ),
            End: new CharPosition( Line: 13, Column: 9 )
        ),
        new ( // [4] 5
            Text: @"number 5, SINGLE LINE",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 17, Column: 1 ),
            End: new CharPosition( Line: 17, Column: 27 )
        ),
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