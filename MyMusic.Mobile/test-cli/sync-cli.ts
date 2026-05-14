#!/usr/bin/env node

import type { SyncDeps, SyncResult, IKeepAwake, IUserPrompt, ConflictResolution } from '../src/services/sync/types';
import { createSyncContext } from '../src/services/sync/context';
import { orchestrateSync } from '../src/services/sync/orchestrator';
import { NodeSyncConfig } from './node-config';
import { NodeApiClient } from './node-api-client';
import { nodeScanner } from './node-scanner';
import { NodeFileOps } from './node-file-ops';

const keepAwake: IKeepAwake = {
    activate: async () => {},
    deactivate: () => {},
};

const autoConfirmPrompt: IUserPrompt = {
    promptConflictResolution: async (_filePath: string): Promise<ConflictResolution> => 'upload',
    confirmDeletion: async (_filePath: string): Promise<boolean> => true,
};

interface CliArgs {
    command: string;
    force: boolean;
    dryRun: boolean;
    autoConfirm: boolean;
    direction: string | null;
    verbose: boolean;
}

function parseArgs(argv: string[]): CliArgs {
    const args = argv.slice(2);
    const result: CliArgs = {
        command: args[0] ?? 'sync',
        force: false,
        dryRun: false,
        autoConfirm: false,
        direction: null,
        verbose: false,
    };

    for (let i = 1; i < args.length; i++) {
        const arg = args[i];
        switch (arg) {
            case '--force':
            case '-f':
                result.force = true;
                break;
            case '--dry-run':
                result.dryRun = true;
                break;
            case '--yes':
            case '-y':
                result.autoConfirm = true;
                break;
            case '--direction':
            case '-d':
                result.direction = args[++i] ?? null;
                break;
            case '--verbose':
                result.verbose = true;
                break;
        }
    }

    return result;
}

function printResults(result: SyncResult): void {
    console.log(`Created: ${result.created}`);
    console.log(`Updated: ${result.updated}`);
    console.log(`Downloaded: ${result.downloaded}`);
    console.log(`Removed: ${result.removed}`);
    console.log(`Failed: ${result.failed}`);
    console.log(`Conflicts: ${result.conflicts}`);
}

async function main(): Promise<number> {
    const args = parseArgs(process.argv);

    if (args.command !== 'sync') {
        console.error(`Unknown command: ${args.command}`);
        console.error('Usage: npx tsx sync-cli.ts sync [--force] [--dry-run] [--yes] [--direction up|down]');
        return 1;
    }

    const configPath = process.env.MYMUSIC_CONFIG_PATH;
    if (!configPath) {
        console.error('Error: MYMUSIC_CONFIG_PATH environment variable is required');
        return 1;
    }

    const config = new NodeSyncConfig(configPath);
    const deviceId = config.getDeviceId();
    if (!deviceId) {
        console.error('Error: deviceId not configured');
        return 1;
    }

    const apiClient = new NodeApiClient(config.getServerUrl(), config.getUserId(), config.getUserName());
    const fileOps = new NodeFileOps();

    const state = {
        isCancelled: false,
        options: {
            force: args.force,
            dryRun: args.dryRun,
            autoConfirm: args.autoConfirm,
            treatConflictsAsErrors: false,
            scannerType: 'fileSystem' as const,
        },
    };

    const deps: SyncDeps = {
        apiClient,
        config,
        state,
        scanner: nodeScanner,
        fileOps,
        keepAwake,
        userPrompt: autoConfirmPrompt,
    };

    const ctx = createSyncContext(config, state);

    if (args.verbose) {
        console.log('MyMusic Mobile Sync');
        if (args.dryRun) {
            console.log('Dry run mode - no changes will be made');
        }
        console.log(`Device: ${deviceId}`);
        console.log(`Repository: ${config.getRepositoryPath()}`);
        console.log(`Server: ${config.getServerUrl()}`);
        console.log('');
    }

    try {
        const result = await orchestrateSync(deps, ctx, (_progress) => {
            // Progress handler is no-op for CLI mode; tests don't need progress UI
        });

        printResults(result);

        if (result.failed > 0) {
            return 1;
        }

        return 0;
    } catch (error) {
        console.error('Sync failed:', error instanceof Error ? error.message : String(error));
        printResults(ctx.result);
        return 1;
    }
}

main()
    .then((exitCode) => {
        process.exit(exitCode);
    })
    .catch((error) => {
        console.error('Unexpected error:', error);
        process.exit(1);
    });
