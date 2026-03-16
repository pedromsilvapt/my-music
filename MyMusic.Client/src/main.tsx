import {MantineProvider} from '@mantine/core';
import {ModalsProvider} from '@mantine/modals';
import {Notifications} from '@mantine/notifications';
import {QueryClient, QueryClientProvider} from "@tanstack/react-query";
import {createRouter, RouterProvider} from "@tanstack/react-router";
import './index.css'
import {StrictMode} from 'react'
import {createRoot} from 'react-dom/client'
import {CollectionStoreProvider} from "./contexts/collection-context.tsx";
import ManageDevicesProvider from "./contexts/manage-devices-context.tsx";
import ManagePlaylistsProvider from "./contexts/manage-playlists-context.tsx";
import {PlayerProvider} from "./contexts/player-context.tsx";
import {ArtworkLightboxProvider} from "./contexts/artwork-lightbox-context.tsx";
import VolumeInitializer from "./components/volume-initializer.tsx";
import SongEditorContextModal from "./components/songs/song-editor-context-modal.tsx";

// Import the generated route tree
import {routeTree} from './routeTree.gen'

// Create a new router instance
const router = createRouter({routeTree})

// Register the router instance for type safety
declare module '@tanstack/react-router' {
    interface Register {
        router: typeof router
    }
}

const queryClient = new QueryClient();

// if (process.env.NODE_ENV === 'development') {
//     require('./mock');
// }

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <QueryClientProvider client={queryClient}>
            <MantineProvider defaultColorScheme="auto">
                <Notifications position="top-right"/>
                <ArtworkLightboxProvider>
                    <ModalsProvider
                        modals={{
                            'song-editor': SongEditorContextModal,
                        }}>
                        <PlayerProvider>
                            <VolumeInitializer/>
                            <ManagePlaylistsProvider>
                                <ManageDevicesProvider>
                                    <CollectionStoreProvider>
                                        <RouterProvider router={router}/>
                                    </CollectionStoreProvider>
                                </ManageDevicesProvider>
                            </ManagePlaylistsProvider>
                        </PlayerProvider>
                    </ModalsProvider>
                </ArtworkLightboxProvider>
            </MantineProvider>
        </QueryClientProvider>
    </StrictMode>,
)
