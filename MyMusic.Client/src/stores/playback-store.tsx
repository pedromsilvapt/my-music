/* eslint-disable react-refresh/only-export-components */
import * as React from 'react';
import {createContext, useContext, useRef} from 'react';
import {create, type StoreApi, useStore} from 'zustand';
import {immer} from 'zustand/middleware/immer';
import {useShallow} from 'zustand/react/shallow';
import type {GetPlaylistSongItem} from '../model';

export type PlaybackState = {
    current: PlayerCurrentSongState;
    autoplay: boolean;
    output: {
        volume: number;
        muted: boolean;
    };
    playbackKey: number;
    scrollToCurrentRequestId: number;
};

export type PlayerCurrentSongState =
    | { type: 'EMPTY' }
    | { type: 'LOADING'; song: GetPlaylistSongItem }
    | { type: 'LOADED'; song: GetPlaylistSongItem; time: number; duration: number; isPlaying: boolean };

type PlaybackActions = {
    setLoadingSong: (song: GetPlaylistSongItem, autoplay: boolean) => void;
    load: (duration: number) => void;
    setIsPlaying: (isPlaying: boolean) => void;
    setVolume: (volume: number) => void;
    setMuted: (muted: boolean) => void;
    setCurrentTime: (currentTime: number) => void;
    setIsFavorite: (isFavorite: boolean, songId?: number) => void;
    clear: () => void;
    incrementPlaybackKey: () => void;
    requestScrollToCurrent: () => void;
    initializeFromUser: (volume: number, isMuted: boolean) => void;
};

type PlaybackStore = PlaybackState & PlaybackActions;

export function createPlaybackStore(): StoreApi<PlaybackStore> {
    return create(
        immer<PlaybackStore>((set) => ({
            autoplay: false,
            current: {type: 'EMPTY'},
            output: {
                volume: 1,
                muted: false,
            },
            playbackKey: 0,
            scrollToCurrentRequestId: 0,
            setLoadingSong: (song, autoplay) =>
                set((state) => {
                    state.current = {type: 'LOADING', song};
                    state.autoplay = autoplay;
                    state.playbackKey += 1;
                }),
            load: (duration) =>
                set((state) => {
                    if (state.current.type === 'LOADING') {
                        state.current = {
                            type: 'LOADED',
                            song: state.current.song,
                            time: 0,
                            duration,
                            // Only playing once the audio actually starts; autoplay stays pending until then
                            isPlaying: false,
                        };
                    }
                }),
            setIsPlaying: (isPlaying) =>
                set((state) => {
                    if (state.current.type === 'LOADING') {
                        // Not loaded yet: remember whether it should start playing once it is
                        state.autoplay = isPlaying;
                    } else if (state.current.type === 'LOADED') {
                        state.current.isPlaying = isPlaying;
                        state.autoplay = false;
                    }
                }),
            setVolume: (volume) =>
                set((state) => {
                    state.output.volume = volume;
                }),
            setMuted: (muted) =>
                set((state) => {
                    state.output.muted = muted;
                }),
            initializeFromUser: (volume, isMuted) =>
                set((state) => {
                    state.output.volume = volume;
                    state.output.muted = isMuted;
                }),
            setCurrentTime: (currentTime) =>
                set((state) => {
                    if (state.current.type === 'LOADED') {
                        state.current.time = currentTime;
                    }
                }),
            setIsFavorite: (isFavorite, songId) =>
                set((state) => {
                    if (state.current.type === 'LOADED' && (!songId || state.current.song.id === songId)) {
                        state.current.song.isFavorite = isFavorite;
                    }
                }),
            clear: () =>
                set((state) => {
                    state.current = {type: 'EMPTY'};
                    state.autoplay = false;
                }),
            incrementPlaybackKey: () =>
                set((state) => {
                    state.playbackKey += 1;
                }),
            requestScrollToCurrent: () =>
                set((state) => {
                    state.scrollToCurrentRequestId += 1;
                }),
        }))
    );
}

/**
 * Whether the player is playing or about to autoplay the current song. Used to show the pause
 * control while a song is still loading or waiting for the audio to start.
 */
export function selectIsPlayingOrPending(state: PlaybackState): boolean {
    if (state.current.type === 'EMPTY') return false;
    if (state.current.type === 'LOADED' && state.current.isPlaying) return true;
    return state.autoplay;
}

export const PlaybackStoreContext = createContext<StoreApi<PlaybackStore>>(null!);

export function PlaybackStoreProvider({children}: { children: React.ReactNode }) {
    const storeRef = useRef<StoreApi<PlaybackStore>>(null!);

    if (storeRef.current === null) {
        storeRef.current = createPlaybackStore();
    }

    return (
        <PlaybackStoreContext.Provider value={storeRef.current}>
            {children}
        </PlaybackStoreContext.Provider>
    );
}

export function usePlaybackStore<U>(selector: (state: PlaybackStore) => U): U {
    const store = useContext(PlaybackStoreContext);
    if (!store) {
        throw new Error('Missing PlaybackStoreProvider');
    }
    return useStore(store, selector);
}

// Hook to select only needed actions/state with shallow comparison to avoid extra renders.
export const usePlaybackActions = <S extends object>(selector: (state: PlaybackStore) => S): S => {
    return usePlaybackStore(useShallow(selector));
};
