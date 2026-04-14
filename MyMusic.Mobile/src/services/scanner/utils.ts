export function fromEpochTimestamp(value: number | null | undefined): Date {
    if (!value) return new Date();
    const ms = value > 1e11 ? value : value * 1000;
    const d = new Date(ms);
    return isNaN(d.getTime()) ? new Date() : d;
}

export function yieldToUI(): Promise<void> {
    return new Promise((resolve) => {
        if (typeof requestIdleCallback === 'function') {
            requestIdleCallback(() => resolve(), {timeout: 10});
        } else {
            setTimeout(resolve, 0);
        }
    });
}

export function shouldExclude(filename: string, patterns: string[]): boolean {
    for (const pattern of patterns) {
        const regex = globToRegex(pattern);
        if (regex.test(filename)) return true;
        if (regex.test('/' + filename)) return true;
    }
    return false;
}

export function globToRegex(glob: string): RegExp {
    let regex = '^';
    for (const c of glob) {
        regex +=
            c === '*'
                ? '.*'
                : c === '?'
                    ? '.'
                    : c === '.'
                        ? '\\.'
                        : c === '/'
                            ? '[\\/]'
                            : c === '\\'
                                ? '[\\/]'
                                : c === '['
                                    ? '['
                                    : c === ']'
                                        ? ']'
                                        : escapeRegExp(c);
    }
    regex += '$';
    return new RegExp(regex, 'i');
}

export function escapeRegExp(str: string): string {
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
