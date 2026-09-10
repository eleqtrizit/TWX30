using System.Text;
using Avalonia.Threading;
using Core = TWXProxy.Core;

namespace MTC;

public partial class MainWindow
{
    private MtcJsonRpcServer? _jsonRpcServer;

    private void ApplyJsonRpcPreferences()
    {
        try
        {
            EmbeddedMtcJsonRpcConfig jsonRpcPrefs = GetCurrentJsonRpcConfig();
            bool shouldEnable = jsonRpcPrefs.Enabled &&
                                HasMtcJsonRpcGameContext(_embeddedGameConfig, _embeddedGameName, _sessionDb, _gameInstance);
            if (MtcStartupFlags.JsonRpcEnabledAtStartup)
                shouldEnable = true;
            if (!shouldEnable && _jsonRpcServer == null)
                return;

            _jsonRpcServer ??= new MtcJsonRpcServer(BuildMtcRpcBridge());
            int configuredPort = MtcStartupFlags.JsonRpcPortOverride ?? AppPreferences.NormalizeJsonRpcPort(jsonRpcPrefs.Port);
            _jsonRpcServer.ApplyOptions(new MtcJsonRpcServerOptions
            {
                Enabled = shouldEnable,
                BindAddress = AppPreferences.NormalizeJsonRpcBindAddress(jsonRpcPrefs.BindAddress),
                Port = configuredPort,
            });
        }
        catch (Exception ex)
        {
            Core.GlobalModules.DebugLog($"[MTC.JsonRpc] failed to apply preferences: {ex}\n");
            _parser.Feed($"\x1b[1;31m[JSON-RPC failed: {ex.Message}]\x1b[0m\r\n");
            _buffer.Dirty = true;
            if (MtcStartupFlags.InStartup)
                FailStartupOnServerConflict(ex.Message);
        }
    }

