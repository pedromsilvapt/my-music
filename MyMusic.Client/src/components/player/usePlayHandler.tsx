import {useCallback} from "react";
import type {PlayableItem, PlayerAction} from "../../contexts/player-context.tsx";

export type PlayHandler = (rows: PlayableItem[], ev: React.MouseEvent<Element, MouseEvent>) => void;

export function usePlayHandler(playerStore: PlayerAction): PlayHandler {
    return useCallback((rows: PlayableItem[], ev: React.MouseEvent<Element, MouseEvent>) => {
        ev.stopPropagation();
        if (ev.ctrlKey) {
            playerStore.playLast(rows);
        } else if (ev.shiftKey) {
            playerStore.playNext(rows);
        } else {
            playerStore.play(rows);
        }
    }, [playerStore]);
}