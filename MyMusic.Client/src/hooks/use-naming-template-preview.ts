import {useDebouncedValue} from '@mantine/hooks';
import {keepPreviousData, useQuery} from '@tanstack/react-query';
import {getGetDevicesQueryKey, previewDeviceNamingTemplate} from '../client/devices';
import {throwOnProblem} from '../utils/api-problem';

const PREVIEW_DEBOUNCE_MS = 400;

interface NamingTemplatePreviewOptions {
    enabled?: boolean;
}

/**
 * Validates a naming template as it is typed and, for an existing device, previews the file names it gives
 * to the device's songs. Wraps the Orval-generated previewDeviceNamingTemplate as a query: the endpoint is a
 * POST (the template goes in its body), which Orval only exposes as a mutation. Without a device, the
 * template is only validated. `isOutdated` says the preview is not (yet) the one of the given template.
 */
export function useNamingTemplatePreview(
    deviceId: number | undefined,
    namingTemplate: string,
    {enabled = true}: NamingTemplatePreviewOptions = {}
) {
    const [debouncedTemplate] = useDebouncedValue(namingTemplate, PREVIEW_DEBOUNCE_MS);

    const query = useQuery({
        queryKey: [...getGetDevicesQueryKey(), 'naming-template-preview', deviceId ?? null, debouncedTemplate],
        queryFn: async ({signal}) =>
            throwOnProblem(await previewDeviceNamingTemplate(
                {deviceId: deviceId ?? null, namingTemplate: debouncedTemplate},
                {signal}
            )).data,
        enabled,
        // The previous preview stays on screen while the next one is computed
        placeholderData: keepPreviousData,
        retry: false,
        // Always asked again: the songs of the device may have changed since the last time
        gcTime: 0,
    });

    return {
        preview: query.data,
        isFetching: query.isFetching,
        isOutdated: debouncedTemplate !== namingTemplate || query.isFetching,
    };
}
