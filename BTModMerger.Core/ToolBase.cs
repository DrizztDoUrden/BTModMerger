﻿using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.Core.Schema;
using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Core;

public static class ToolBase
{
    public static (int start, int end) FindSubscript(string from, string diffPath)
    {
        var start = from.IndexOf('[');
        if (start < 0) return (-1, -1);
        var end = from.IndexOfAny(['[', ']'], start + 1);

        if (end < 0) throw new InvalidDataException("Missing ']' character in a path subscript operator.");

        var level = from[end] == '[' ? 2 : 0;
        while (level > 0)
        {
            if (end + 1 == from.Length)
                throw new InvalidDataException("Missing ']' character in a path subscript operator.");

            end = from.IndexOfAny(['[', ']'], end + 1);
            level += from[end] == '[' ? 1 : -1;
        }

        if (start + 1 == end)
            throw new InvalidDataException("Empty subscript operator.");

        return (start, end);
    }

    public static string? ExtractSubscript(ref string from, string diffPath)
    {
        var (start, end) = FindSubscript(from, diffPath);
        if (start == -1)
            return null;
        var value = from[(start + 1)..end];
        from = from.Remove(start, end - start + 1);
        return value;
    }

    public static (string? id, int idx) ParseSubscript(string? subscript)
    {
        if (subscript is null)
            return (null, -1);

        if (subscript[0] == '@')
        {
            subscript = subscript[1..];
            return (subscript, -1);
        }

        return (null, int.Parse(subscript));
    }

    public static IEnumerable<XElement> FilterBy(this IEnumerable<XElement> elements, string identifier, BTMetadata metadata)
        => elements.Where(e => e.GetBTIdentifier(metadata) == identifier);

    public static IEnumerable<XElement> FilterBy(this IEnumerable<XElement> elements, int index)
        => elements.Skip(index).Take(1);

    public static IEnumerable<XElement> FilterBySubscript(this IEnumerable<XElement> elements, string? subscript, BTMetadata metadata)
    {
        var (id, idx) = ParseSubscript(subscript);
        return elements.FilterBy((id, idx), metadata);
    }

    public static (string? ss0, string? ss1) ExtractSubscripts(ref string from, string diffPath)
    {
        return (
            ExtractSubscript(ref from, diffPath),
            ExtractSubscript(ref from, diffPath)
        );
    }

    public static IEnumerable<XElement> FilterBy(this IEnumerable<XElement> elements, (string? ss0, string? ss1) subscripts, BTMetadata metadata)
    {
        elements = elements.FilterBySubscript(subscripts.ss0, metadata);
        elements = elements.FilterBySubscript(subscripts.ss1, metadata);
        return elements;
    }

    public static IEnumerable<XElement> FilterBy(this IEnumerable<XElement> elements, (string? id, int idx) subscripts, BTMetadata metadata)
    {
        if (subscripts.id is not null) elements = elements.FilterBy(subscripts.id, metadata);
        if (subscripts.idx != -1) elements = elements.FilterBy(subscripts.idx);
        return elements;
    }

    public static string[] SplitPath(string path)
        => path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string Fancify(this XName name)
    {
        return NamespaceAliases.TryGetValue(name.Namespace, out var alias)
            ? $"{alias}:{name.LocalName}"
            : name.ToString();
    }

    public static string CombineBTMMPaths(params string?[] paths)
        => string.Join('/', paths.Where(p => !string.IsNullOrEmpty(p)));

    public static string CombineBTMMPaths(string? path, XName newPart)
        => CombineBTMMPaths(path, newPart.Fancify());
}
