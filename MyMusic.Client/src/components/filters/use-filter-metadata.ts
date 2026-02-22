import {useQuery} from "@tanstack/react-query";
import type {FilterEntity} from "../../types/filter-entity.ts";

export interface FilterFieldMetadata {
    name: string;
    entityPath?: string;
    type: string;
    description: string;
    supportedOperators: string[];
    isComputed?: boolean;
    isCollection?: boolean;
    nestedFields?: FilterFieldMetadata[];
    values?: string[];
    supportsDynamicValues?: boolean;
}

export interface FilterOperatorMetadata {
    name: string;
    displayName: string;
    description: string;
    applicableTypes: string[];
}

export interface FilterMetadataResponse {
    fields: FilterFieldMetadata[];
    operators: FilterOperatorMetadata[];
}

const ENTITY_ENDPOINTS: Record<FilterEntity, string> = {
    songs: '/api/songs/filter-metadata',
    artists: '/api/artists/filter-metadata',
    albums: '/api/albums/filter-metadata',
    playlists: '/api/playlists/filter-metadata',
    devices: '/api/devices/filter-metadata',
    sources: '/api/sources/songs/filter-metadata',
};

export function useFilterMetadata(entityType: FilterEntity) {
    return useQuery({
        queryKey: ["filter-metadata", entityType],
        queryFn: async (): Promise<FilterMetadataResponse> => {
            const endpoint = ENTITY_ENDPOINTS[entityType];
            const response = await fetch(endpoint);
            if (!response.ok) {
                throw new Error(`Failed to fetch filter metadata for ${entityType}`);
            }
            return response.json();
        },
        staleTime: Infinity,
    });
}
