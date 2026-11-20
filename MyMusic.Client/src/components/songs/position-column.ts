// Horizontal padding of a table cell (Mantine's default `xs` spacing on each side)
const CELL_PADDING = 2 * 10;
// Widths of the `#` and of one tabular digit at the cell's font size (16px), measured in the browser and rounded up
const HASH_WIDTH = 16;
const DIGIT_WIDTH = 10;

/**
 * Number of digits needed to write the given position (at least one).
 */
export function countDigits(position: number): number {
    return Math.max(1, Math.floor(Math.abs(position)).toString().length);
}

/**
 * Width, in pixels, of the queue's position column, wide enough to show `#` followed by
 * a position with the given number of digits.
 */
export function getPositionColumnWidth(digits: number): number {
    return CELL_PADDING + HASH_WIDTH + Math.max(1, digits) * DIGIT_WIDTH;
}
