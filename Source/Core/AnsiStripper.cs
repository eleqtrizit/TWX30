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

namespace TWXProxy.Core
{
    /// <summary>
    /// Streaming ANSI/VT100 sequence stripper implemented as a compact VT500-style
    /// state machine.  Escapes are consumed by construction, so text split across
    /// arbitrary packet/line boundaries never leaks sequence fragments into the
    /// visible output — unlike regex-based stripping, which needs a separate
    /// "incomplete fragment" cleanup for every sequence class.
    ///
    /// Grammar coverage (ECMA-48 / xterm):
    /// - CSI sequences: ESC [ or single-byte C1 0x9B, parameter bytes 0x30-0x3F
    ///   (digits, ;, :, ?), intermediate bytes 0x20-0x2F, final byte 0x40-0x7E.
    /// - String sequences: OSC (ESC ] or 0x9D), DCS (ESC P or 0x90), SOS (ESC X /
    ///   0x98), PM (ESC ^ / 0x9E), APC (ESC _ / 0x9F) — terminated by BEL (OSC
    ///   only) or ST (ESC \ or 0x9C).  An embedded ESC inside a string state is
    ///   treated as the start of the terminator/next sequence, so payloads cannot
    ///   leak.
    /// - Two- and three-character escape sequences (ESC 7, ESC =, ESC ( B).
    /// - CAN (0x18) / SUB (0x1A) abort an in-progress sequence.
    /// - The full C1 zone 0x80-0x9F: sequence introducers are dispatched, other
    ///   C1 bytes are dropped as non-printable.
    ///
    /// C0 policy: CR, LF, backspace and HT pass through untouched (destructive
    /// backspace remains the responsibility of AnsiCodes.NormalizeTerminalText);
    /// BEL and NUL are dropped everywhere.
    ///
    /// Two usage modes:
    /// - <see cref="Strip"/> on an instance: streaming, state persists across
    ///   calls (one instance per connection/chunk stream).
    /// - <see cref="StripAll"/>: one-shot; runs a fresh machine over the whole
    ///   string.  Callers that strip complete lines (or line-builder output that
    ///   retains raw fragment bytes) use this.
    /// </summary>
    public sealed class AnsiStripper
    {
        private enum MachineState
        {
            Ground,
            Escape,
            EscapeIntermediate,
            CsiEntry,
            CsiParam,
            CsiIntermediate,
            CsiIgnore,
            OscString,
            OscEscape,
            StringState,
            StringEscape
        }

        private MachineState _state = MachineState.Ground;

        /// <summary>Resets the machine so the instance can be reused on a new stream.</summary>
        public void Reset() => _state = MachineState.Ground;

        /// <summary>
        /// Feeds a chunk of text and appends the visible characters to
        /// <paramref name="output"/>.  Sequence state persists across calls, so
        /// sequences split across chunks are consumed correctly.
        /// </summary>
        /// <param name="text">Raw text chunk, possibly containing escape sequences</param>
        /// <param name="output">Receives the visible characters from this chunk</param>
        public void Strip(string text, System.Text.StringBuilder output)
        {
            for (int i = 0; i < text.Length; i++)
                Step(text[i], output);
        }

        /// <summary>Streaming variant of <see cref="Strip(string, System.Text.StringBuilder)"/> returning the visible text of this chunk.</summary>
        public string Strip(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var output = new System.Text.StringBuilder(text.Length);
            Strip(text, output);
            return output.ToString();
        }

        /// <summary>One-shot variant: runs a fresh machine over the whole string.</summary>
        public static string StripAll(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var output = new System.Text.StringBuilder(text.Length);
            var stripper = new AnsiStripper();
            stripper.Strip(text, output);
            return output.ToString();
        }

        private void Step(char ch, System.Text.StringBuilder output)
        {
            // CAN / SUB abort any in-progress sequence from any state.
            if (_state != MachineState.Ground && (ch == '\x18' || ch == '\x1A'))
            {
                _state = MachineState.Ground;
                return;
            }

            switch (_state)
            {
                case MachineState.Ground:
                    StepGround(ch, output);
                    break;
                case MachineState.Escape:
                    StepEscape(ch);
                    break;
                case MachineState.EscapeIntermediate:
                    StepEscapeIntermediate(ch);
                    break;
                case MachineState.CsiEntry:
                case MachineState.CsiParam:
                case MachineState.CsiIntermediate:
                case MachineState.CsiIgnore:
                    StepCsi(ch);
                    break;
                case MachineState.OscString:
                    StepOsc(ch);
                    break;
                case MachineState.OscEscape:
                    StepOscEscape(ch);
                    break;
                case MachineState.StringState:
                    StepString(ch);
                    break;
                case MachineState.StringEscape:
                    StepStringEscape(ch);
                    break;
            }
        }

