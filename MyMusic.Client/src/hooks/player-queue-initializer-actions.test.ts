import {describe, expect, it} from 'vitest';
import type {GetPlaylistSongItem} from '../model';
import {type QueueInitializerState, resolveQueueInitializerAction} from './player-queue-initializer-actions';

const song = {id: 7, title: 'The Alibi'} as GetPlaylistSongItem;

function makeState(overrides: Partial<QueueInitializerState> = {}): QueueInitializerState {
    return {
        isLoading: false,
        isFetching: false,
        queue: [song],
        currentSongId: song.id,
        currentType: 'EMPTY',
        currentQueueId: 1,
        visibleQueueId: 1,
        ...overrides,
    };
}

describe('resolveQueueInitializerAction', () => {
    it('does nothing while the queue is loading', () => {
        expect(resolveQueueInitializerAction(makeState({isLoading: true}))).toEqual({type: 'none'});
    });

    it('loads the queue current song when the player is empty', () => {
        expect(resolveQueueInitializerAction(makeState())).toEqual({type: 'load', song});
    });

    it('does nothing when the current song is not in the queue', () => {
        expect(resolveQueueInitializerAction(makeState({currentSongId: 99}))).toEqual({type: 'none'});
    });

    it('does nothing when the player already has a song', () => {
        expect(resolveQueueInitializerAction(makeState({currentType: 'LOADED'}))).toEqual({type: 'none'});
    });

    it('clears the player when the playing queue is empty', () => {
        expect(resolveQueueInitializerAction(makeState({queue: [], currentType: 'LOADING'}))).toEqual({type: 'clear'});
    });

    it('does nothing when viewing a different queue than the playing one', () => {
        expect(resolveQueueInitializerAction(makeState({queue: [], currentType: 'LOADING', visibleQueueId: 2})))
            .toEqual({type: 'none'});
    });

    it('does not clear the player from a stale empty queue while it is being refetched', () => {
        // Right after a queue is created and a song starts loading, the cached queue is still empty
        expect(resolveQueueInitializerAction(makeState({queue: [], currentType: 'LOADING', isFetching: true})))
            .toEqual({type: 'none'});
    });
});
