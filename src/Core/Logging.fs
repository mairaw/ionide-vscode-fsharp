namespace Ionide.VSCode.FSharp

[<AutoOpen>]
module Logging =
    open Fable.Import.Node
    open Fable.Import.vscode
    open System

    type Level = DBG|INF|WRN|ERR
        with
            static member GetLevelNum level = match level with DBG->10|INF->20|WRN->30|ERR->40
            override this.ToString() = match this with ERR->"ERROR"|INF->"INFO " |WRN->"WARN "|DBG->"DEBUG"
            member this.isGreaterOrEqualTo level = Level.GetLevelNum(this) >= Level.GetLevelNum(level)

    let private writeDevToolsConsole (level: Level) (source: string option) (template: string) (args: obj[]) =
        // just replace %j (Util.format->JSON specifier --> console->OBJECT %O specifier)
        // the other % specifiers are basically the same
        let browserLogTemplate = String.Format("[{0,5}] {1}", source.ToString().PadRight(5), template.Replace("%j", "%O"))
        match args.Length with
        | 0 -> Fable.Import.Browser.console.log (browserLogTemplate)
        | 1 -> Fable.Import.Browser.console.log (browserLogTemplate, args.[0])
        | 2 -> Fable.Import.Browser.console.log (browserLogTemplate, args.[0], args.[1])
        | 3 -> Fable.Import.Browser.console.log (browserLogTemplate, args.[0], args.[1], args.[2])
        | 4 -> Fable.Import.Browser.console.log (browserLogTemplate, args.[0], args.[1], args.[2], args.[3])
        | _ -> Fable.Import.Browser.console.log (browserLogTemplate, args)

    let private writeOutputChannel (out: OutputChannel) level source template args =
        let formattedMessage = util.format(template, args)
        let formattedLogLine = String.Format("[{0:HH:mm:ss} {1}] {2}", DateTime.Now, string level, formattedMessage)
        out.appendLine (formattedLogLine)

    let private writeBothIfConfigured (out: OutputChannel option)
              (chanMinLevel: Level)
              (consoleMinLevel: Level option)
              (level: Level)
              (source: string option)
              (template: string)
              (args: obj[]) =
        if consoleMinLevel.IsSome && level.isGreaterOrEqualTo(consoleMinLevel.Value) then
            writeDevToolsConsole level source template args

        if out.IsSome && level.isGreaterOrEqualTo(chanMinLevel) then
            writeOutputChannel out.Value level source template args

    /// The templates may use node util.format placeholders: %s, %d, %j, %%
    /// https://nodejs.org/api/util.html#util_util_format_format
    type ConsoleAndOutputChannelLogger(source: string option, chanMinLevel: Level, out:OutputChannel option, consoleMinLevel: Level option) =

        /// Logs a different message in either DBG (if enabled) or INF (otherwise).
        /// The templates may use node util.format placeholders: %s, %d, %j, %%
        /// https://nodejs.org/api/util.html#util_util_format_format
        member this.DebugOrInfo
                        (debugTemplateAndArgs: string * obj[])
                        (infoTemplateAndArgs: string * obj[]) =
            // OutputChannel: when at DBG level, use the DBG template and args, otherwise INF
            if out.IsSome then
                if chanMinLevel.isGreaterOrEqualTo(Level.DBG) then
                    writeOutputChannel out.Value DBG source (fst debugTemplateAndArgs) (snd debugTemplateAndArgs)
                elif chanMinLevel.isGreaterOrEqualTo(Level.INF) then
                    writeOutputChannel out.Value INF source (fst infoTemplateAndArgs) (snd infoTemplateAndArgs)

            // Console: when at DBG level, use the DBG template and args, otherwise INF
            if consoleMinLevel.IsSome then
                if Level.DBG.isGreaterOrEqualTo(consoleMinLevel.Value) then
                    writeDevToolsConsole DBG source (fst debugTemplateAndArgs) (snd debugTemplateAndArgs)
                elif Level.INF.isGreaterOrEqualTo(consoleMinLevel.Value) then
                    writeDevToolsConsole INF source (fst infoTemplateAndArgs) (snd infoTemplateAndArgs)

        /// Logs a message that should/could be seen by developers when diagnosing problems.
        /// The templates may use node util.format placeholders: %s, %d, %j, %%
        /// https://nodejs.org/api/util.html#util_util_format_format
        member this.Debug (template, [<ParamArray>]args:obj[]) =
            writeBothIfConfigured out chanMinLevel consoleMinLevel DBG source template args
        /// Logs a message that should/could be seen by the user in the output channel.
        /// The templates may use node util.format placeholders: %s, %d, %j, %%
        /// https://nodejs.org/api/util.html#util_util_format_format
        member this.Info (template, [<ParamArray>]args:obj[]) =
            writeBothIfConfigured out chanMinLevel consoleMinLevel INF source template args
        /// Logs a message that should/could be seen by the user in the output channel when a problem happens.
        /// The templates may use node util.format placeholders: %s, %d, %j, %%
        /// https://nodejs.org/api/util.html#util_util_format_format
        member this.Error (template, [<ParamArray>]args:obj[]) =
            writeBothIfConfigured out chanMinLevel consoleMinLevel ERR source template args
        /// Logs a message that should/could be seen by the user in the output channel when a problem happens.
        /// The templates may use node util.format placeholders: %s, %d, %j, %%
        /// https://nodejs.org/api/util.html#util_util_format_format
        member this.Warn (template, [<ParamArray>]args:obj[]) =
            writeBothIfConfigured out chanMinLevel consoleMinLevel WRN source template args
