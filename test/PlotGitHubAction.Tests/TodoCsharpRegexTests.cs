using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using FluentAssertions;

using Xunit;

namespace PlotGitHubAction.Tests;

public class TodoCsharpRegexTests {
    const string    filePath   = @"./data/Test.cs";
    int             totalFound = 0;
    HashSet<string> hs         = new ();
    List<SourceText> result;
    
    public TodoCsharpRegexTests(){
        // System.Console.WriteLine( "Test.sql exists?" + System.IO.File.Exists( @"./data/Test.sql" ).ToString() );
        // System.Console.WriteLine( "Test.cs exists?" + System.IO.File.Exists( @"./data/Test.cs" ).ToString() );
        result = TodoScanner.ScanFileForTodos( filePath, ref totalFound, hs );
    }


    SourceText[] expected = new SourceText[] {
        new ( // [0] 1
            Text: @"this thing",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 3, Column: 8 ),
            End: new CharPosition( Line: 3, Column: 23 )
        ),
        new ( // [1] 2
            Text: @"MultiLine TODO with first line empty" + "\n"
                + @"     * Second line" + "\n"
                + @"     *       Third Line" + "\n"
                + @"     *",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 6, Column: 8 ),
            End: new CharPosition( Line: 9, Column: 6 )
        ),
        new ( // [2] 3
            Text: @"Multiline TODO with occupied first line" + '\n'
                + @"     * A second line" + "\n"
                + @"     * And a third line",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 12, Column: 8 ),
            End: new CharPosition( Line: 14, Column: 23 )
        ),
        new ( // [3] 4
            Text: @"NUMBER 2",
            Level: @"URGENT",
            FilePath: filePath,
            Start: new CharPosition( Line: 15, Column: 12 ),
            End: new CharPosition( Line: 15, Column: 27 )
        ),
        new ( // [4] 5
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 16, Column: 6 ),
            End: new CharPosition( Line: 16, Column: 9 )
        ),
        new ( // [5] 6
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 17, Column: 6 ),
            End: new CharPosition( Line: 17, Column: 9 )
        ),
        new ( // [6] 7
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 17, Column: 17 ),
            End: new CharPosition( Line: 17, Column: 20 )
        ),
        new ( // [7] 8
            Text: String.Empty,
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 19, Column: 6 ),
            End: new CharPosition( Line: 19, Column: 9 )
        ),
        new ( // [8] 9
            Text: String.Empty,
            Level: @"URGENT",
            FilePath: filePath,
            Start: new CharPosition( Line: 19, Column: 15 ),
            End: new CharPosition( Line: 19, Column: 20 )
        ),
        new ( // [9] 1p
            Text: "without a semicolon",
            Level: @"TODO",
            FilePath: filePath,
            Start: new CharPosition( Line: 21, Column: 6 ),
            End: new CharPosition( Line: 21, Column: 29 )
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
        
        
        var s = result.Skip( idx ).First();
        var x = expected[ idx ];
        var rl = s.Text.Split( '\n' );
        var el = x.Text.Split( '\n' );
        rl.Length.Should().Be( el.Length );
        rl[0].Length.Should().Be( el[0].Length );
        rl[1].Length.Should().Be( el[1].Length );
        rl[2].Length.Should().Be( el[2].Length );
        
        s.Text.Length.Should().Be( x.Text.Length );
        s.Should().Be( x );
        
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
    
    [ Fact ]
    public void MatchTest2( ) {
        int idx = 2;
        var s = result.Skip( idx ).First();
        var x = expected[ idx ];
        var rl = s.Text.Split( '\n' );
        var el = x.Text.Split( '\n' );
        rl.Length.Should().Be( el.Length );
        rl[0].Length.Should().Be( el[0].Length );
        rl[1].Length.Should().Be( el[1].Length );
        rl[2].Length.Should().Be( el[2].Length );
        
        s.Text.Length.Should().Be( x.Text.Length );
        s.Should().Be( x );
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
    
    [ Fact ]
    public void MatchTest8( ) {
        int idx = 8;
        result.Skip( idx ).First().Should()
              .Be( expected[ idx ] );
    }
}
