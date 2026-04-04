import {useUncontrolled} from '@mantine/hooks';
import {useCallback, useEffect, useMemo, useRef, useState} from 'react';

export interface UseFilterOptions {
    value?: string;
    defaultValue?: string;
    onChange?: (value: string) => void;
    debounceMs?: number;
}

export interface UseFilterReturn {
    value: string;
    debouncedValue: string;
    setValue: (value: string) => void;
}

/**
 * Unified hook for managing filter/search state with debouncing.
 * 
 * Uses Mantine's useUncontrolled to handle both controlled and uncontrolled scenarios:
 * - If 'value' prop is provided → controlled mode (parent manages state)
 * - If 'defaultValue' is provided → uncontrolled mode (internal state)
 * 
 * Returns both immediate value (for UI display) and debounced value (for API calls/filtering)
 * 
 * PERFORMANCE OPTIMIZATIONS:
 * - Returns memoized object to prevent unnecessary re-renders downstream
 * - Returns stable setValue callback (useCallback) 
 * - Debounce effect only updates state when value actually changes
 */
export function useFilter(options: UseFilterOptions = {}): UseFilterReturn {
    const {debounceMs = 300} = options;

    // Use Mantine's useUncontrolled for clean controlled/uncontrolled handling
    const [value, setValueInternal] = useUncontrolled({
        value: options.value,
        defaultValue: options.defaultValue ?? '',
        finalValue: '',
        onChange: options.onChange,
    });

    // Debounced value for API calls/filtering
    const [debouncedValue, setDebouncedValue] = useState(value);

    // Ref to track pending timeout for cleanup
    const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

    // Clear timeout on unmount
    useEffect(() => {
        return () => {
            if (timeoutRef.current) {
                clearTimeout(timeoutRef.current);
            }
        };
    }, []);

    // Optimized debouncing - only update if value changed and not already debounced
    useEffect(() => {
        // Clear existing timeout
        if (timeoutRef.current) {
            clearTimeout(timeoutRef.current);
            timeoutRef.current = null;
        }

        // If no debounce needed or value already matches debounced, skip
        if (debounceMs <= 0) {
            if (value !== debouncedValue) {
                setDebouncedValue(value);
            }
            return;
        }

        // Set new timeout
        timeoutRef.current = setTimeout(() => {
            setDebouncedValue(value);
            timeoutRef.current = null;
        }, debounceMs);
    }, [value, debounceMs, debouncedValue]);

    // Stable callback for setting value
    const setValue = useCallback((newValue: string) => {
        setValueInternal(newValue);
    }, [setValueInternal]);

    // Memoize return object to prevent unnecessary re-renders in consuming components
    return useMemo(() => ({
        value,
        debouncedValue,
        setValue,
    }), [value, debouncedValue, setValue]);
}

export default useFilter;
