export class SyncCancelledError extends Error {
    constructor() {
        super('Sync was cancelled');
        this.name = 'SyncCancelledError';
    }
}
