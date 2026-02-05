import * as path from 'path';
import * as fs from 'fs';
import { workspace, ExtensionContext } from 'vscode';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions
} from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: ExtensionContext) {
  // Detect whether running from VSIX (production) or development
  const serverDir = context.asAbsolutePath(path.join('server'));

  let serverOptions: ServerOptions;

  if (fs.existsSync(serverDir)) {
    // Production: use published server binary from VSIX
    const serverPath = path.join(serverDir, 'LangLSP.Server');
    serverOptions = {
      run: { command: serverPath, options: { cwd: serverDir } },
      debug: { command: serverPath, options: { cwd: serverDir } }
    };
  } else {
    // Development: use dotnet run
    serverOptions = {
      run: {
        command: 'dotnet',
        args: ['run', '--project', context.asAbsolutePath(
          path.join('..', 'src', 'LangLSP.Server', 'LangLSP.Server.fsproj')
        )],
        options: { cwd: context.asAbsolutePath('..') }
      },
      debug: {
        command: 'dotnet',
        args: ['run', '--project', context.asAbsolutePath(
          path.join('..', 'src', 'LangLSP.Server', 'LangLSP.Server.fsproj')
        )],
        options: { cwd: context.asAbsolutePath('..') }
      }
    };
  }

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
