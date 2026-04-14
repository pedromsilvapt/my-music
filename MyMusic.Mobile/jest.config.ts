import type {Config} from 'jest';

const config: Config = {
    preset: 'ts-jest',
    testEnvironment: 'node',
    roots: ['<rootDir>/src'],
    testMatch: ['**/__tests__/**/*.test.ts'],
    moduleFileExtensions: ['ts', 'tsx', 'js', 'jsx', 'json', 'node'],
    transform: {
        '^.+\\.tsx?$': ['ts-jest', {
            tsconfig: {
                strict: true,
                esModuleInterop: true,
                moduleResolution: 'node',
                target: 'ES2022',
                module: 'commonjs',
                skipLibCheck: true,
                baseUrl: '.',
                paths: {
                    '@/*': ['./*'],
                },
            },
        }],
    },
    moduleNameMapper: {
        '^@/(.*)$': '<rootDir>/$1',
    },
};

export default config;
