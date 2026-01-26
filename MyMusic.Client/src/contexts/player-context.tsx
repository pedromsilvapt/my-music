import * as React from 'react';
import {createContext, useContext, useRef} from 'react';
import {create, type StoreApi, useStore} from 'zustand'
import {immer} from 'zustand/middleware/immer';
import type {GetPlaylistSong, ListSongsItem} from "../model";

export type PlayerState = {
    queue: GetPlaylistSong[];
    current: PlayerCurrentSongState;
    autoplay: boolean;
    output: {
        volume: number;
        muted: boolean;
    }
}

export type PlayerCurrentSongState
    = { 'type': 'EMPTY' }
    | { 'type': 'LOADING', song: GetPlaylistSong }
    | { 'type': 'LOADED', song: GetPlaylistSong, time: number, duration: number, isPlaying: boolean }
    ;

export type PlayableItem = GetPlaylistSong | ListSongsItem; 

export type PlayerAction = {
    play: (songs: PlayableItem[]) => void;
    playNext: (songs: PlayableItem[]) => void;
    playLast: (songs: PlayableItem[]) => void;
    goForward: () => void;
    goBackward: () => void;
    goTo: (index: number) => void;
    load: (duration: number) => void;
    setIsPlaying: (isPlaying: boolean) => void;
    setVolume: (volume: number) => void;
    setMuted: (muted: boolean) => void;
    setCurrentTime: (currentTime: number) => void;
    setIsFavorite: (isFavorite: boolean) => void;
}

// Where should the tracks be placed on the queue
type QueueAnchor = 'NOW' // Should start playing them right away 
    | 'NEXT'  // Should add them right after the current song, to start afterward
    | 'LAST' // Should add them to the end of the queue

/**
 * Helper method to add songs to a queue.
 * @param state
 * @param songs
 * @param anchor
 */
function addToQueue(
    state: PlayerState & PlayerAction,
    songs: PlayableItem[],
    anchor: QueueAnchor,
) {
    // Assume by default we are adding to the end of the queue
    let anchorIndex = state.queue.length;

    if (anchor === 'NOW' || anchor === 'NEXT') {
        // If we have a current song, then we can grab the index right after it
        // Otherwise, it will be the end of the (empty) queue, same as last
        if (state.current.type == 'LOADED' || state.current.type == 'LOADING') {
            anchorIndex = state.current.song.order + 1;
        }
    }

    // If we are trying to add any songs that already belong to the queue, we need to remove them from their 
    // current positions on said queue
    const songsFromQueue = new Set(songs.filter(isPlaylistSong).map(s => s.order));

    if (songsFromQueue.size > 0) {
        // Make an exception for the currently playing playlist song, we won't remove that one
        state.queue = state.queue.filter((_, i) => i != anchorIndex - 1 && !songsFromQueue.has(i));

        // In case we removed any songs from the playlist before the currently playing song,
        // we need to adjust its "position" back by that amount
        anchorIndex -= Array.from(songsFromQueue).filter(i => i < anchorIndex - 1).length;
    }

    const songsToAdd = songs.map((song, i) => {
        if (!isPlaylistSong(song)) {
            return {
                ...song,
                order: anchorIndex + i,
                rank: 0, // TODO Calculate rank
            } as GetPlaylistSong;
        }

        return song;
    })

    state.queue.splice(anchorIndex, 0, ...songsToAdd);

    // Update the indexes of all songs on the queue
    for (let i = 0; i < state.queue.length; i++) {
        state.queue[i].order = i;
    }

    if (state.current.type === 'EMPTY' || anchor === 'NOW') {
        playAtIndex(state, anchorIndex);
    }
}

function playAtIndex(
    state: PlayerState & PlayerAction,
    index: number,
) {
    if (index < 0) {
        return;
    }

    if (index >= state.queue.length) {
        return;
    }

    const nextSong = state.queue[index];

    state.autoplay = true;
    state.current = {type: 'LOADING', song: nextSong};
}

function isPlaylistSong(song: PlayableItem | null | undefined): song is GetPlaylistSong {
    return song != null && 'order' in song;
}

function createPlayerStore(): StoreApi<PlayerState & PlayerAction> {
    return create(immer((set) => ({
        queue: [],
        autoplay: false,
        current: {type: 'EMPTY'},
        output: {
            volume: 1,
            muted: false,
        },
        play: (songs: (PlayableItem)[]) =>
            set(state => {
                addToQueue(state, songs, 'NOW');
            }),
        playNext: (songs: (PlayableItem)[]) =>
            set(state => {
                addToQueue(state, songs, 'NEXT');
            }),
        playLast: (songs: (PlayableItem)[]) =>
            set(state => {
                addToQueue(state, songs, 'LAST');
            }),
        goForward: () =>
            set(state => {
                if (state.current.type === 'LOADING' || state.current.type === 'LOADED') {
                    playAtIndex(state, state.current.song.order + 1);
                }
            }),
        goBackward: () =>
            set(state => {
                if (state.current.type === 'LOADING' || state.current.type === 'LOADED') {
                    playAtIndex(state, state.current.song.order - 1);
                }
            }),
        goTo: (index: number) =>
            set(state => {
                if (state.current.type === 'LOADING' || state.current.type === 'LOADED') {
                    playAtIndex(state, index);
                }
            }),
        load: (duration: number) =>
            set(state => {
                if (state.current.type === 'LOADING') {
                    state.current = {
                        type: 'LOADED',
                        song: state.current.song,
                        time: 0,
                        duration: duration,
                        isPlaying: state.autoplay
                    };
                }
            }),
        setIsPlaying: (isPlaying: boolean) =>
            set(state => {
                if (state.current.type === 'LOADED') {
                    state.current.isPlaying = isPlaying;
                    state.autoplay = false;
                }
            }),
        setVolume: (volume: number) =>
            set(state => {
                state.output.volume = volume;
            }),
        setMuted: (muted: boolean) =>
            set(state => {
                state.output.muted = muted;
            }),
        setCurrentTime: (currentTime: number) =>
            set(state => {
                if (state.current.type === 'LOADED') {
                    state.current.time = currentTime;
                }
            }),
        setIsFavorite: (isFavorite: boolean) =>
            set(state => {
                if (state.current.type === 'LOADED') {
                    state.current.song.isFavorite = isFavorite;
                }
            }),
    })));
}

export type PlayerStore = StoreApi<PlayerState & PlayerAction>;

export const PlayerContext = createContext<PlayerStore>(null!);

export default function PlayerProvider({children}: { children: React.ReactNode }) {
    const storeRef = useRef<PlayerStore>(null!);

    if (storeRef.current === null) {
        storeRef.current = createPlayerStore();
    }

    return (
        <PlayerContext.Provider value={storeRef.current}>
            {children}
        </PlayerContext.Provider>
    )
}

export function usePlayerContext(): PlayerState & PlayerAction;
export function usePlayerContext<U>(selector: (state: PlayerState & PlayerAction) => U): U;
export function usePlayerContext<U>(selector?: (state: PlayerState & PlayerAction) => U) {
    const store = useContext(PlayerContext)
    if (!store) {
        throw new Error('Missing StoreProvider')
    }
    if (selector != null) {
        return useStore(store, selector);
    }

    return useStore(store);
}