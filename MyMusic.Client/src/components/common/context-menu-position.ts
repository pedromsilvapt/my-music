export interface Point {
    x: number;
    y: number;
}

export interface Size {
    width: number;
    height: number;
}

/**
 * Positions a menu of the given size at the requested point, flipping it to the left/top of the point
 * when it would otherwise overflow the viewport.
 */
export function constrainToViewport(position: Point, size: Size, viewport: Size): { left: number; top: number } {
    let left = position.x;
    let top = position.y;

    if (left + size.width > viewport.width) {
        left = Math.max(0, left - size.width);
    }

    if (top + size.height > viewport.height) {
        top = Math.max(0, top - size.height);
    }

    return {left, top};
}
