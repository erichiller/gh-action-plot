using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json.Serialization;

namespace PlotGitHubAction;

public class CsProjInfo : INamedObject, IEquatable<CsProjInfo> {
    [ SuppressMessage( "ReSharper", "MemberCanBePrivate.Global" ) ]
    public GitRepoInfo GitRepo { get; init; }

    [ JsonConstructor ]
    public CsProjInfo( string filePath, GitRepoInfo gitRepo ) {
        GitRepo                   = gitRepo;
        FilePath                  = filePath;
        DirectoryPath             = System.IO.Path.GetDirectoryName( filePath ) ?? throw new ArgumentException( $"Unable to determine directory name of {filePath}" );
        ProjectName               = System.IO.Path.GetFileNameWithoutExtension( filePath );
        RepoRelativePath          = Path.GetRelativePath( gitRepo.RootDir.FullName, filePath );
        RepoRelativeDirectoryPath = Path.GetDirectoryName( this.RepoRelativePath ) ?? throw new NullReferenceException();
    }

    public CsProjInfo( CsProjInfo toClone ) {
        FilePath                  = toClone.FilePath;
        DirectoryPath             = toClone.DirectoryPath;
        ProjectName               = toClone.ProjectName;
        GitRepo                   = toClone.GitRepo;
        RepoRelativePath          = toClone.RepoRelativePath;
        RepoRelativeDirectoryPath = Path.GetDirectoryName( this.RepoRelativePath ) ?? throw new NullReferenceException();
    }

    public string ProjectName   { get; }
    public string Name          => ProjectName;
    public string DirectoryPath { get; }
    [ SuppressMessage( "ReSharper", "MemberCanBePrivate.Global" ) ]
    public string RepoRelativePath { get; }
    public string RepoRelativeDirectoryPath { get; }
    public string FilePath                  { get; }
    public string MarkdownId                => ProjectName.Replace( '.', '-' );

    public bool ContainsFile( string filePath ) =>
        filePath.StartsWith( this.DirectoryPath.TrimEnd( Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar );

    public bool Equals( CsProjInfo? other ) {
        return other?.FilePath == this.FilePath;
    }

    public override bool Equals( object? obj ) {
        return this.Equals( obj as CsProjInfo );
    }

    public static bool operator ==( CsProjInfo? left, CsProjInfo? right ) {
        return Equals( left, right );
    }

    public static bool operator !=( CsProjInfo? left, CsProjInfo? right ) {
        return !Equals( left, right );
    }

    public override int GetHashCode( ) {
        return this.FilePath.GetHashCode();
    }

    public override string ToString( ) =>
        $"CsProjInfo {{ {nameof(ProjectName)} = {ProjectName} {nameof(FilePath)} = {FilePath} }}";
}


public interface INamedObject {
    public string Name { get; }
}