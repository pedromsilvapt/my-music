/** Returns the exclusion rule a path relative to the repository matches, or null when it is not excluded. */
export type ExclusionMatcher = (relativePath: string) => string | null;

/** The error reported for an action on a path that an exclusion rule matches. */
export function excludedPathError(rule: string): string {
    return `Path is excluded from sync by the rule '${rule}'`;
}

/**
 * Builds the matcher of a list of exclusion rules, in a gitignore-like syntax (see "Exclusion Rules" in
 * docs/development/sync.md). Mirrors the CLI's ExclusionMatcher: both are pinned by the same test vectors.
 * A folder is tested with a trailing `/`.
 */
export function createExclusionMatcher(patterns: string[]): ExclusionMatcher {
    const rules: Array<{ pattern: string; regex: RegExp }> = [];

    for (const pattern of patterns) {
        const regex = ruleToRegex(pattern);
        if (regex) {
            rules.push({ pattern: pattern.trim(), regex });
        }
    }

    return (relativePath: string) => {
        const path = relativePath.replace(/\\/g, '/').replace(/^\/+/, '');

        for (const rule of rules) {
            if (rule.regex.test(path)) {
                return rule.pattern;
            }
        }

        return null;
    };
}

function ruleToRegex(rule: string): RegExp | null {
    let pattern = rule.trim().replace(/\\/g, '/');
    if (pattern === '' || pattern.startsWith('#')) {
        return null;
    }

    // A trailing slash only matches folders, so something has to follow it in the path
    const folderOnly = pattern.endsWith('/');
    pattern = pattern.replace(/\/+$/, '');

    // A rule with a slash is relative to the repository; otherwise it matches a name at any depth
    const anchored = pattern.includes('/');
    pattern = pattern.replace(/^\/+/, '');
    if (pattern === '') {
        return null;
    }

    let body = '';
    let i = 0;
    while (i < pattern.length) {
        const atSegmentStart = i === 0 || pattern[i - 1] === '/';

        if (atSegmentStart && pattern.startsWith('**/', i)) {
            body += '(?:[\\s\\S]*/)?';
            i += 3;
        } else if (pattern.startsWith('/**', i) && i + 3 === pattern.length) {
            body += '/[\\s\\S]*';
            i += 3;
        } else if (pattern[i] === '*') {
            body += '[^/]*';
            i++;
        } else if (pattern[i] === '?') {
            body += '[^/]';
            i++;
        } else {
            body += pattern[i].replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
            i++;
        }
    }

    const prefix = anchored ? '^' : '^(?:[\\s\\S]*/)?';
    const suffix = folderOnly ? '/[\\s\\S]*$' : '(?:/[\\s\\S]*)?$';

    return new RegExp(prefix + body + suffix, 'i');
}
