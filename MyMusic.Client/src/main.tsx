import {MantineProvider} from '@mantine/core';
import {QueryClient, QueryClientProvider} from "@tanstack/react-query";
import {createRouter, RouterProvider} from "@tanstack/react-router";
import {ContextMenuProvider} from 'mantine-contextmenu';
import './index.css'
import {StrictMode} from 'react'
import {createRoot} from 'react-dom/client'
import PlayerProvider from "./contexts/player-context.tsx";

// Import the generated route tree
import {routeTree} from './routeTree.gen'

// Create a new router instance
const router = createRouter({ routeTree })

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
            <MantineProvider>
                <ContextMenuProvider>
                    <PlayerProvider>
                        <RouterProvider router={router}/>
                    </PlayerProvider>
                </ContextMenuProvider>
            </MantineProvider>
        </QueryClientProvider>
    </StrictMode>,
)
