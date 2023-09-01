using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using FluentAssertions;

using Xunit;

namespace PlotGitHubAction.Tests;


/*
 * SQL
 */
public class TodoSqlRegexTests {
    const string    filePath   = @"./data/Test.sql";
    
    int             totalFound = 0;
    HashSet<string> hs         = new ();
    List<SourceText> result;
    
    public TodoSqlRegexTests(){
        result = TodoScanner.ScanFileForTodos( filePath, ref totalFound, hs );
    }

    SourceText[] expected = new SourceText[] {
        new ( // [0] 1
            Text: @"SQL TODO number 1",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 3, Column: 4 ),
            End: new CharPosition( Line: 3, Column: 26 )
        ),
        new ( // [1] 2
            Text: @"TODO number 2 ; Multiline TODO with occupied first line" + "\n" +
                  @" * A second line" + "\n" +
                  @" * And a third line",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 5, Column: 4 ),
            End: new CharPosition( Line: 7, Column: 19 )
        ),
        new ( // [2] 3
            Text: @"content for #3",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 10, Column: 4 ),
            End: new CharPosition( Line: 10, Column: 23 )
        ),
        new ( // [3] 4
            Text: "has trailing space content for #4 \n" +
                  "  line 2 #4",
            Level: @"URGENT",
            FilePath: filePath,
            Start: new CharPosition( Line: 10, Column: 31 ),
            End: new CharPosition( Line: 11, Column: 11 )
        ),
        new ( // [4] 5
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 14, Column: 4 ),
            End: new CharPosition( Line: 14, Column: 7 )
        ),
        new ( // [5] 6
            Text: @"content for #6",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 16, Column: 4 ),
            End: new CharPosition( Line: 16, Column: 23 )
        ),
        new ( // [6] 7
            Text: "content for #7" + "\n" +
                  "  line 2 #7",
            Level: @"URGENT",
            FilePath: filePath,
            Start: new CharPosition( Line: 16, Column: 31 ),
            End: new CharPosition( Line: 17, Column: 11 )
        ),
        new ( // [7] 8
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 20, Column: 4 ),
            End: new CharPosition( Line: 20, Column: 7 )
        ),
        new ( // [8] 9
            Text: String.Empty,
            Level: @"URGENT",
            FilePath: filePath,
            Start: new CharPosition( Line: 20, Column: 15 ),
            End: new CharPosition( Line: 20, Column: 20 )
        ),
        new ( // [9] 10
            Text: @"SQL TODO number 10",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 22, Column: 4 ),
            End: new CharPosition( Line: 22, Column: 27 )
        ),
        new ( // [10] 11
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 24, Column: 4 ),
            End: new CharPosition( Line: 24, Column: 7 )
        ),
        new ( // [11] 12
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 26, Column: 4 ),
            End: new CharPosition( Line: 26, Column: 7 )
        ),
        new ( // [12] 13
            Text: String.Empty,
            Level: @"URGENT",
            FilePath: filePath,
            Start: new CharPosition( Line: 26, Column: 12 ),
            End: new CharPosition( Line: 26, Column: 17 )
        ),
        new ( // [13] 14
            Text: @"TEXT for #14",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 28, Column: 4 ),
            End: new CharPosition( Line: 28, Column: 21 )
        ),
        new ( // [14] 15
            Text: @"CONTENT for #15",
            Level: @"URGENT",
            FilePath: filePath,
            Start: new CharPosition( Line: 28, Column: 26 ),
            End: new CharPosition( Line: 28, Column: 48 )
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
        var singleResult = result.Skip( idx ).First();
        singleResult.Text.Length.Should().Be( expected[ idx ].Text.Length );
        singleResult.Should().BeEquivalentTo( expected[ idx ] );
        // result.Should().Equal( expected[ idx ] );
        singleResult.Should().Be( expected[ idx ] );
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
    
    [ Fact ]
    public void MatchTest8( ) {
        int idx = 8;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest9( ) {
        int idx = 9;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest10( ) {
        int idx = 10;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest11( ) {
        int idx = 11;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest12( ) {
        int idx = 12;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest13( ) {
        int idx = 13;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    
    [ Fact ]
    public void MatchTest14( ) {
        int idx = 14;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
}