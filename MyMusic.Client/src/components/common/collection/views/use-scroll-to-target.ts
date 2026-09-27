import type {Virtualizer} from "@tanstack/react-virtual";
import type React from "react";
import {useEffect, useRef} from "react";
import type {ScrollTarget} from "../scroll-request.ts";
import {isIndexFullyInViewport} from "./virtualizer-utils.ts";

/** Without a `scrollend` event, a scroll counts as finished after this long without scroll events */
const SCROLL_SETTLE_MS = 150;

/**
 * Smooth-scrolls to `target` once per request id, then calls `onScrolled` with that id when the
 * scroll has finished (or right away when the target is already fully in view). Later changes to
 * the target's index, e.g. from sorting or filtering, don't scroll again.
 */
export function useScrollToTarget(
    virtualizer: Virtualizer<HTMLDivElement, Element>,
    scrollElementRef: React.RefObject<HTMLDivElement | null>,
    target: ScrollTarget | undefined,
    onScrolled: (id: number) => void
) {
    const indexRef = useRef(target?.index);
    indexRef.current = target?.index;
    const onScrolledRef = useRef(onScrolled);
    onScrolledRef.current = onScrolled;

    const id = target?.id;

    useEffect(() => {
        const index = indexRef.current;
        if (id == null || index == null) return;

        if (isIndexFullyInViewport(virtualizer, index)) {
            onScrolledRef.current(id);
            return;
        }

        const scrollElement = scrollElementRef.current;
        let settleTimeout: ReturnType<typeof setTimeout> | undefined;

        const finish = () => {
            cleanup();
            onScrolledRef.current(id);
        };
        const restartSettleTimeout = () => {
            clearTimeout(settleTimeout);
            settleTimeout = setTimeout(finish, SCROLL_SETTLE_MS);
        };
        const cleanup = () => {
            cancelAnimationFrame(frame);
            clearTimeout(settleTimeout);
            scrollElement?.removeEventListener('scroll', restartSettleTimeout);
            scrollElement?.removeEventListener('scrollend', finish);
        };

        const frame = requestAnimationFrame(() => {
            scrollElement?.addEventListener('scroll', restartSettleTimeout, {passive: true});
            scrollElement?.addEventListener('scrollend', finish);
            restartSettleTimeout();
            virtualizer.scrollToIndex(index, {align: 'center', behavior: 'smooth'});
        });

        return cleanup;
    }, [id, virtualizer, scrollElementRef]);
}
