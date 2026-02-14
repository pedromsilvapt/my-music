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
                    ],
                }
            }
        },
        input: {
            target: 'http://localhost:5000/api/openapi/v1.json',
        },
    },
};
