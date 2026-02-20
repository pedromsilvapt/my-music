import {useCallback} from 'react';
import {usePlayerNavigation} from '../../hooks/use-player-navigation';
import type {PlayableItem} from '../../hooks/use-queue';
import {useQueueMutations} from '../../hooks/use-queue';

export type PlayHandler = (rows: PlayableItem[], ev: React.MouseEvent<Element, MouseEvent>) => void;

export function usePlayHandler(nowPlaying: boolean = false): PlayHandler {
    const {play, playNext, playLast} = useQueueMutations();
    const {goTo} = usePlayerNavigation();

    return useCallback((rows: PlayableItem[], ev: React.MouseEvent<Element, MouseEvent>) => {
        ev.stopPropagation();

        if (nowPlaying && rows.length === 1 && 'order' in rows[0]) {
            goTo(rows[0].order);
        } else if (ev.ctrlKey) {
            playLast(rows);
        } else if (ev.shiftKey) {
            playNext(rows);
        } else {
            play(rows);
        }
    }, [nowPlaying, goTo, play, playNext, playLast]);
}
