import {useDebouncedValue} from "@mantine/hooks";
import {useQueryClient} from "@tanstack/react-query";
import {useCallback, useRef, useState} from "react";
import {getListPurchasesQueryKey, useCreatePurchase} from "../../client/purchases.ts";
import {type searchSongsResponse, useSearchSongs} from "../../client/sources.ts";
import {API_SEARCH_DEBOUNCE_MS} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListSourcesItem, SourceSong} from "../../model";
import type {CollectionFilterBarRef} from "../common/collection/collection-filter-bar.tsx";
import Collection from "../common/collection/collection.tsx";
import SourcesSearchToolbar from "./sources-song-toolbar.tsx";
import {useSourceSongsSchema} from "./useSourceSongsSchema.tsx";

export default function SourcesSearch() {
    const queryClient = useQueryClient()
    const searchInputRef = useRef<CollectionFilterBarRef>(null);

    const [search, setSearch] = useState('');
    const [filter, setFilter] = useState('');
    const [appliedFilter, setAppliedFilter] = useState('');
    const [source, setSource] = useState<ListSourcesItem | null | undefined>(null);
    const [debouncedSearch] = useDebouncedValue(search, API_SEARCH_DEBOUNCE_MS);

    const searchSongsQuery = useSearchSongs(source?.id ?? 0, debouncedSearch, {filter: appliedFilter}, {
        query: {
            placeholderData: (prev) => prev as searchSongsResponse | undefined
        }
    });

    const searchSongsResponse = useQueryData(
        searchSongsQuery,
        "Failed to search songs"
    ) ?? {data: []};

    const createPurchase = useCreatePurchase({
        mutation: {
            onSuccess: () => {
                queryClient.invalidateQueries({queryKey: getListPurchasesQueryKey()})
            }
        }
    });

    const onPurchase = useCallback(async (songs: SourceSong[]) => {
        searchInputRef.current?.focusAndSelect();
        await Promise.all(songs.map(s => createPurchase.mutate({
            songId: s.id,
            sourceId: source!.id
        })))
    }, [createPurchase, source]);

    const sourceSongsSchema = useSourceSongsSchema(onPurchase);

    const elements = searchSongsResponse?.data ?? [];

    return <>
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="songs"
                stateKey="sources-search"
                items={elements}
                schema={sourceSongsSchema}
                isFetching={searchSongsQuery.isFetching}
                toolbar={p => (
                    <SourcesSearchToolbar
                        {...p}
                        searchInputRef={searchInputRef}
                        source={source}
                        setSource={setSource}
                        search={search}
                        setSearch={setSearch}
                        filter={filter}
                        setFilter={setFilter}
                        onApplyFilter={(filterValue) => {
                            setFilter(filterValue);
                            setAppliedFilter(filterValue);
                        }}
                    />
                )}
            >
            </Collection>
        </div>
    </>;
}