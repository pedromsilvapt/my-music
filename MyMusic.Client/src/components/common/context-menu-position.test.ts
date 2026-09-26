import {describe, expect, it} from 'vitest';
import {constrainToViewport} from './context-menu-position';

const viewport = {width: 1000, height: 800};

describe('constrainToViewport', () => {
    it('keeps the position when the menu fits', () => {
        expect(constrainToViewport({x: 100, y: 100}, {width: 200, height: 300}, viewport))
            .toEqual({left: 100, top: 100});
    });

    it('flips left when the menu overflows the right edge', () => {
        expect(constrainToViewport({x: 900, y: 100}, {width: 200, height: 300}, viewport))
            .toEqual({left: 700, top: 100});
    });

    it('flips up when the menu overflows the bottom edge', () => {
        expect(constrainToViewport({x: 100, y: 700}, {width: 200, height: 300}, viewport))
            .toEqual({left: 100, top: 400});
    });

    it('flips both ways when the menu overflows the bottom-right corner', () => {
        expect(constrainToViewport({x: 900, y: 700}, {width: 200, height: 300}, viewport))
            .toEqual({left: 700, top: 400});
    });

    it('clamps to zero when the menu does not fit on either side', () => {
        expect(constrainToViewport({x: 100, y: 200}, {width: 1200, height: 900}, viewport))
            .toEqual({left: 0, top: 0});
    });

    it('keeps the position when the menu has not been measured yet', () => {
        expect(constrainToViewport({x: 999, y: 799}, {width: 0, height: 0}, viewport))
            .toEqual({left: 999, top: 799});
    });
});
