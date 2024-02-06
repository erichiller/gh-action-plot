using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlotGitHubAction;

public class UrlMdShortUtils {
    private readonly Dictionary<string, string> _urlMap   = new (StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _usedUrls = new (StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _idStrToGenerated = new (StringComparer.OrdinalIgnoreCase);
    private readonly bool                       _generateIds;
    private readonly ActionConfig?              _config;

    public UrlMdShortUtils( ActionConfig? config = null, bool generateIds = false ) {
        _generateIds = generateIds;
        _config      = config;
    }

    public string Add( string id, string url, bool isCode = false ) {
        string mdModifier = isCode ? "`" : String.Empty;
        if ( _generateIds ) {
            //
            if ( !_idStrToGenerated.TryGetValue( id, out string? generatedId ) ) {
                // generatedId = $"{_generatedIdPrefix}{_idSeq++}";
                generatedId = toBase36( Utils.GetDeterministicHashCode( url ) );
                _urlMap.TryAdd( generatedId, url );
                _usedUrls.TryAdd( generatedId, url );
                _idStrToGenerated.TryAdd( id, generatedId );
            }

            return $"{mdModifier}[{id}][{generatedId}]{mdModifier}";
        }
        _urlMap.TryAdd( id, url );
        _usedUrls.TryAdd( id, url );
        return $"{mdModifier}[{id}]{mdModifier}";
    }

    public string GetFormattedLink( string id, bool isCode = false ) {
        string mdModifier = isCode ? "`" : String.Empty;
        if ( _generateIds ) {
            if ( _idStrToGenerated.TryGetValue( id, out string? generatedId ) ) {
                return $"{mdModifier}[{id}][{generatedId}]{mdModifier}";
            }
        } else if ( _urlMap.ContainsKey( id ) ) {
            return $"{mdModifier}[{id}]{mdModifier}";
        }
        return $"{mdModifier}{id}{mdModifier}";
    }

    public string AddSourceLink( string filePath, CharPosition? start = null, CharPosition? end = null, bool linkToBranch = false ) {
        if ( this._config is not { } config ) {
            throw new NullReferenceException( nameof(_config) );
        }
        string linkTitle = config.GetFormattedSourcePosition( filePath, start, end );
        if ( config.GetGitHubSourceLink( filePath, start, end, linkToBranch ) is not { } url ) {
            if ( !filePath.Contains( "Microsoft.NET.Sdk" ) ) {
                Log.Warn( $"Unable to create source link for {linkTitle}" );
            }
            return linkTitle;
        }
        // KILL
        try {
            Log.Debug( $"[{filePath}] [{start}] [{end}]: {new Uri( url ).AbsoluteUri}" );
        } catch ( System.UriFormatException e ) {
            Log.Error( $"Uri format failed for [{filePath}] [{start}] [{end}]: '{url}': {e.Message}" );
            throw;
        }
        return this.Add( linkTitle, url );
    }

    public void AddReferencedUrls( StringBuilder sb ) {
        foreach ( var (id, url) in _usedUrls.OrderByDescending( kv => kv.Key ) ) {
            try {
                sb.AppendLine( $"[{id}]: {new Uri( url ).AbsoluteUri}" );
            } catch ( System.UriFormatException e ) {
                Log.Error( $"Uri format failed for id '{id}': '{url}': {e.Message}" );
                throw;
            }
        }
    }


    private static string toBase36( uint h ) {
        uint         b     = 36;
        string       s     = String.Empty;
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        while ( h > 0 ) {
            s += chars[ ( int )( h % b ) ];
            h =  h / b;
        }
        return s.PadLeft( 7, '0' );
    }
}