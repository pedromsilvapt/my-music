import {describe, expect, it} from 'vitest';
import type {GetPlaylistSongItem} from '../model';
import {createPlaybackStore, selectIsPlayingOrPending} from './playback-store';

const song = {id: 7, title: 'The Alibi'} as GetPlaylistSongItem;

function loadingStore(autoplay: boolean) {
    const store = createPlaybackStore();
    store.getState().setLoadingSong(song, autoplay);
    return store;
}

describe('playback store', () => {
    it('keeps the autoplay intent while the song is loading', () => {
        const store = loadingStore(true);

        expect(store.getState().current.type).toBe('LOADING');
        expect(store.getState().autoplay).toBe(true);
    });

    it('starts a loaded song paused until the audio actually plays', () => {
        const store = loadingStore(true);
        store.getState().load(120);

        const current = store.getState().current;
        expect(current.type === 'LOADED' && current.isPlaying).toBe(false);
        expect(store.getState().autoplay).toBe(true);
    });

    it('marks the song as playing and consumes the autoplay intent once playback starts', () => {
        const store = loadingStore(true);
        store.getState().load(120);
        store.getState().setIsPlaying(true);

        const current = store.getState().current;
        expect(current.type === 'LOADED' && current.isPlaying).toBe(true);
        expect(store.getState().autoplay).toBe(false);
    });

    it('cancels a pending autoplay when paused while loading', () => {
        const store = loadingStore(true);
        store.getState().setIsPlaying(false);

        expect(store.getState().autoplay).toBe(false);
    });

    it('requests autoplay when played while loading', () => {
        const store = loadingStore(false);
        store.getState().setIsPlaying(true);

        expect(store.getState().autoplay).toBe(true);
    });

    it('cancels a pending autoplay when paused after loading', () => {
        const store = loadingStore(true);
        store.getState().load(120);
        store.getState().setIsPlaying(false);

        const current = store.getState().current;
        expect(current.type === 'LOADED' && current.isPlaying).toBe(false);
        expect(store.getState().autoplay).toBe(false);
    });

    it('ignores play state changes when no song is loaded', () => {
        const store = createPlaybackStore();
        store.getState().setIsPlaying(true);

        expect(store.getState().current.type).toBe('EMPTY');
        expect(store.getState().autoplay).toBe(false);
    });

    it('clears the song and the autoplay intent', () => {
        const store = loadingStore(true);
        store.getState().clear();

        expect(store.getState().current.type).toBe('EMPTY');
        expect(store.getState().autoplay).toBe(false);
    });
});

describe('selectIsPlayingOrPending', () => {
    it('is false when no song is loaded', () => {
        expect(selectIsPlayingOrPending(createPlaybackStore().getState())).toBe(false);
    });

    it('is true while a song is loading to autoplay', () => {
        expect(selectIsPlayingOrPending(loadingStore(true).getState())).toBe(true);
    });

    it('is false while a song is loading paused', () => {
        expect(selectIsPlayingOrPending(loadingStore(false).getState())).toBe(false);
    });

    it('is true while a loaded song waits for autoplay to start', () => {
        const store = loadingStore(true);
        store.getState().load(120);

        expect(selectIsPlayingOrPending(store.getState())).toBe(true);
    });

    it('follows the playing state once playback started', () => {
        const store = loadingStore(true);
        store.getState().load(120);
        store.getState().setIsPlaying(true);
        expect(selectIsPlayingOrPending(store.getState())).toBe(true);

        store.getState().setIsPlaying(false);
        expect(selectIsPlayingOrPending(store.getState())).toBe(false);
    });
});
