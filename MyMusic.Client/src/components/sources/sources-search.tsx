import {useDebouncedValue} from "@mantine/hooks";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {useCallback, useEffect, useState} from "react";
import {getListPurchasesQueryKey, useCreatePurchase} from "../../client/purchases.ts";
import {type searchSongsResponse, useSearchSongs} from "../../client/sources.ts";
import {API_SEARCH_DEBOUNCE_MS} from "../../consts.ts";
import type {ListSourcesItem, SourceSong} from "../../model";
import Collection from "../common/collection/collection.tsx";
import SourcesSearchToolbar from "./sources-song-toolbar.tsx";
import {useSourceSongsSchema} from "./useSourceSongsSchema.tsx";

export default function SourcesSearch() {
    const queryClient = useQueryClient()

    const [search, setSearch] = useState('');
    const [filter, setFilter] = useState('');
    const [appliedFilter, setAppliedFilter] = useState('');
    const [source, setSource] = useState<ListSourcesItem | null | undefined>(null);
    const [debouncedSearch] = useDebouncedValue(search, API_SEARCH_DEBOUNCE_MS);

    const {data, isFetching} = useSearchSongs(source?.id ?? 0, debouncedSearch, {filter: appliedFilter}, {
        query: {
            placeholderData: (prev) => prev as searchSongsResponse | undefined
        }
    });

    const hasError = data && data.status >= 400;

    useEffect(() => {
        if (hasError) {
            notifications.show({
                title: "Error",
                message: "Failed to search songs. Please try again.",
                color: "red",
            });
            console.error("Search songs error:", data);
        }
    }, [hasError, data]);

    const createPurchase = useCreatePurchase({
        mutation: {
            onSuccess: () => {
                queryClient.invalidateQueries({queryKey: getListPurchasesQueryKey()})
            }
        }
    });

    const onPurchase = useCallback(async (songs: SourceSong[]) => {
        await Promise.all(songs.map(s => createPurchase.mutate({
            songId: s.id,
            sourceId: source!.id
        })))
    }, [createPurchase, source]);

    const sourceSongsSchema = useSourceSongsSchema(onPurchase);

    const elements = hasError ? [] : (data?.data ?? []);

    return <>
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="songs"
                stateKey="sources-search"
                items={elements}
                schema={sourceSongsSchema}
                isFetching={isFetching}
                toolbar={p => (
                    <SourcesSearchToolbar
                        {...p}
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