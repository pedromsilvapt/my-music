import {afterEach, describe, expect, it, vi} from 'vitest';
import {randomUuid} from './random-uuid';

const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

describe('randomUuid', () => {
    afterEach(() => {
        vi.restoreAllMocks();
        vi.unstubAllGlobals();
    });

    it('uses crypto.randomUUID when available', () => {
        const spy = vi.spyOn(crypto, 'randomUUID').mockReturnValue('00000000-0000-4000-8000-000000000000');

        expect(randomUuid()).toBe('00000000-0000-4000-8000-000000000000');
        expect(spy).toHaveBeenCalled();
    });

    it('generates a v4 UUID without crypto.randomUUID (insecure contexts, e.g. plain http on a non-localhost host)', () => {
        const realCrypto = globalThis.crypto;
        const getRandomValues = vi.fn(<T extends ArrayBufferView>(array: T) => realCrypto.getRandomValues(array as never) as T);
        vi.stubGlobal('crypto', {getRandomValues});

        const first = randomUuid();
        const second = randomUuid();

        expect(first).toMatch(UUID_V4);
        expect(second).toMatch(UUID_V4);
        expect(first).not.toBe(second);
        expect(getRandomValues).toHaveBeenCalledTimes(2);
    });
});
