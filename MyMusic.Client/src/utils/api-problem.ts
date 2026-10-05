/**
 * An error response (4xx/5xx) of an Orval-generated call. Those calls resolve with the response
 * instead of throwing, so callers that need a rejection turn it into this error.
 */
export class ApiProblemError extends Error {
    readonly status: number;
    /** The reason given by the server's problem details, when it sent one. */
    readonly detail: string | undefined;

    constructor(status: number, detail?: string) {
        super(detail ?? `Request failed with status ${status}`);
        this.name = 'ApiProblemError';
        this.status = status;
        this.detail = detail;
    }
}

/** Returns the response, or throws an {@link ApiProblemError} when it is an error response. */
export function throwOnProblem<T extends { status: number; data?: unknown }>(response: T): T {
    if (response.status >= 400) {
        throw new ApiProblemError(response.status, (response.data as { detail?: string } | undefined)?.detail);
    }

    return response;
}
