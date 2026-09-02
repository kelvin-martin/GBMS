using System;
using System.IO;
using Avalonia.Platform;

namespace GBMS.Services;

public sealed class TacticalSymbolResolver
{
    private const string ResourcePrefix = "avares://GBMS/Assets/TacticalSymbols/";

    public static Stream Resolve(string symbolId)
    {
        if (string.IsNullOrWhiteSpace(symbolId))
            throw new ArgumentException("Symbol ID cannot be null or empty.", nameof(symbolId));

        var uri = new Uri($"{ResourcePrefix}{symbolId}.svg");

        // Use AssetLoader static methods directly (no GetDefault)
        if (!AssetLoader.Exists(uri))
        {
            throw new FileNotFoundException(
                $"Tactical symbol '{symbolId}' was not found.",
                uri.ToString());
        }

        return AssetLoader.Open(uri);
    }
}
