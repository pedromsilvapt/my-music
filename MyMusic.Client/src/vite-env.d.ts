/// <reference types="vite/client" />

interface FeedboxOpenOptions {
    title?: string;
    labels?: string[];
    meta?: Record<string, string>;
}

interface FeedboxApi {
    open(options?: FeedboxOpenOptions): void;
    close(): void;
    on(event: "submitted", handler: (issue: { id: string; url?: string }) => void): () => void;
}

interface Window {
    /** Feedbox feedback widget, present once its script has loaded. */
    Feedbox?: FeedboxApi;
}
