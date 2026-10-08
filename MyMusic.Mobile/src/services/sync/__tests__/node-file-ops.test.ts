import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { NodeFileOps } from '../../../../test-cli/node-file-ops';

/**
 * The test vectors shared with the server (ChecksumServiceSpecs) and the CLI (CliFileOpsTests): the
 * server compares these checksums with its own, so every client must produce the same strings.
 * A wrong byte order of the 128-bit digest would show up here.
 */
const VECTORS: Array<[name: string, content: Uint8Array, expected: string]> = [
    ['empty', new Uint8Array(0), 'maoG0wFHmNhgAcMkRo1Jfw=='],
    ['abc', new TextEncoder().encode('abc'), 'BrBatnM6YYV4r1+UiS85UA=='],
    ['1 MiB of i % 251', Uint8Array.from({ length: 1024 * 1024 }, (_, i) => i % 251), 'U3ONmAmMq7puDXrDa4wQ/w=='],
];

describe('NodeFileOps.computeChecksum', () => {
    let directory: string;

    beforeAll(() => {
        directory = fs.mkdtempSync(path.join(os.tmpdir(), 'mymusic-checksum-'));
    });

    afterAll(() => {
        fs.rmSync(directory, { recursive: true, force: true });
    });

    test.each(VECTORS)('XxHash128 matches the shared vector: %s', async (_name, content, expected) => {
        const filePath = path.join(directory, 'song.mp3');
        fs.writeFileSync(filePath, content);

        expect(await new NodeFileOps().computeChecksum(filePath, 'XxHash128')).toBe(expected);
    });

    test('an unknown algorithm is rejected', async () => {
        await expect(new NodeFileOps().computeChecksum(path.join(directory, 'song.mp3'), 'Sha256')).rejects.toThrow('Unsupported checksum algorithm: Sha256');
    });
});

describe('NodeFileOps.setModificationTime', () => {
    test('changes the modified time of the file', async () => {
        const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'mymusic-mtime-'));
        const filePath = path.join(directory, 'song.mp3');
        fs.writeFileSync(filePath, 'abc');
        const date = new Date('2020-01-02T03:04:05Z');

        try {
            const fileOps = new NodeFileOps();
            await fileOps.setModificationTime(filePath, date);

            expect(fileOps.getModificationTime(filePath)).toEqual(date);
        } finally {
            fs.rmSync(directory, { recursive: true, force: true });
        }
    });
});
