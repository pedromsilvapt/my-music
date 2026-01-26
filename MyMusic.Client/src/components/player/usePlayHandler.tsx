import {useCallback} from "react";
import type {PlayableItem, PlayerAction, PlayerState} from "../../contexts/player-context.tsx";

export function usePlayHandler(playerStore: PlayerState & PlayerAction) {
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