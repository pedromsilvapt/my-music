#!/usr/bin/env node

import type { SyncDeps, SyncResult, IKeepAwake, IUserPrompt, ConflictResolution, SyncDirection } from '../src/services/sync/types';
import { createEmptyResult } from '../src/services/sync/context';
import { orchestrateSync, getPartialSyncResult } from '../src/services/sync/orchestrator';
import { NodeSyncConfig } from './node-config';
import { NodeApiClient } from './node-api-client';
import { nodeScanner } from './node-scanner';
import { NodeFileOps } from './node-file-ops';

const keepAwake: IKeepAwake = {
    activate: async () => {},
    deactivate: () => {},
};

/** Answers every prompt without asking: conflicts with the given resolution (when the direction allows it). */
function createAutoConfirmPrompt(conflicts: ConflictResolution): IUserPrompt {
    return {
        promptConflictResolution: async (_filePath: string, choices: ConflictResolution[]): Promise<ConflictResolution> =>
            choices.includes(conflicts) ? conflicts : 'skip',
        confirmDeletion: async (_filePath: string): Promise<boolean> => true,
    };
}

interface CliArgs {
    command: string;
    force: boolean;
    dryRun: boolean;
    autoConfirm: boolean;
    direction: SyncDirection;
    deduplicate: boolean;
    conflicts: ConflictResolution;
    verbose: boolean;
}

function parseArgs(argv: string[]): CliArgs {
    const args = argv.slice(2);
    const result: CliArgs = {
        command: args[0] ?? 'sync',
        force: false,
        dryRun: false,
        autoConfirm: false,
        direction: 'Both',
        deduplicate: false,
        conflicts: 'skip',
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
            case '--deduplicate':
                result.deduplicate = true;
                break;
            case '--yes':
            case '-y':
                result.autoConfirm = true;
                break;
            case '--direction':
            case '-d':
                result.direction = parseDirection(args[++i]);
                break;
            case '--conflicts':
                result.conflicts = parseConflicts(args[++i]);
                break;
            case '--verbose':
                result.verbose = true;
                break;
        }
    }

    return result;
}

function parseDirection(value: string | undefined): SyncDirection {
    switch (value?.toLowerCase()) {
        case 'up':
            return 'Up';
        case 'down':
            return 'Down';
        case 'both':
            return 'Both';
        default:
            throw new Error(`Invalid direction: ${value}. Expected up, down or both`);
    }
}

function parseConflicts(value: string | undefined): ConflictResolution {
    switch (value?.toLowerCase()) {
        case 'upload':
        case 'download':
        case 'skip':
            return value.toLowerCase() as ConflictResolution;
        default:
            throw new Error(`Invalid conflicts: ${value}. Expected upload, download or skip`);
    }
}

function printResults(result: SyncResult): void {
    console.log(`CreateRemote: ${result.createRemote}`);
    console.log(`UpdateRemote: ${result.updateRemote}`);
    console.log(`CreateLocal: ${result.createLocal}`);
    console.log(`UpdateLocal: ${result.updateLocal}`);
    console.log(`DeleteLocal: ${result.deleteLocal}`);
    console.log(`Link: ${result.link}`);
    console.log(`Unlink: ${result.unlink}`);
    console.log(`Rename: ${result.rename}`);
    console.log(`Skipped: ${result.skipped}`);
    console.log(`Conflict: ${result.conflict}`);
    console.log(`UpdateTimestamp: ${result.updateTimestamp}`);
    console.log(`Error: ${result.error}`);

    if (result.sessionId !== undefined && result.sessionId !== null) {
        console.log(`SessionId: ${result.sessionId}`);
    }
}

async function main(): Promise<number> {
    const args = parseArgs(process.argv);

    if (args.command !== 'sync') {
        console.error(`Unknown command: ${args.command}`);
        console.error('Usage: npx tsx sync-cli.ts sync [--force] [--dry-run] [--deduplicate] [--yes] [--direction up|down|both] [--conflicts upload|download|skip]');
        return 1;
    }

    const configPath = process.env.MYMUSIC_CONFIG_PATH;
    if (!configPath) {
        console.error('Error: MYMUSIC_CONFIG_PATH environment variable is required');
        return 1;
    }

    const config = new NodeSyncConfig(configPath);
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
            direction: args.direction,
            deduplicate: args.deduplicate,
        },
    };

    const deps: SyncDeps = {
        apiClient,
        config,
        state,
        scanner: nodeScanner,
        fileOps,
        keepAwake,
        userPrompt: createAutoConfirmPrompt(args.conflicts),
    };

    if (args.verbose) {
        console.log('MyMusic Mobile Sync');
        if (args.dryRun) {
            console.log('Dry run mode - no changes will be made');
        }
        console.log(`Device: ${config.getDeviceId()}`);
        console.log(`Repository: ${config.getRepositoryPath()}`);
        console.log(`Server: ${config.getServerUrl()}`);
        console.log('');
    }

    try {
        const result = await orchestrateSync(deps, (_progress) => {
            // Progress handler is no-op for CLI mode; tests don't need progress UI
        });

        printResults(result);

        if (result.error > 0) {
            return 1;
        }

        return 0;
    } catch (error) {
        console.error('Sync failed:', error instanceof Error ? error.message : String(error));
        printResults(getPartialSyncResult(error) ?? {...createEmptyResult(), error: 1});
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
