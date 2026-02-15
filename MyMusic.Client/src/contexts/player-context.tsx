import * as React from 'react';
import {createContext, useContext, useMemo, useRef} from 'react';
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
    removeFromQueue: (indices: number[]) => void;
    reorderQueue: (fromIndex: number, toIndex: number) => void;
    reorderQueueBatch: (reorders: { fromIndex: number; toIndex: number }[]) => void;
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
    const songsFromQueue = new Set(songs.filter(isPlaylistSong).map(s => s.id));

    if (songsFromQueue.size > 0) {
        // Make an exception for the currently playing playlist song, we won't remove that one
        state.queue = state.queue.filter((s, i) => i != anchorIndex - 1 && !songsFromQueue.has(s.id));

        // In case we removed any songs from the playlist before the currently playing song,
        // we need to adjust its "position" back by that amount
        anchorIndex -= Array.from(songsFromQueue).filter(i => i < anchorIndex - 1).length;
    }

    const songsToAdd = songs.map((song, i) => {
        if (!isPlaylistSong(song)) {
            return {
                ...song,
                order: anchorIndex + i,
                addedAtPlaylist: new Date().toISOString(),
            } as GetPlaylistSong;
        }

        return {...song};
    })

    state.queue.splice(anchorIndex, 0, ...songsToAdd);

    // Update the indexes of all songs on the queue
    for (let i = 0; i < state.queue.length; i++) {
        state.queue[i] = {...state.queue[i], order: i};
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
        removeFromQueue: (indices: number[]) =>
            set(state => {
                const indexSet = new Set(indices);

                let currentSongId: number | null = null;
                let currentSongOrder = -1;

                if (state.current.type === 'LOADED' || state.current.type === 'LOADING') {
                    currentSongId = state.current.song.id;
                    currentSongOrder = state.current.song.order;
                }

                const isCurrentSongRemoved = currentSongOrder >= 0 && indexSet.has(currentSongOrder);
                
                state.queue = state.queue.filter((_, i) => !indexSet.has(i));
                for (let i = 0; i < state.queue.length; i++) {
                    state.queue[i].order = i;
                }

                if (isCurrentSongRemoved) {
                    if (state.queue.length > 0) {
                        let nextIndex = currentSongOrder;
                        if (nextIndex >= state.queue.length) {
                            nextIndex = state.queue.length - 1;
                        }
                        const nextSong = state.queue[nextIndex];
                        state.autoplay = true;
                        state.current = {type: 'LOADING', song: nextSong};
                    } else {
                        state.current = {type: 'EMPTY'};
                    }
                } else if (currentSongId && (state.current.type === 'LOADED' || state.current.type === 'LOADING')) {
                    const newIndex = state.queue.findIndex(s => s.id === currentSongId);
                    if (newIndex !== -1) {
                        state.current.song = state.queue[newIndex];
                    }
                }
            }),
        reorderQueue: (fromIndex: number, toIndex: number) =>
            set(state => {
                let currentSongId: number | null = null;

                if (state.current.type === 'LOADED' || state.current.type === 'LOADING') {
                    currentSongId = state.current.song.id;
                }

                const [moved] = state.queue.splice(fromIndex, 1);
                state.queue.splice(toIndex, 0, moved);
                for (let i = 0; i < state.queue.length; i++) {
                    state.queue[i].order = i;
                }

                if (currentSongId && (state.current.type === 'LOADED' || state.current.type === 'LOADING')) {
                    const newIndex = state.queue.findIndex(s => s.id === currentSongId);
                    if (newIndex !== -1) {
                        state.current.song = state.queue[newIndex];
                    }
                }
            }),
        reorderQueueBatch: (reorders: { fromIndex: number; toIndex: number }[]) =>
            set(state => {
                if (reorders.length === 0) return;

                let currentSongId: number | null = null;

                if (state.current.type === 'LOADED' || state.current.type === 'LOADING') {
                    currentSongId = state.current.song.id;
                }

                const sortedReorders = [...reorders].sort((a, b) => a.fromIndex - b.fromIndex);

                const itemsToMove: GetPlaylistSong[] = [];
                let indexOffset = 0;

                for (const {fromIndex} of sortedReorders) {
                    const adjustedFromIndex = fromIndex - indexOffset;
                    const [moved] = state.queue.splice(adjustedFromIndex, 1);
                    itemsToMove.push(moved);
                    indexOffset++;
                }

                const sortedByTarget = [...reorders].sort((a, b) => a.toIndex - b.toIndex);
                let insertIndex = sortedByTarget[0].toIndex;

                for (const item of itemsToMove) {
                    state.queue.splice(insertIndex, 0, item);
                    insertIndex++;
                }

                for (let i = 0; i < state.queue.length; i++) {
                    state.queue[i].order = i;
                }

                if (currentSongId && (state.current.type === 'LOADED' || state.current.type === 'LOADING')) {
                    const newIndex = state.queue.findIndex(s => s.id === currentSongId);
                    if (newIndex !== -1) {
                        state.current.song = state.queue[newIndex];
                    }
                }
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
export function usePlayerContext(selector?: (state: PlayerState & PlayerAction) => unknown): unknown {
    const store = useContext(PlayerContext);
    
    if (!store) {
        throw new Error('Missing StoreProvider');
    }

    if (selector) {
        return useStore(store, selector);
    }

    return useStore(store);
}

export function usePlayerActions(): PlayerAction {
    const play = usePlayerContext(state => state.play);
    const playNext = usePlayerContext(state => state.playNext);
    const playLast = usePlayerContext(state => state.playLast);
    const removeFromQueue = usePlayerContext(state => state.removeFromQueue);
    const reorderQueue = usePlayerContext(state => state.reorderQueue);
    const reorderQueueBatch = usePlayerContext(state => state.reorderQueueBatch);
    const goForward = usePlayerContext(state => state.goForward);
    const goBackward = usePlayerContext(state => state.goBackward);
    const goTo = usePlayerContext(state => state.goTo);
    const load = usePlayerContext(state => state.load);
    const setIsPlaying = usePlayerContext(state => state.setIsPlaying);
    const setVolume = usePlayerContext(state => state.setVolume);
    const setMuted = usePlayerContext(state => state.setMuted);
    const setCurrentTime = usePlayerContext(state => state.setCurrentTime);
    const setIsFavorite = usePlayerContext(state => state.setIsFavorite);

    return useMemo(() => ({
        play,
        playNext,
        playLast,
        removeFromQueue,
        reorderQueue,
        reorderQueueBatch,
        goForward,
        goBackward,
        goTo,
        load,
        setIsPlaying,
        setVolume,
        setMuted,
        setCurrentTime,
        setIsFavorite,
    }), [play, playNext, playLast, removeFromQueue, reorderQueue, reorderQueueBatch, goForward, goBackward, goTo, load, setIsPlaying, setVolume, setMuted, setCurrentTime, setIsFavorite]);
}