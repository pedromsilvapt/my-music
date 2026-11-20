import {type RefObject, useEffect, useState} from "react";

/**
 * Tracks the `clientWidth` of an element: the width available to its content, which excludes its
 * vertical scroll bar. Mantine's `useElementSize` reports the border box instead, which includes it.
 * Resize observers also fire when a scroll bar appears or disappears, since it changes the content box.
 */
export function useClientWidth(ref: RefObject<HTMLElement | null>): number {
    const [width, setWidth] = useState(0);

    useEffect(() => {
        const element = ref.current;
        if (!element) return;

        let frame = 0;
        const observer = new ResizeObserver(() => {
            cancelAnimationFrame(frame);
            frame = requestAnimationFrame(() => setWidth(element.clientWidth));
        });
        observer.observe(element);

        return () => {
            observer.disconnect();
            cancelAnimationFrame(frame);
        };
    }, [ref]);

    return width;
}
