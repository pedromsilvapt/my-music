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
