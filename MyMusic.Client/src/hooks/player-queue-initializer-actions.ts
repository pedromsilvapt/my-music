import type {GetPlaylistSongItem} from '../model';
import type {PlayerCurrentSongState} from '../stores/playback-store';

export interface QueueInitializerState {
    isLoading: boolean;
    isFetching: boolean;
    queue: GetPlaylistSongItem[];
    currentSongId: number | null | undefined;
    currentType: PlayerCurrentSongState['type'];
    currentQueueId: number | null;
    visibleQueueId: number | null;
}

export type QueueInitializerAction =
    | { type: 'none' }
    | { type: 'clear' }
    | { type: 'load'; song: GetPlaylistSongItem };

/**
 * Decides how the player should be synced with the current queue fetched from the server:
 * clear the player when the queue is empty, or load the queue's current song when the player is empty.
 */
export function resolveQueueInitializerAction(state: QueueInitializerState): QueueInitializerAction {
    const {isLoading, isFetching, queue, currentSongId, currentType, currentQueueId, visibleQueueId} = state;

    // While (re)fetching, the cached queue may be stale: e.g. right after creating a queue and starting
    // a song, it can still be empty and would wrongly clear the player
    if (isLoading || isFetching) return {type: 'none'};

    // If viewing a different queue than what's playing, don't interfere with player
    if (currentQueueId !== null && currentQueueId !== visibleQueueId) {
        return {type: 'none'};
    }

    // If queue is empty and player has a song loaded, clear it
    // Only clear if we're viewing the queue that's supposed to be playing
    if (queue.length === 0 && currentType !== 'EMPTY' && currentQueueId === visibleQueueId) {
        return {type: 'clear'};
    }

    // If player is empty and queue has a current song, load it
    if (currentType === 'EMPTY' && currentSongId != null) {
        const song = queue.find((s) => s.id === currentSongId);
        if (song) {
            return {type: 'load', song};
        }
    }

    return {type: 'none'};
}
