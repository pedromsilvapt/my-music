import {formatErrorDetails} from '../errorDetails';

describe('formatErrorDetails', () => {
    test('lists every field in the order of the error screen', () => {
        const text = formatErrorDetails({
            status: 500,
            title: 'Internal Server Error',
            message: 'Something broke',
            url: 'http://server/api/sync',
            validationErrors: {Name: ['is required', 'is too short'], Path: ['is invalid']},
            responseBody: '{"detail":"boom"}',
            stack: 'Error: Something broke\n    at sync (sync.ts:10)',
            cause: 'Network request failed',
        });

        expect(text).toBe([
            'Status: 500',
            'Title: Internal Server Error',
            'Message: Something broke',
            'URL: http://server/api/sync',
            'Validation Errors:',
            '  Name: is required',
            '  Name: is too short',
            '  Path: is invalid',
            'Response Body:',
            '{',
            '  "detail": "boom"',
            '}',
            'Stack Trace:',
            'Error: Something broke',
            '    at sync (sync.ts:10)',
            'Caused By: Network request failed',
        ].join('\n'));
    });

    test('leaves out the fields with no value', () => {
        expect(formatErrorDetails({message: 'Offline', validationErrors: {}})).toBe('Message: Offline');
    });

    test('keeps a response body that is not JSON as it is', () => {
        expect(formatErrorDetails({message: 'Bad gateway', responseBody: '<html>502</html>'}))
            .toBe('Message: Bad gateway\nResponse Body:\n<html>502</html>');
    });

    test('says so when there is nothing to show', () => {
        expect(formatErrorDetails({message: ''})).toBe('No details available');
    });
});
