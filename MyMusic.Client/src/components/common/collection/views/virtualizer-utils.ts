import type {Virtualizer} from "@tanstack/react-virtual";

/**
 * Whether the item at `index` is fully inside the scroll viewport. Unlike `getVirtualItems()`,
 * this ignores overscan and partially visible items.
 */
export function isIndexFullyInViewport(virtualizer: Virtualizer<HTMLDivElement, Element>, index: number): boolean {
    const measurement = virtualizer.measurementsCache[index];
    if (!measurement) return false;

    const viewportStart = virtualizer.scrollOffset ?? 0;
    const viewportEnd = viewportStart + (virtualizer.scrollRect?.height ?? 0);
    return measurement.start >= viewportStart && measurement.end <= viewportEnd;
}