        private void StepGround(char ch, System.Text.StringBuilder output)
        {
            switch (ch)
            {
                case '\x1B':
                    _state = MachineState.Escape;
                    break;
                case '\x18':  // CAN
                case '\x1A':  // SUB
                    _state = MachineState.Ground;
                    break;
                case '\x9B':
                    _state = MachineState.CsiEntry;
                    break;
                case '\x9D':
                    _state = MachineState.OscString;
                    break;
                case '\x90':
                case '\x98':
                case '\x9E':
                case '\x9F':
                    _state = MachineState.StringState;
                    break;
                case '\x07':  // BEL dropped in ground state
                case '\x00':  // NUL dropped
                    break;
                default:
                    // Drop the rest of the C1 zone (0x80-0x9F); nothing printable
                    // lives there.  Pass everything else through.
                    if (ch < '\x80' || ch > '\x9F')
                        output.Append(ch);
                    break;
            }
        }

        private void StepEscape(char ch)
        {
            switch (ch)
            {
                case '[':
                    _state = MachineState.CsiEntry;
                    break;
                case ']':
                    _state = MachineState.OscString;
                    break;
                case 'P':
                case 'X':
                case '^':
                case '_':
                    _state = MachineState.StringState;
                    break;
                case '\x18':  // CAN
                case '\x1A':  // SUB
                    _state = MachineState.Ground;
                    break;
                default:
                    if (ch is >= '\x20' and <= '\x2F')
                        _state = MachineState.EscapeIntermediate;
                    else if (ch == '\x9C')
                        _state = MachineState.Ground;
                    else
                        _state = MachineState.Ground;  // two-char final (ESC 7, ESC =, ...)
                    break;
            }
        }

        private void StepEscapeIntermediate(char ch)
        {
            if (ch is >= '\x20' and <= '\x2F')
                return;  // more intermediates

            _state = MachineState.Ground;  // final byte consumed (any byte, incl. 0x9C)
        }

        private void StepCsi(char ch)
        {
            switch (_state)
            {
                case MachineState.CsiEntry:
                    if (ch is >= '\x30' and <= '\x3F')
                        _state = MachineState.CsiParam;
                    else if (ch is >= '\x20' and <= '\x2F')
                        _state = MachineState.CsiIntermediate;
                    else if (ch is >= '\x40' and <= '\x7E')
                        _state = MachineState.Ground;
                    else
                        _state = MachineState.CsiIgnore;
                    break;
                case MachineState.CsiParam:
                    if (ch is >= '\x30' and <= '\x3F')
                        return;  // more parameters
                    if (ch is >= '\x20' and <= '\x2F')
                        _state = MachineState.CsiIntermediate;
                    else if (ch is >= '\x40' and <= '\x7E')
                        _state = MachineState.Ground;
                    else
                        _state = MachineState.CsiIgnore;
                    break;
                case MachineState.CsiIntermediate:
                    if (ch is >= '\x20' and <= '\x2F')
                        return;  // more intermediates
                    if (ch is >= '\x40' and <= '\x7E')
                        _state = MachineState.Ground;
                    else
                        _state = MachineState.CsiIgnore;
                    break;
                default:  // CsiIgnore
                    if (ch is >= '\x40' and <= '\x7E' || ch == '\x9C')
                        _state = MachineState.Ground;
                    break;
            }
        }

        private void StepOsc(char ch)
        {
            switch (ch)
            {
                case '\x07':  // BEL terminates OSC
                    _state = MachineState.Ground;
                    break;
                case '\x1B':
                    _state = MachineState.OscEscape;
                    break;
                case '\x9C':  // C1 ST terminates OSC
                    _state = MachineState.Ground;
                    break;
            }
        }

        private void StepOscEscape(char ch)
        {
            if (ch == '\\')
            {
                _state = MachineState.Ground;  // ESC \ = ST
                return;
            }

            // Not a terminator: per xterm the byte belongs to the OSC payload; the
            // machine returns to the string state so the payload cannot leak.
            _state = MachineState.OscString;
        }

        private void StepString(char ch)
        {
            switch (ch)
            {
                case '\x1B':
                    _state = MachineState.StringEscape;
                    break;
                case '\x9C':
                    _state = MachineState.Ground;
                    break;
            }
        }

        private void StepStringEscape(char ch)
        {
            if (ch == '\\')
            {
                _state = MachineState.Ground;  // ESC \ = ST
                return;
            }

            // Not a terminator: the byte belongs to the string payload.
            _state = MachineState.StringState;
        }
    }
}
