using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using FluentAssertions;

using Xunit;

namespace PlotGitHubAction.Tests;

/*
 * PowerShell scripts
 */
public class TodoPowerShellRegexTests {
    const string    filePath   = @"./data/Test.ps1";
    
    int             totalFound = 0;
    HashSet<string> hs         = new ();
    List<SourceText> result;
    
    public TodoPowerShellRegexTests(){
        result = TodoScanner.ScanFileForTodos( filePath, ref totalFound, hs );
    }

    SourceText[] expected = new SourceText[] {
        new ( // [0] 1
            Text: @"#1 this is a TODO",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 5, Column: 3 ),
            End: new CharPosition( Line: 5, Column: 25 )
        ),
        new ( // [1] 2
            Text: @"#2 this is a multiline TODO" + "\n" +
                  @"   SECOND LINE HERE for #2",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 9, Column: 4 ),
            End: new CharPosition( Line: 10, Column: 26 )
        ),
        new ( // [2] 3
            Text: @"#3 this is a multiline TODO" + "\n" +
                  @" # SECOND LINE HERE for #3",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 14, Column: 4 ),
            End: new CharPosition( Line: 15, Column: 26 )
        ),
        new ( // [3] 4
            Text: "#4 this is a multiline TODO" + "\n" +
                  " SECOND LINE HERE for #4",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 19, Column: 2 ),
            End: new CharPosition( Line: 20, Column: 24 )
        ),
        
        new ( // [4] 5
            Text: "5 same line, first",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 23, Column: 4 ),
            End: new CharPosition( Line: 23, Column: 27 )
        ),
        new ( // [5] 6
            Text: "6 same line, second",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 23, Column: 35 ),
            End: new CharPosition( Line: 23, Column: 59 )
        ),
        new ( // [6] 7
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 25, Column: 4 ),
            End: new CharPosition( Line: 25, Column: 7 )
        ),
        new ( // [7] 8
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 27, Column: 3 ),
            End: new CharPosition( Line: 27, Column: 6 )
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
        var localResult = result.Skip( idx ).First();
        localResult.Text.Length.Should().Be( expected[ idx ].Text.Length );
        localResult.Should().BeEquivalentTo( expected[ idx ] );
        localResult.Should().Be( expected[ idx ] );
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