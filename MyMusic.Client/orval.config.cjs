module.exports = {
    'mymusic-file-transfomer': {
        output: {
            mode: 'tags',
            target: './src/client/',
            schemas: './src/model',
            client: 'react-query',
            httpClient: 'fetch',
            baseUrl: '/api',
            biome: true,
            mock: true,
        },
        input: {
            target: 'http://localhost:5000/api/openapi/v1.json',
        },
    },
};
