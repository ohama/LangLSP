module LangLSP.Server.Program

open System
open System.IO
open Serilog

[<EntryPoint>]
let main argv =
    // Set up Serilog logging to a file
    Log.Logger <-
        LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(Path.GetTempPath(), "funlang-lsp.log"),
                rollingInterval = RollingInterval.Day,
                outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger()

    try
        Log.Information("FunLang LSP Server starting...")

        // Set up stdin/stdout for LSP communication
        use input = Console.OpenStandardInput()
        use output = Console.OpenStandardOutput()

        Log.Information("Setting up LSP server on stdin/stdout")
        Log.Information("Server capabilities configured: {Capabilities}", Server.serverCapabilities)

        // Keep server running
        // TODO: Implement actual LSP message loop in next phase
        // For now, just wait for stdin to close
        let mutable running = true
        while running do
            let line = Console.ReadLine()
            if line = null then
                running <- false
            else
                Log.Debug("Received input: {Input}", line)

        Log.Information("LSP Server shutdown complete")
        0
    with ex ->
        Log.Fatal(ex, "LSP Server crashed")
        1
