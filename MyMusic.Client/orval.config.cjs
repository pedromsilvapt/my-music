module.exports = {
    'mymusic-file-transfomer': {
        output: {
            mode: 'tags',
            target: './src/client/',
            schemas: './src/model',
            client: 'react-query',
            httpClient: 'fetch',
            baseUrl: '/api',
            enumGenerationType: 'enum',
            biome: true,
            mock: true,
            override: {
                query: {
                    shouldSplitQueryKey: true,
                    useInvalidate: true,
                    mutationInvalidates: [
                        {
                            onMutations: ['deletePurchase', 'deleteManyPurchases', 'createPurchase', 'requeuePurchase'],
                            invalidates: ['listPurchases'],
                        },
                        {
                            onMutations: ['deletePlaylist', 'createPlaylist', 'addSongsToPlaylist', 'removeSongFromPlaylist', 'managePlaylistSongs'],
                            invalidates: ['listPlaylists'],
                        },
                        {
                            onMutations: ['addSongsToPlaylist', 'removeSongFromPlaylist', 'managePlaylistSongs'],
                            invalidates: ['getPlaylist'],
                        },
                        {
                            onMutations: ['toggleSongFavorite', 'toggleFavorites'],
                            invalidates: ['listSongs', 'getSong'],
                        },
                        {
                            onMutations: ['deleteApiDevicesDeviceId', 'postApiDevices', 'putApiDevicesDeviceId'],
                            invalidates: ['getApiDevices'],
                        },
                    ],
                }
            }
        },
        input: {
            target: 'http://localhost:5000/api/openapi/v1.json',
        },
    },
};
