import {createExclusionMatcher, excludedPathError} from '../exclusions';

/**
 * The test vectors shared with the CLI (`ExclusionMatcherTests`): both clients must exclude the same
 * paths for the same rules, so keep the two tables identical.
 */
const vectors: Array<[rule: string, path: string, excluded: boolean]> = [
    // A rule without a slash matches a name at any depth
    ['*.tmp', 'a.tmp', true],
    ['*.tmp', 'Artist/Album/a.tmp', true],
    ['*.tmp', 'a.tmp.mp3', false],
    ['*.TMP', 'a.tmp', true],
    ['Podcasts', 'Podcasts/ep1.mp3', true],
    ['Podcasts', 'Shows/Podcasts/ep1.mp3', true],
    ['Podcasts', 'Podcasts.mp3', false],
    ['Podcasts', 'My Podcasts/ep1.mp3', false],
    // A trailing slash only matches folders
    ['Podcasts/', 'Podcasts/ep1.mp3', true],
    ['Podcasts/', 'Shows/Podcasts/ep1.mp3', true],
    ['Podcasts/', 'Podcasts', false],
    ['Podcasts/', 'Podcasts/', true],
    // A rule with a slash is relative to the repository
    ['/Inbox', 'Inbox/a.mp3', true],
    ['/Inbox', 'Music/Inbox/a.mp3', false],
    ['Podcasts/*.mp3', 'Podcasts/ep1.mp3', true],
    ['Podcasts/*.mp3', 'Podcasts/2024/ep1.mp3', false],
    ['Podcasts/*.mp3', 'Shows/Podcasts/ep1.mp3', false],
    // `**` crosses folders
    ['Podcasts/**', 'Podcasts/2024/ep1.mp3', true],
    ['Podcasts/**', 'Podcasts', false],
    ['**/Live/*.mp3', 'Live/a.mp3', true],
    ['**/Live/*.mp3', 'Artist/Live/a.mp3', true],
    ['**/Live/*.mp3', 'Artist/Live/Disc 1/a.mp3', false],
    ['Artist/**/demo.mp3', 'Artist/demo.mp3', true],
    ['Artist/**/demo.mp3', 'Artist/A/B/demo.mp3', true],
    ['Artist/**/demo.mp3', 'Other/demo.mp3', false],
    ['**/.*', '.hidden.mp3', true],
    ['**/.*', 'Artist/.hidden.mp3', true],
    ['**/.*', '.trash/a.mp3', true],
    ['**/.*', 'Artist/a.b.mp3', false],
    ['**/Thumbs.db', 'Thumbs.db', true],
    // `?` is one character of a name
    ['track?.mp3', 'track1.mp3', true],
    ['track?.mp3', 'track10.mp3', false],
    ['track?/a.mp3', 'track/a.mp3', false],
    // Everything else is literal
    ['Song [Live].mp3', 'Song [Live].mp3', true],
    ['Song [Live].mp3', 'Song L.mp3', false],
    ['a+b (1).mp3', 'a+b (1).mp3', true],
    ['a+b (1).mp3', 'aab (1).mp3', false],
    // Comments, blank rules and surrounding spaces
    ['# comment', '# comment', false],
    ['', 'a.mp3', false],
    ['   ', 'a.mp3', false],
    ['/', 'a.mp3', false],
    ['  *.tmp  ', 'a.tmp', true],
    // Backslashes are path separators
    ['Podcasts\\*.mp3', 'Podcasts/ep1.mp3', true],
    ['*.tmp', 'Artist\\a.tmp', true],
];

describe('createExclusionMatcher', () => {
    test.each(vectors)('rule %p on path %p excludes: %p', (rule, path, excluded) => {
        const match = createExclusionMatcher([rule])(path);

        expect(match !== null).toBe(excluded);
    });

    test('returns the first rule that matches, trimmed', () => {
        const isExcluded = createExclusionMatcher(['*.tmp', ' Podcasts/ ', '**/ep1.mp3']);

        expect(isExcluded('Podcasts/ep1.mp3')).toBe('Podcasts/');
    });

    test('excludes nothing without rules', () => {
        expect(createExclusionMatcher([])('Artist/song.mp3')).toBeNull();
    });
});

test('excludedPathError names the rule', () => {
    expect(excludedPathError('*.tmp')).toBe("Path is excluded from sync by the rule '*.tmp'");
});
