import {useRef, useCallback} from 'react';

export interface LongPressHandlers {
    onContextMenu: (e: React.MouseEvent) => void;
    onTouchStart: (e: React.TouchEvent) => void;
    onTouchEnd: (e: React.TouchEvent) => void;
    onTouchMove: (e: React.TouchEvent) => void;
    onTouchCancel: (e: React.TouchEvent) => void;
}

export interface UseLongPressOptions {
    delay?: number;
    onCancel?: () => void;
}

export function useLongPress(
    callback: (e: React.SyntheticEvent) => void,
    options: UseLongPressOptions = {}
): LongPressHandlers {
    const {delay = 500, onCancel} = options;
    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const isLongPressTriggeredRef = useRef(false);

    const clearTimer = useCallback(() => {
        if (timerRef.current != null) {
            clearTimeout(timerRef.current);
            timerRef.current = null;
        }
    }, []);

    const handleTouchStart = useCallback((e: React.TouchEvent) => {
        isLongPressTriggeredRef.current = false;
        clearTimer();

        timerRef.current = setTimeout(() => {
            isLongPressTriggeredRef.current = true;
            callback(e);
        }, delay);
    }, [callback, delay, clearTimer]);

    const handleTouchEnd = useCallback((_e: React.TouchEvent) => {
        clearTimer();
        if (!isLongPressTriggeredRef.current) {
            onCancel?.();
        }
        isLongPressTriggeredRef.current = false;
    }, [clearTimer, onCancel]);

    const handleTouchMove = useCallback((e: React.TouchEvent) => {
        if (timerRef.current != null) {
            const touch = e.touches[0];
            const startX = (e.target as HTMLElement).getBoundingClientRect().left;
            const startY = (e.target as HTMLElement).getBoundingClientRect().top;
            const moveThreshold = 10;

            if (Math.abs(touch.clientX - startX) > moveThreshold || Math.abs(touch.clientY - startY) > moveThreshold) {
                clearTimer();
            }
        }
    }, [clearTimer]);

    const handleTouchCancel = useCallback(() => {
        clearTimer();
        isLongPressTriggeredRef.current = false;
    }, [clearTimer]);

    const handleContextMenu = useCallback((e: React.MouseEvent) => {
        e.preventDefault();
        callback(e);
    }, [callback]);

    return {
        onContextMenu: handleContextMenu,
        onTouchStart: handleTouchStart,
        onTouchEnd: handleTouchEnd,
        onTouchMove: handleTouchMove,
        onTouchCancel: handleTouchCancel,
    };
}