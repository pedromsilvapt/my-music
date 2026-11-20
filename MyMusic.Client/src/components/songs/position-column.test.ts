import {describe, expect, it} from 'vitest';
import {countDigits, getPositionColumnWidth} from './position-column';

describe('countDigits', () => {
    it.each([
        [0, 1],
        [7, 1],
        [10, 2],
        [999, 3],
        [4278, 4],
        [10000, 5],
    ])('counts %i as %i digit(s)', (position, digits) => {
        expect(countDigits(position)).toBe(digits);
    });
});

describe('getPositionColumnWidth', () => {
    // Content widths of `#8`, `#4278` and `#88888` measured in the browser, at the cell's font size
    it.each([
        [1, 24],
        [4, 54],
        [5, 64],
    ])('fits a %i digit position, whose text is %ipx wide, inside the cell padding', (digits, textWidth) => {
        expect(getPositionColumnWidth(digits)).toBeGreaterThanOrEqual(textWidth + 20);
    });

    it('grows with every extra digit', () => {
        const widths = [1, 2, 3, 4, 5].map(getPositionColumnWidth);

        for (let i = 1; i < widths.length; i++) {
            expect(widths[i]).toBeGreaterThan(widths[i - 1]);
        }
    });

    it('never goes below the single digit width', () => {
        expect(getPositionColumnWidth(0)).toBe(getPositionColumnWidth(1));
    });
});
