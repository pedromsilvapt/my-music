import {describe, expect, it} from 'vitest';
import {extractFieldName, extractListContext, extractStringContext} from './filter-completion-context';

describe('extractStringContext', () => {
    it.each([
        ['device.name = "', 'device.name', ''],
        ['device.name = "ab', 'device.name', 'ab'],
        ['title contains "foo bar', 'title', 'foo bar'],
        ['artists[any].name = "x', 'artists[any].name', 'x'],
        ['title = "a" and device.name != "b', 'device.name', 'b'],
        ['title == "ab', 'title', 'ab'],
        ['title <> "ab', 'title', 'ab'],
        ['rating >= "4', 'rating', '4'],
    ])('detects the single value string in %s', (text, field, partialValue) => {
        expect(extractStringContext(text)).toEqual({field, partialValue});
    });

    it.each([
        ['device.name notIn ["', 'device.name', ''],
        ['device.name in ["ab', 'device.name', 'ab'],
        ['device.name notIn ["a", "b', 'device.name', 'b'],
        ['device.name notIn ["a","b", "', 'device.name', ''],
        ['device.name notIn [ "a b" , "c d', 'device.name', 'c d'],
        ['device.name notIn ["a \\" b", "c', 'device.name', 'c'],
        ['artists[any].name notIn ["x', 'artists[any].name', 'x'],
        ['title = "a" and device.name notIn ["b', 'device.name', 'b'],
        ['genre.name in ["a"] or device.name notIn ["b', 'device.name', 'b'],
    ])('detects the list item string in %s', (text, field, partialValue) => {
        expect(extractStringContext(text)).toEqual({field, partialValue});
    });

    it.each([
        'device.name notIn [',
        'device.name notIn ["a"',
        'device.name notIn ["a", ',
        'device.name notIn ["a"] ',
        'device.name notIn ["a"] and ',
        'device.name = "a"',
        'artists[',
    ])('returns null outside of a string in %s', (text) => {
        expect(extractStringContext(text)).toBeNull();
    });
});

describe('extractListContext', () => {
    it.each([
        ['rating in [', 'rating'],
        ['rating in [ ', 'rating'],
        ['rating in ["a", ', 'rating'],
        ['rating in ["a"', 'rating'],
        ['rating in [1, 2', 'rating'],
        ['rating notIn [1, ', 'rating'],
        ['title = "a" and rating in [', 'rating'],
    ])('detects the cursor between the items of %s', (text, field) => {
        expect(extractListContext(text)).toEqual({field, partialValue: null});
    });

    it('reports the partial value when inside a string', () => {
        expect(extractListContext('rating in ["a", "b')).toEqual({field: 'rating', partialValue: 'b'});
    });

    it.each([
        'artists[',
        'artists[any].name = ',
        'rating in ["a"]',
        'rating in ["a"] and title = ',
        'rating = "a',
        'origin [',
    ])('returns null outside of a list in %s', (text) => {
        expect(extractListContext(text)).toBeNull();
    });
});

describe('extractFieldName', () => {
    it.each([
        ['explicit = ', 'explicit'],
        ['rating >= ', 'rating'],
        ['explicit == ', 'explicit'],
        ['durationCategory <> ', 'durationCategory'],
        ['explicit != ', 'explicit'],
        ['rating <= ', 'rating'],
        ['title ~ ', 'title'],
        ['rating between ', 'rating'],
        ['origin in ', 'origin'],
        ['artists[any].name contains ', 'artists[any].name'],
        ['title = "a" and rating in ', 'rating'],
        ['title = "a" and explicit = ', 'explicit'],
    ])('returns the field of the condition under the cursor in %s', (text, field) => {
        expect(extractFieldName(text)).toBe(field);
    });

    it('returns null when the text does not end in an operator', () => {
        expect(extractFieldName('title = "a" and ')).toBeNull();
    });
});
