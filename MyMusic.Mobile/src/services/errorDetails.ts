export interface ErrorDetails {
    title?: string;
    status?: number;
    message: string;
    url?: string;
    responseBody?: string;
    stack?: string;
    cause?: string;
    validationErrors?: Record<string, string[]>;
}

/** Pretty-prints a JSON string; any other text is returned as it is. */
export function formatJson(value: string): string {
    try {
        return JSON.stringify(JSON.parse(value), null, 2);
    } catch {
        return value;
    }
}

/**
 * Renders an error as plain text, with the same fields and order as the ErrorDisplay component.
 * Fields with no value are left out.
 */
export function formatErrorDetails(error: ErrorDetails): string {
    const lines: string[] = [];

    if (error.status !== undefined) lines.push(`Status: ${error.status}`);
    if (error.title) lines.push(`Title: ${error.title}`);
    if (error.message) lines.push(`Message: ${error.message}`);
    if (error.url) lines.push(`URL: ${error.url}`);
    if (error.validationErrors && Object.keys(error.validationErrors).length > 0) {
        lines.push('Validation Errors:');
        for (const [field, messages] of Object.entries(error.validationErrors)) {
            for (const msg of messages) {
                lines.push(`  ${field}: ${msg}`);
            }
        }
    }
    if (error.responseBody) lines.push('Response Body:', formatJson(error.responseBody));
    if (error.stack) lines.push('Stack Trace:', error.stack);
    if (error.cause) lines.push(`Caused By: ${error.cause}`);

    return lines.length > 0 ? lines.join('\n') : 'No details available';
}
