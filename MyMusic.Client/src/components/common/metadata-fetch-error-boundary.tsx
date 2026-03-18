import {Component, type ErrorInfo, type ReactNode} from 'react';
import {Alert, Button, Stack, Text} from '@mantine/core';
import {IconAlertCircle, IconRefresh} from '@tabler/icons-react';

interface Props {
    children: ReactNode;
    fallback?: ReactNode;
    onError?: (error: Error, errorInfo: ErrorInfo) => void;
    resetKeys?: Array<string | number>;
}

interface State {
    hasError: boolean;
    error: Error | null;
    errorInfo: ErrorInfo | null;
}

/**
 * Error boundary component for catching and displaying metadata fetch errors.
 * Provides a user-friendly fallback UI with retry functionality.
 */
export class MetadataFetchErrorBoundary extends Component<Props, State> {
    constructor(props: Props) {
        super(props);
        this.state = {hasError: false, error: null, errorInfo: null};
    }

    static getDerivedStateFromError(error: Error): State {
        return {hasError: true, error, errorInfo: null};
    }

    componentDidCatch(error: Error, errorInfo: ErrorInfo) {
        this.setState({error, errorInfo});
        
        // Log to console for debugging
        console.error('Metadata fetch error:', error, errorInfo);
        
        // Call optional error handler
        this.props.onError?.(error, errorInfo);
    }

    componentDidUpdate(prevProps: Props) {
        // Reset error state if resetKeys change
        if (this.state.hasError && this.props.resetKeys) {
            const hasResetKeyChanged = this.props.resetKeys.some(
                (key, idx) => key !== prevProps.resetKeys?.[idx]
            );
            
            if (hasResetKeyChanged) {
                this.resetErrorBoundary();
            }
        }
    }

    resetErrorBoundary = () => {
        this.setState({hasError: false, error: null, errorInfo: null});
    };

    render() {
        if (this.state.hasError) {
            // Custom fallback UI for metadata fetch errors
            if (this.props.fallback) {
                return this.props.fallback;
            }

            return (
                <Alert
                    icon={<IconAlertCircle size={24} />}
                    title="Failed to Load Metadata"
                    color="red"
                    variant="filled"
                >
                    <Stack gap="sm">
                        <Text size="sm">
                            An error occurred while fetching metadata suggestions. 
                            You can still edit the song manually.
                        </Text>
                        {this.state.error && (
                            <Text size="xs" c="red.2">
                                Error: {this.state.error.message}
                            </Text>
                        )}
                        <Button
                            leftSection={<IconRefresh size={16} />}
                            variant="white"
                            color="red"
                            onClick={this.resetErrorBoundary}
                            size="sm"
                        >
                            Retry
                        </Button>
                    </Stack>
                </Alert>
            );
        }

        return this.props.children;
    }
}

export default MetadataFetchErrorBoundary;
