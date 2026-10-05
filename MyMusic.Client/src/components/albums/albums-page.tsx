import {ActionIcon} from "@mantine/core";
import {useDisclosure} from "@mantine/hooks";
import {IconPlus} from "@tabler/icons-react";
import {useTranslation} from "react-i18next";
import {useListAlbums} from "../../client/albums.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {useCollectionActions, useCollectionStateByKey} from "../../stores/collection-store.tsx";
import Collection from "../common/collection/collection.tsx";
import CollectionToolbar from "../common/collection/collection-toolbar.tsx";
import CreateAlbumModal from "./create-album-modal.tsx";
import {useAlbumsSchema} from "./useAlbumsSchema.tsx";

const ALBUMS_STATE_KEY = "albums";

export default function AlbumsPage() {
    const {t} = useTranslation(["albums", "common"]);
    const {setCollectionFilter} = useCollectionActions(state => ({
        setCollectionFilter: state.setCollectionFilter,
    }));
    const collectionState = useCollectionStateByKey(ALBUMS_STATE_KEY);
    const appliedSearch = collectionState.filter.search;
    const appliedFilter = collectionState.filter.expression;

    const albumsQuery = useListAlbums(
        {search: appliedSearch, filter: appliedFilter},
        {query: {select: response => response.data}},
    );

    const albums = useQueryData(albumsQuery, t("albums:page.fetchFailed")) ?? {albums: []};

    const albumsSchema = useAlbumsSchema();
    const [createOpened, {open: openCreate, close: closeCreate}] = useDisclosure(false);

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setCollectionFilter(ALBUMS_STATE_KEY, { search: newSearch, expression: newFilter });
    };

    const elements = albums?.albums ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}} data-testid="albums">
            <Collection
                key={ALBUMS_STATE_KEY}
                stateKey={ALBUMS_STATE_KEY}
                items={elements}
                schema={albumsSchema}
                isFetching={albumsQuery.isFetching}
                filterMode="server"
                serverSearch={appliedSearch}
                serverFilter={appliedFilter}
                onServerFilterChange={handleFilterChange}
                searchPlaceholder={t("albums:page.searchPlaceholder")}
                toolbar={p => (
                    <CollectionToolbar
                        {...p}
                        renderExtraActions={() => (
                            <ActionIcon
                                variant="default"
                                size="lg"
                                aria-label={t("albums:page.createAlbum")}
                                title={t("albums:page.createAlbum")}
                                onClick={openCreate}
                                data-testid="create-album"
                            >
                                <IconPlus/>
                            </ActionIcon>
                        )}
                    />
                )}
            />
            <CreateAlbumModal opened={createOpened} onClose={closeCreate}/>
        </div>
    );
}
