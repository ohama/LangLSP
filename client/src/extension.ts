import * as path from 'path';
import { workspace, ExtensionContext } from 'vscode';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions
} from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: ExtensionContext) {
  // Path to the F# LSP server executable
  // After `dotnet publish`, the executable is in the publish folder
  const serverPath = context.asAbsolutePath(
    path.join('server', 'LangLSP.Server')
  );

  // For development: use dotnet run
  // For production: use published executable
  const serverOptions: ServerOptions = {
    run: {
      command: 'dotnet',
      args: ['run', '--project', context.asAbsolutePath(path.join('..', 'src', 'LangLSP.Server', 'LangLSP.Server.fsproj'))],
      options: { cwd: context.asAbsolutePath('..') }
    },
    debug: {
      command: 'dotnet',
      args: ['run', '--project', context.asAbsolutePath(path.join('..', 'src', 'LangLSP.Server', 'LangLSP.Server.fsproj'))],
      options: { cwd: context.asAbsolutePath('..') }
    }
  };

  // Client options: what documents to sync
  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'funlang' }],
    synchronize: {
      fileEvents: workspace.createFileSystemWatcher('**/*.fun')
    }
  };

  // Create and start the client
  client = new LanguageClient(
    'funlangServer',
    'FunLang Language Server',
    serverOptions,
    clientOptions
  );

  // Start the client (and server)
  client.start();
}

export function deactivate(): Thenable<void> | undefined {
  if (!client) {
    return undefined;
  }
  return client.stop();
}
