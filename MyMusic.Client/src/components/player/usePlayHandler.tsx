import {useCallback} from "react";
import type {PlayableItem, PlayerAction} from "../../contexts/player-context.tsx";

export type PlayHandler = (rows: PlayableItem[], ev: React.MouseEvent<Element, MouseEvent>) => void;

export function usePlayHandler(playerActions: PlayerAction, nowPlaying: boolean): PlayHandler {
    return useCallback((rows: PlayableItem[], ev: React.MouseEvent<Element, MouseEvent>) => {
        ev.stopPropagation();

        if (nowPlaying && rows.length == 1 && 'order' in rows[0]) {
            playerActions.goTo(rows[0].order);
        } else if (ev.ctrlKey) {
            playerActions.playLast(rows);
        } else if (ev.shiftKey) {
            playerActions.playNext(rows);
        } else {
            playerActions.play(rows);
        }
    }, [playerActions]);
}