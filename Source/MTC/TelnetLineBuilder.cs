/*
Copyright (C) 2005  Remco Mulder

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation; either version 2 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
*/

using System.Text;


namespace MTC;

/// <summary>
/// Accumulates application text across receive chunks, splits it into lines,
/// and fires fully and partially ANSI-stripped lines.
///
/// Extracted from <see cref="TelnetClient.FeedTextLines"/> so the line
/// extraction and ANSI-stripping behavior is testable in isolation — in
/// particular escape sequences split across packet boundaries and prompts
/// delivered without a trailing newline.
/// </summary>
public sealed class TelnetLineBuilder
{
    private readonly StringBuilder _lineBuf = new();

    /// <summary>
    /// Cap on the buffered partial line.  A server stream that never terminates
    /// a line (protocol failure, corrupted data) would otherwise grow the
    /// buffer without bound; the partial is force-emitted at this limit.
    /// </summary>
    private const int MaxPartialLineLength = 131_072;

    /// <summary>Fired for every ANSI-stripped line (and partial prompt line).</summary>
    public event Action<string>? TextLineReceived;

    /// <summary>
    /// Fired for every raw line together with its ANSI-stripped form, including
    /// partial prompt lines. The raw form is IAC-stripped but keeps ANSI codes.
    /// </summary>
    public event Action<string, string>? TextLineAnsiReceived;

    /// <summary>
    /// Feeds a chunk of application data (Latin-1 text, telnet IAC already removed).
    /// Complete lines are emitted immediately; a trailing partial line (prompt)
    /// is held in the buffer and re-emitted as a partial line so prompt surfaces
    /// stay visible between chunks.
    /// </summary>
    /// <param name="data">Raw application bytes</param>
    /// <param name="length">Number of bytes of <paramref name="data"/> to process</param>
    public void Feed(byte[] data, int length)
    {
        string text = Encoding.Latin1.GetString(data, 0, length);
        _lineBuf.Append(text);

        string buf = _lineBuf.ToString();
        int start = 0;

        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i] != '\n')
                continue;

            int lineEnd = i;
            if (lineEnd > start && buf[lineEnd - 1] == '\r')
                lineEnd--;

            string raw = buf[start..lineEnd];
            string stripped = StripLine(raw);
            start = i + 1;
            EmitLine(raw, forced: false);
        }

        // Keep unprocessed remainder; fire it as a partial line (catches prompts)
        string remainder = buf[start..];
        _lineBuf.Clear();
        _lineBuf.Append(remainder);
        if (remainder.Length > MaxPartialLineLength)
        {
            // Pathological stream: force the partial out so the buffer cannot
            // grow without bound.
            _lineBuf.Clear();
            EmitLine(remainder, forced: true);
        }
        else if (remainder.Length > 0)
        {
            EmitLine(remainder, forced: false);
        }
    }

    private void EmitLine(string raw, bool forced)
    {
        string stripped = StripLine(raw);
        if (stripped.Length > 0)
        {
            TextLineAnsiReceived?.Invoke(raw, stripped);
            TextLineReceived?.Invoke(stripped);
        }
    }

    private static string StripLine(string raw)
    {
        // StripTerminalSequences (not a bare CSI regex) so partial prompts never
        // carry incomplete escape fragments downstream to the agent or parser.
        // NormalizeTerminalText applies destructive backspace and removes stray
        // CR characters, matching the final normalized form the agent sees.
        return TWXProxy.Core.AnsiCodes.NormalizeTerminalText(
            TWXProxy.Core.AnsiCodes.StripTerminalSequences(raw));
    }
}
