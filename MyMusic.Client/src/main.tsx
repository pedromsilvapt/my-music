import {StrictMode} from 'react'
import {createRoot} from 'react-dom/client'
import {MantineProvider} from '@mantine/core';
import './index.css'

// Import the generated route tree
import { routeTree } from './routeTree.gen'
import {createRouter, RouterProvider} from "@tanstack/react-router";
import {QueryClient, QueryClientProvider} from "@tanstack/react-query";

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
                <RouterProvider router={router} />
            </MantineProvider>
        </QueryClientProvider>
    </StrictMode>,
)