    /// <summary>
    /// Ends the application when the JSON-RPC listener cannot start during startup, which
    /// means another MTC copy (or another process) already owns the configured port. Announces
    /// a visible countdown so agents watching the stream see the failure before exit.
    /// </summary>
    /// <param name="reason">The bind failure message shown to the player</param>
    private void FailStartupOnServerConflict(string reason)
    {
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            _parser.Feed($"\x1b[1;31m[MTC JSON-RPC] {reason}]\x1b[0m\r\n");
            _parser.Feed("\x1b[1;31m[MTC JSON-RPC] MTC will exit. Countdown started; close this dialog to exit immediately.]\x1b[0m\r\n");
            _buffer.Dirty = true;
            _ = ShowMessageAsync(
                "MTC JSON-RPC",
                $"{reason}\n\nMTC is exiting because the JSON-RPC port is already in use.\n" +
                "MTC exits automatically when the countdown finishes; press OK to exit now.");
            const int countdownSeconds = 10;
            for (int remaining = countdownSeconds; remaining > 0; remaining--)
            {
                _parser.Feed($"\x1b[1;31m[MTC JSON-RPC] Exiting in {remaining} second{(remaining == 1 ? string.Empty : "s")}.]\x1b[0m\r\n");
                _buffer.Dirty = true;
                await Task.Delay(1000).ConfigureAwait(true);
            }

            _parser.Feed("\x1b[1;31m[MTC JSON-RPC] Exiting now.]\x1b[0m\r\n");
            _buffer.Dirty = true;
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        });
    }

    private EmbeddedMtcJsonRpcConfig GetCurrentJsonRpcConfig()
    {
        if (_embeddedGameConfig == null)
            return new EmbeddedMtcJsonRpcConfig();

        _embeddedGameConfig.Mtc ??= new EmbeddedMtcConfig();
        _embeddedGameConfig.Mtc.JsonRpc ??= new EmbeddedMtcJsonRpcConfig();
        return _embeddedGameConfig.Mtc.JsonRpc;
    }

    /// <summary>
    /// Subscribes interpreter runtime script errors into the tab's game-agent event stream,
    /// so failures reach the terminal feed, JSON-RPC subscribers, and MCP SSE clients.
    /// </summary>
    /// <param name="interpreter">The interpreter whose running scripts are observed</param>
    /// <param name="tab">The MTC tab hosting the game session</param>
    private static void WireMtcScriptErrorTelemetry(Core.ModInterpreter interpreter, MtcTabPrototype? tab)
    {
        if (tab == null)
            return;

        interpreter.ScriptError += (scriptName, message) =>
        {
            var evt = new GameAgentEvent
            {
                Kind = GameAgentEventKind.System,
                GameName = tab.Title,
                PlainText = $"[Script error] {message}",
                Metadata = new Dictionary<string, string>
                {
                    ["scriptEvent"] = "error",
                    ["script"] = string.IsNullOrWhiteSpace(scriptName) ? "unknown" : scriptName,
                },
            };
            tab.GameAgent.Record(evt);
        };
    }

    private static bool HasMtcJsonRpcGameContext(        EmbeddedGameConfig? config,
        string? gameName,
        Core.ModDatabase? sessionDb,
        Core.GameInstance? gameInstance)
    {
        if (config != null || sessionDb != null || gameInstance != null)
            return true;

        string normalizedGameName = NormalizeGameName(gameName);
        return !string.IsNullOrWhiteSpace(normalizedGameName) &&
               !IsGeneratedPlaceholderGameName(normalizedGameName);
    }

    private MtcRpcBridge BuildMtcRpcBridge()
        => new()
        {
            GetContextAsync = BuildMtcRpcContextAsync,
            GetRecentEventsAsync = GetMtcRpcRecentEventsAsync,
            QuerySectorAsync = QueryMtcRpcSectorAsync,
            ListScriptsAsync = ListMtcRpcScriptsAsync,
            SendCommandAsync = SendMtcRpcCommandAsync,
            SendAndWaitAsync = SendMtcRpcSendAndWaitAsync,
            GetMombotStatusAsync = GetMtcRpcMombotStatusAsync,
            SendMombotPageAsync = SendMtcRpcMombotPageAsync,
            RunMombotCommandAsync = ExecuteGameAgentMombotCommandAsync,
            RunScriptAsync = RunMtcRpcScriptAsync,
            StopScriptAsync = StopMtcRpcScriptAsync,
            ConnectServerAsync = ConnectMtcRpcServerAsync,
            DisconnectServerAsync = DisconnectMtcRpcServerAsync,
            WriteScriptAsync = WriteMtcRpcScriptFileAsync,
            EditScriptAsync = EditMtcRpcScriptFileAsync,
            ReadScriptAsync = ReadMtcRpcScriptFileAsync,
            CompileScriptAsync = CompileMtcRpcScriptAsync,
        };

    private Task<GameAgentContextSnapshot> BuildMtcRpcContextAsync(int recentEventCount)
        => InvokeMtcRpcUiAsync(() =>
        {
            _gameAgent.SetGameName(GetGameAgentGameName());
            return Task.FromResult(_gameAgent.BuildContextSnapshot(
                _state,
                _sessionDb,
                BuildGameAgentBotSnapshot(),
                BuildGameAgentOnlinePlayersSnapshot(),
                BuildGameAgentRunningScriptsSnapshot(),
                recentEventCount));
        });

    private Task<IReadOnlyList<GameAgentEvent>> GetMtcRpcRecentEventsAsync(int limit, bool includeAnsi)
    {
        IReadOnlyList<GameAgentEvent> events = _gameAgent.GetRecentEvents(limit);
        if (includeAnsi)
            return Task.FromResult(events);

        IReadOnlyList<GameAgentEvent> stripped = events
            .Select(evt => new GameAgentEvent
            {
                Timestamp = evt.Timestamp,
                GameName = evt.GameName,
                Kind = evt.Kind,
                PlainText = evt.PlainText,
                AnsiText = string.Empty,
                CurrentSector = evt.CurrentSector,
                PromptSurface = evt.PromptSurface,
                Metadata = evt.Metadata,
            })
            .ToArray();
        return Task.FromResult(stripped);
    }

    private Task<GameAgentSectorSnapshot?> QueryMtcRpcSectorAsync(int sector)
        => InvokeMtcRpcUiAsync(() => Task.FromResult(GameAgentRuntime.BuildSectorSnapshot(_sessionDb, sector)));

    private async Task<IReadOnlyList<GameAgentRunningScriptSnapshot>> ListMtcRpcScriptsAsync()
    {
        GameAgentContextSnapshot context = await BuildMtcRpcContextAsync(recentEventCount: 0).ConfigureAwait(false);
        return context.RunningScripts;
    }

    private Task<MtcRpcActionResult> SendMtcRpcCommandAsync(string command, bool appendEnter)
        => InvokeMtcRpcUiAsync(() =>
        {
            if (!IsMtcRpcConnected())
                return Task.FromResult(MtcRpcActionResult.Fail("Connect to server, first."));

            string payload = command ?? string.Empty;
            if (appendEnter && !payload.EndsWith('\r') && !payload.EndsWith('\n'))
                payload += "\r";

            byte[] bytes = Encoding.Latin1.GetBytes(payload);
            if (_termCtrl.SendInput == null)
                return Task.FromResult(MtcRpcActionResult.Fail("Terminal input is not available."));

            _termCtrl.SendInput.Invoke(bytes);
            return Task.FromResult(MtcRpcActionResult.Ok("Command submitted through the MTC terminal input path.", new Dictionary<string, string>
            {
                ["bytes"] = bytes.Length.ToString(),
                ["appendEnter"] = appendEnter ? "true" : "false",
            }));
        });

    private async Task<MtcRpcSendAndWaitResult> SendMtcRpcSendAndWaitAsync(string command, bool appendEnter, double timeoutSeconds)
    {
        MtcRpcActionResult send = await SendMtcRpcCommandAsync(command, appendEnter).ConfigureAwait(true);
        if (!send.Success)
            return new MtcRpcSendAndWaitResult
            {
                Success = false,
                Message = send.Message,
                Lines = [],
                Prompt = string.Empty,
                TimedOut = false,
            };

        long beforeTicks = GetLastGameAgentEventTicks();
        long watermark = beforeTicks;
        var collected = new List<string>();
        string prompt = string.Empty;
        bool timedOut = true;
        var deadline = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 0.5, 90));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // A sent command immediately re-echoes into the prompt event, so a bare "prompt seen"
        // check would truncate the response. Require the stream to settle instead: a prompt
        // event must be observed and no further server events may arrive for SettleDwell.
        TimeSpan settleDwell = TimeSpan.FromMilliseconds(400);
        long lastEventTicks = watermark;
        bool promptSeen = false;

        while (stopwatch.Elapsed < deadline)
        {
            await Task.Delay(120).ConfigureAwait(true);
            (List<string> freshLines, string freshPrompt, long latestTicks) = CollectEventsSince(watermark);
            if (freshLines.Count > 0 || !string.IsNullOrEmpty(freshPrompt) || latestTicks > watermark)
            {
                watermark = latestTicks;
                collected.AddRange(freshLines);
                if (!string.IsNullOrEmpty(freshPrompt))
                {
                    prompt = freshPrompt;
                    promptSeen = true;
                }
                lastEventTicks = Environment.TickCount64;
            }

            if (promptSeen && Environment.TickCount64 - lastEventTicks >= settleDwell.TotalMilliseconds)
            {
                timedOut = false;
                break;
            }
        }

        return new MtcRpcSendAndWaitResult
        {
            Success = true,
            Message = timedOut
                ? $"Command sent; no prompt within {deadline.TotalSeconds:0.#}s. Partial response captured."
                : $"Command sent; response captured ({collected.Count} lines).",
            Lines = collected,
            Prompt = prompt,
            TimedOut = timedOut,
        };
    }

    /// <summary>Builds the structured Mombot status snapshot for the mombot_status tool.</summary>
    private Task<MombotRpcStatusSnapshot> GetMtcRpcMombotStatusAsync()
        => InvokeMtcRpcUiAsync(() =>
        {
            MTC.mombot.mombotStatusSnapshot snapshot = _mombot.GetStatusSnapshot();
            return Task.FromResult(new MombotRpcStatusSnapshot(
                Enabled: snapshot.Enabled,
                AutoStart: snapshot.AutoStart,
                Attached: snapshot.IsAttached,
                WatcherEnabled: snapshot.WatcherEnabled,
                WatcherAttached: snapshot.WatcherAttached,
                AcceptSelfCommands: snapshot.AcceptSelfCommands,
                AcceptSubspaceCommands: snapshot.AcceptSubspaceCommands,
                AcceptPrivateCommands: snapshot.AcceptPrivateCommands,
                BotName: snapshot.BotName,
                TeamName: snapshot.TeamName,
                SubspaceChannel: snapshot.SubspaceChannel,
                CurrentSector: snapshot.CurrentSector,
                Mode: snapshot.Mode,
                LastLoadedModule: snapshot.LastLoadedModule,
                ScriptRoot: snapshot.ScriptRoot,
                AuthorizedUsers: snapshot.AuthorizedUsers,
                GameConnected: _gameInstance?.IsConnected == true || _telnet.IsConnected,
                ExternalBotName: _gameInstance?.ActiveBotName ?? string.Empty));
        });

    /// <summary>Sends an in-game subspace page command to the Mombot through the game stream
    /// (a quote line addressed to the bot name), then optionally collects the bot's response
    /// for up to the requested wait window.</summary>
    private async Task<MtcRpcSendAndWaitResult> SendMtcRpcMombotPageAsync(string command, double waitSeconds)
    {
        string input = (command ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
            throw new MtcRpcException(-32602, "Mombot command is required.");

        return await InvokeMtcRpcUiAsync(async () =>
        {
            MTC.mombot.mombotStatusSnapshot snapshot = _mombot.GetStatusSnapshot();
            if (!snapshot.Enabled)
                return new MtcRpcSendAndWaitResult { Success = false, Message = "Native MTC Mombot is not enabled.", Lines = [], Prompt = string.Empty, TimedOut = true };
            if (_gameInstance == null || !_gameInstance.IsConnected)
                return new MtcRpcSendAndWaitResult { Success = false, Message = "Connect to server, first.", Lines = [], Prompt = string.Empty, TimedOut = true };

            string botName = string.IsNullOrWhiteSpace(snapshot.BotName) ? "mombot" : snapshot.BotName;
            string pageLine = $"'{botName} {input}";
            long watermark = GetLastGameAgentEventTicks();
            await _gameInstance.SendToServerAsync(System.Text.Encoding.ASCII.GetBytes(pageLine + "\r")).ConfigureAwait(true);

            if (waitSeconds <= 0)
                return new MtcRpcSendAndWaitResult { Success = true, Message = $"Page sent: {pageLine}", Lines = [], Prompt = string.Empty, TimedOut = false };

            var deadline = TimeSpan.FromSeconds(Math.Clamp(waitSeconds, 0.5, 90));
            await Task.Delay(deadline).ConfigureAwait(true);
            (List<string> lines, string _, long _) = CollectEventsSince(watermark);
            return new MtcRpcSendAndWaitResult
            {
                Success = true,
                Message = lines.Count == 0
                    ? $"Page sent: {pageLine}. No response captured within {deadline.TotalSeconds:0.#}s."
                    : $"Page sent: {pageLine}. Extracted {lines.Count} bot response lines.",
                Lines = lines,
                Prompt = string.Empty,
                TimedOut = lines.Count == 0,
            };
        }).ConfigureAwait(false);
    }

    private long GetLastGameAgentEventTicks()
    {
        IReadOnlyList<GameAgentEvent> events = _gameAgent.GetRecentEvents(int.MaxValue);
        return events.Count == 0 ? 0 : events[^1].Timestamp.Ticks;
    }

    /// <summary>Collects newly observed server lines since <paramref name="watermark"/>.
    /// Returns the lines, the last prompt seen (empty string when none arrived), and the
    /// newest event tick (for the stream-settle watermark).</summary>
    private (List<string> Lines, string Prompt, long LatestTicks) CollectEventsSince(long watermark)
    {
        IReadOnlyList<GameAgentEvent> events = _gameAgent.GetRecentEvents(int.MaxValue);
        var lines = new List<string>();
        string prompt = string.Empty;
        long latestTicks = watermark;

        foreach (GameAgentEvent evt in events)
        {
            if (evt.Timestamp.Ticks <= watermark)
                continue;

            if (evt.Timestamp.Ticks > latestTicks)
                latestTicks = evt.Timestamp.Ticks;

            if (evt.Kind == GameAgentEventKind.ServerPrompt)
            {
                prompt = evt.PlainText;
                continue;
            }

            if (evt.Kind == GameAgentEventKind.ServerLine && !string.IsNullOrWhiteSpace(evt.PlainText))
                lines.Add(evt.PlainText);
        }

        return (lines, prompt, latestTicks);
    }

    private Task<MtcRpcActionResult> RunMtcRpcScriptAsync(string script)
        => InvokeMtcRpcUiAsync(() =>
        {
            string scriptReference = (script ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(scriptReference))
                return Task.FromResult(MtcRpcActionResult.Fail("Script name is required."));

            Core.ModInterpreter? interpreter = CurrentInterpreter;
            bool remoteProxyScripts = interpreter == null && CanUseRemoteProxyScripts();
            if (interpreter == null && !remoteProxyScripts)
                return Task.FromResult(MtcRpcActionResult.Fail("Connect to server, first."));

            try
            {
                if (interpreter != null)
                    Core.ProxyGameOperations.LoadScript(interpreter, scriptReference);
                else
                    SendProxyMenuCommand($"ss {scriptReference}");

                _parser.Feed($"\x1b[1;36m[JSON-RPC loaded script: {scriptReference}]\x1b[0m\r\n");
                _buffer.Dirty = true;
                RebuildProxyMenu();
                RebuildScriptsMenu();
                return Task.FromResult(MtcRpcActionResult.Ok("Script load requested.", new Dictionary<string, string>
                {
                    ["script"] = scriptReference,
                }));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MtcRpcActionResult.Fail(ex.Message));
            }
        });

    private Task<MtcRpcActionResult> StopMtcRpcScriptAsync(int? id, string? name)
        => InvokeMtcRpcUiAsync(() =>
        {
            Core.ModInterpreter? interpreter = CurrentInterpreter;
            bool remoteProxyScripts = interpreter == null && CanUseRemoteProxyScripts();
            if (interpreter == null && !remoteProxyScripts)
                return Task.FromResult(MtcRpcActionResult.Fail("Connect to server, first."));

            try
            {
                bool stopped;
                if (interpreter != null && id is { } scriptId)
                {
                    stopped = Core.ProxyGameOperations.StopScriptById(interpreter, scriptId);
                    return Task.FromResult(stopped
                        ? MtcRpcActionResult.Ok($"Script ID {scriptId} stopped.")
                        : MtcRpcActionResult.Fail($"Script ID {scriptId} was not found."));
                }

                if (interpreter != null && !string.IsNullOrWhiteSpace(name))
                {
                    stopped = Core.ProxyGameOperations.StopScriptByName(interpreter, name);
                    return Task.FromResult(stopped
                        ? MtcRpcActionResult.Ok($"Script '{name}' stopped.")
                        : MtcRpcActionResult.Fail($"Script '{name}' was not found."));
                }

                if (remoteProxyScripts && id is { } remoteId)
                {
                    SendProxyMenuCommand($"sk {remoteId}");
                    return Task.FromResult(MtcRpcActionResult.Ok($"Remote proxy kill requested for script ID {remoteId}."));
                }

                return Task.FromResult(MtcRpcActionResult.Fail("Stopping a remote script by name is not supported; use id."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MtcRpcActionResult.Fail(ex.Message));
            }
        });

    /// <summary>
    /// Compiles a TWX script source file in-process with <see cref="Core.ScriptCmp"/>, mirroring
    /// TWXC's default (pruned) mode. Writes the .cts next to the source when <paramref name="run"/>
    /// is true; otherwise performs a check-only compile pass. Uses an atomic temp-file move so a
    /// failed write never leaves a truncated .cts behind.
    /// </summary>
    /// <param name="path">Source path relative to the scripts root directory</param>
    /// <param name="run">True to write the compiled .cts; false for a check-only pass</param>
    /// <returns>Compiler result: diagnostics on failure, code size / lines / definitions on success</returns>
    private Task<MtcRpcActionResult> CompileMtcRpcScriptAsync(string path, bool run)
    {
        string scriptRoot = ResolveEffectiveScriptDirectory();
        if (!ScriptPathGuard.TryResolve(path, scriptRoot, out string fullPath))
            return Task.FromResult(MtcRpcActionResult.Fail(ScriptPathRejectionMessage(path, scriptRoot)));

        if (!File.Exists(fullPath))
            return Task.FromResult(MtcRpcActionResult.Fail(ScriptNotFoundMessage(path, scriptRoot)));

        try
        {
            using var scriptCmp = new Core.ScriptCmp(new Core.ScriptRef(), scriptRoot);
            scriptCmp.PruneBytecode = true;
            scriptCmp.CompileFromFile(fullPath, descFile: string.Empty);

            if (!run)
            {
                return Task.FromResult(MtcRpcActionResult.Ok(
                    "Compilation successful (check only; no file written).", new Dictionary<string, string>
                    {
                        ["codeSize"] = scriptCmp.CodeSize.ToString(),
                        ["lines"] = scriptCmp.LineCount.ToString(),
                        ["definitions"] = scriptCmp.ParamCount.ToString(),
                    }));
            }

            string ctsFile = Path.ChangeExtension(fullPath, ".cts");
            string outputDir = Path.GetDirectoryName(Path.GetFullPath(ctsFile)) ?? scriptRoot;
            string tempFile = Path.Combine(outputDir, $".{Path.GetFileName(ctsFile)}.{Guid.NewGuid():N}.tmp");
            try
            {
                scriptCmp.WriteToFile(tempFile);
                File.Move(tempFile, ctsFile, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }

            return Task.FromResult(MtcRpcActionResult.Ok(
                $"Compilation successful; wrote {Path.GetFileName(ctsFile)}.", new Dictionary<string, string>
                {
                    ["output"] = ctsFile,
                    ["codeSize"] = scriptCmp.CodeSize.ToString(),
                    ["lines"] = scriptCmp.LineCount.ToString(),
                    ["definitions"] = scriptCmp.ParamCount.ToString(),
                }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(MtcRpcActionResult.Fail($"Compilation failed: {ex.Message}"));
        }
    }

    private Task<MtcRpcActionResult> WriteMtcRpcScriptFileAsync(string path, string content)
    {
        string scriptRoot = ResolveEffectiveScriptDirectory();
        if (!ScriptPathGuard.TryResolve(path, scriptRoot, out string fullPath))
            return Task.FromResult(MtcRpcActionResult.Fail(ScriptPathRejectionMessage(path, scriptRoot)));

        try
        {
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return Task.FromResult(MtcRpcActionResult.Ok("Script file written.", new Dictionary<string, string>
            {
                ["path"] = fullPath,
                ["bytes"] = Encoding.UTF8.GetByteCount(content).ToString(),
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(MtcRpcActionResult.Fail(ex.Message));
        }
    }

    private Task<MtcRpcActionResult> EditMtcRpcScriptFileAsync(string path, string oldText, string newText, bool replaceAll)
    {
        string scriptRoot = ResolveEffectiveScriptDirectory();
        if (!ScriptPathGuard.TryResolve(path, scriptRoot, out string fullPath))
            return Task.FromResult(MtcRpcActionResult.Fail(ScriptPathRejectionMessage(path, scriptRoot)));

        try
        {
            if (!File.Exists(fullPath))
                return Task.FromResult(MtcRpcActionResult.Fail(ScriptNotFoundMessage(path, scriptRoot)));

            string content = ReadScriptText(fullPath);
            int matchCount = CountOrdinalOccurrences(content, oldText);
            if (matchCount == 0)
                return Task.FromResult(MtcRpcActionResult.Fail("oldText not found in the script file."));
            if (matchCount > 1 && !replaceAll)
                return Task.FromResult(MtcRpcActionResult.Fail(
                    $"oldText matches {matchCount} times; make oldText unique or pass replaceAll: true."));

            string updated = replaceAll
                ? content.Replace(oldText, newText, StringComparison.Ordinal)
                : ReplaceFirstOccurrence(content, oldText, newText);
            File.WriteAllText(fullPath, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return Task.FromResult(MtcRpcActionResult.Ok("Script file edited.", new Dictionary<string, string>
            {
                ["path"] = fullPath,
                ["matches"] = matchCount.ToString(),
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(MtcRpcActionResult.Fail(ex.Message));
        }
    }

    private Task<MtcScriptReadResult> ReadMtcRpcScriptFileAsync(string path, int offset, int limit)
    {
        string scriptRoot = ResolveEffectiveScriptDirectory();
        if (!ScriptPathGuard.TryResolve(path, scriptRoot, out string fullPath))
            throw new MtcRpcException(-32602, ScriptPathRejectionMessage(path, scriptRoot));

        if (!File.Exists(fullPath))
            throw new MtcRpcException(-32602, ScriptNotFoundMessage(path, scriptRoot));

        try
        {
            string content = ReadScriptText(fullPath);
            string[] lines = content.Length == 0 ? [] : content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int effectiveOffset = Math.Max(offset, 1);
            int effectiveLimit = Math.Max(limit, 1);
            int start = Math.Min(effectiveOffset - 1, lines.Length);
            int count = Math.Min(effectiveLimit, lines.Length - start);
            var selected = new string[count];
            for (int i = 0; i < count; i++)
            {
                selected[i] = lines[start + i];
            }

            return Task.FromResult(new MtcScriptReadResult
            {
                Path = fullPath,
                TotalLines = lines.Length,
                Offset = effectiveOffset,
                Content = string.Join('\n', selected),
            });
        }
        catch (Exception ex)
        {
            throw new MtcRpcException(-32602, ex.Message);
        }
    }

    private static string ReadScriptText(string fullPath)
    {
        byte[] bytes = File.ReadAllBytes(fullPath);
        return TryDecodeUtf8(bytes, out string utf8) ? utf8 : Encoding.Latin1.GetString(bytes);
    }

    private static bool TryDecodeUtf8(byte[] bytes, out string decoded)
    {
        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            decoded = strictUtf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            decoded = string.Empty;
            return false;
        }
    }

    private static int CountOrdinalOccurrences(string content, string oldText)
    {
        int count = 0;
        int index = 0;
        while ((index = content.IndexOf(oldText, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += oldText.Length;
        }
        return count;
    }

    private static string ReplaceFirstOccurrence(string content, string oldText, string newText)
    {
        int index = content.IndexOf(oldText, StringComparison.Ordinal);
        return index < 0 ? content : string.Concat(content.AsSpan(0, index), newText, content.AsSpan(index + oldText.Length));
    }

    private static string ScriptPathRejectionMessage(string path, string scriptRoot)
        => $"Invalid script path '{path}': script paths are relative to the configured script directory '{scriptRoot}' and cannot escape it.";

    private static string ScriptNotFoundMessage(string path, string scriptRoot)
        => $"Script file not found: '{path}' does not exist within the configured script directory '{scriptRoot}'.";

    private async Task<MtcRpcActionResult> ConnectMtcRpcServerAsync(string? host, int? port)
    {
        string connectHost = host?.Trim() ?? string.Empty;
        int connectPort = port ?? 2002;
        bool alreadyConnected = await InvokeMtcRpcUiAsync(() =>
            Task.FromResult(_telnet.IsConnected || (_gameInstance?.IsConnected ?? false))).ConfigureAwait(false);
        if (alreadyConnected)
        {
            if (!string.IsNullOrEmpty(connectHost))
                return MtcRpcActionResult.Fail("Already connected; disconnect_server first, then connect to the new address.");
            return MtcRpcActionResult.Ok("Already connected to the game server.", new Dictionary<string, string>
            {
                ["server"] = _state.Host,
                ["port"] = _state.Port.ToString(),
            });
        }

        string effectiveHost = _state.EmbeddedProxy
            ? _embeddedGameConfig?.Host ?? _state.Host
            : _state.Host;
        if (string.IsNullOrEmpty(connectHost) && string.IsNullOrWhiteSpace(effectiveHost))
            return MtcRpcActionResult.Fail("No connect address configured; pass host and port or set them in MTC first.");

        if (!string.IsNullOrEmpty(connectHost))
        {
            await InvokeMtcRpcUiAsync(() =>
            {
                _state.Host = connectHost;
                _state.Port = connectPort;
                return Task.FromResult(string.Empty);
            }).ConfigureAwait(false);
        }

        await InvokeMtcRpcUiAsync(async () =>
        {
            await OnConnectAsync().ConfigureAwait(true);
            return string.Empty;
        }).ConfigureAwait(false);

        bool connected = await InvokeMtcRpcUiAsync(() =>
            Task.FromResult(_telnet.IsConnected || (_gameInstance?.IsConnected ?? false))).ConfigureAwait(false);
        return connected
            ? MtcRpcActionResult.Ok("Connected to the game server.", new Dictionary<string, string>
            {
                ["server"] = _state.Host,
                ["port"] = _state.Port.ToString(),
            })
            : MtcRpcActionResult.Fail("Connection failed; check the MTC terminal for the reason.");
    }

    private async Task<MtcRpcActionResult> DisconnectMtcRpcServerAsync()
    {
        bool connected = await InvokeMtcRpcUiAsync(() =>
            Task.FromResult(_telnet.IsConnected || (_gameInstance?.IsConnected ?? false))).ConfigureAwait(false);
        if (!connected)
            return MtcRpcActionResult.Fail("Not connected to a server.");

        await InvokeMtcRpcUiAsync(async () =>
        {
            await OnDisconnectAsync().ConfigureAwait(true);
            return string.Empty;
        }).ConfigureAwait(false);

        bool stillConnected = await InvokeMtcRpcUiAsync(() =>
            Task.FromResult(_telnet.IsConnected || (_gameInstance?.IsConnected ?? false))).ConfigureAwait(false);
        return stillConnected
            ? MtcRpcActionResult.Fail("Disconnect did not complete; check the MTC terminal.")
            : MtcRpcActionResult.Ok("Disconnected from the game server.");
    }

    private Task<bool> ApproveMtcRpcActionAsync(string action, string details)
        => InvokeMtcRpcUiAsync(() => ShowConfirmAsync(
            "JSON-RPC Action Approval",
            $"{action}\n\n{details}\n\nAllow this JSON-RPC client action?",
            "Allow",
            "Reject"));

    private bool IsMtcRpcConnected()
        => _gameInstance?.IsConnected == true || _telnet.IsConnected;

    private Task<T> InvokeMtcRpcUiAsync<T>(Func<Task<T>> action)
    {
        var owner = ResolveCurrentMtcTabContext();
        var runtimeContext = Core.GlobalModules.CurrentContext;
        if (Dispatcher.UIThread.CheckAccess())
        {
            owner ??= FindMtcTabForRuntimeContext(runtimeContext) ?? ActiveMtcTab;
            return ExecuteInOptionalMtcTabSessionAsync(owner, action);
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var resolvedOwner = owner
                    ?? FindMtcTabForRuntimeContext(runtimeContext)
                    ?? ActiveMtcTab;
                tcs.SetResult(await ExecuteInOptionalMtcTabSessionAsync(resolvedOwner, action).ConfigureAwait(true));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
