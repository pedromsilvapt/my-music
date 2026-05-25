import {z} from 'zod';

export const SyncFileInfoItemRequestSchema = z.object({
    path: z.string(),
    modifiedAt: z.string(),
    createdAt: z.string(),
    reason: z.string().optional(),
});

export type SyncFileInfoItemRequest = z.infer<typeof SyncFileInfoItemRequestSchema>;

export const SyncFileInfoItemSchema = z.object({
    path: z.string(),
    modifiedAt: z.coerce.date(),
    createdAt: z.coerce.date(),
    reason: z.string().optional(),
});

export type SyncFileInfoItem = z.infer<typeof SyncFileInfoItemSchema>;

export const SyncStartRequestSchema = z.object({
    dryRun: z.boolean().optional(),
    repositoryPath: z.string().optional(),
    scanErrors: z.array(z.object({
        path: z.string(),
        error: z.string(),
    })).optional(),
});

export type SyncStartRequest = z.infer<typeof SyncStartRequestSchema>;

export const SyncStartResponseSchema = z.object({
    sessionId: z.number(),
});

export type SyncStartResponse = z.infer<typeof SyncStartResponseSchema>;

export const SyncFileInfoSchema = SyncFileInfoItemSchema;

export const SyncCheckRequestSchema = z.object({
    files: z.array(SyncFileInfoItemRequestSchema),
    force: z.boolean(),
});

export type SyncCheckRequest = z.infer<typeof SyncCheckRequestSchema>;

export const SyncPotentialConflictItemSchema = z.object({
    path: z.string(),
    localModifiedAt: z.coerce.date(),
    serverModifiedAt: z.coerce.date(),
    lastSyncedAt: z.coerce.date().nullable(),
    songId: z.number().nullable(),
    serverChecksum: z.string(),
    serverChecksumAlgorithm: z.string(),
});

export type SyncPotentialConflictItem = z.infer<typeof SyncPotentialConflictItemSchema>;

/** Represents pending actions that come from the server (e.g., files to download or remove). */
export const SyncActionSchema = z.enum(['Download', 'Upload', 'Remove']);

export type SyncAction = z.infer<typeof SyncActionSchema>;

/** Represents the result of what action was performed during a specific sync session. */
export const SyncRecordActionSchema = z.enum(['CreateRemote', 'UpdateRemote', 'CreateLocal', 'UpdateLocal', 'Delete', 'Link', 'Unlink', 'Rename', 'Skipped', 'Conflict', 'UpdateTimestamp', 'Error']);

export type SyncRecordAction = z.infer<typeof SyncRecordActionSchema>;

export const RenameDataSchema = z.object({
    previousPath: z.string(),
    newPath: z.string(),
});

export const SongModifiedAtDataSchema = z.object({
    songId: z.number().nullable().optional(),
    modifiedAt: z.string().nullable().optional(),
    checksum: z.string().nullable().optional(),
    algorithm: z.string().nullable().optional(),
});

export const CreateRemoteDataSchema = z.object({
    songId: z.number().nullable().optional(),
    checksum: z.string().nullable().optional(),
    algorithm: z.string().nullable().optional(),
    modifiedAt: z.string().nullable().optional(),
    tempFilePath: z.string().nullable().optional(),
    createdAt: z.string().nullable().optional(),
    originalFilePath: z.string().nullable().optional(),
});

export const UpdateRemoteDataSchema = z.object({
    songId: z.number().nullable().optional(),
    checksum: z.string().nullable().optional(),
    algorithm: z.string().nullable().optional(),
    modifiedAt: z.string().nullable().optional(),
    tempFilePath: z.string().nullable().optional(),
    createdAt: z.string().nullable().optional(),
    originalFilePath: z.string().nullable().optional(),
});

export const ConflictDataSchema = z.object({
    localModifiedAt: z.string(),
    serverModifiedAt: z.string(),
});

export const UpdateTimestampDataSchema = z.object({
    newTimestamp: z.string(),
    songId: z.number().nullable().optional(),
});

export const ErrorDataSchema = z.object({
    errorMessage: z.string(),
});

const SyncRecordItemBaseSchema = z.object({
    id: z.number(),
    filePath: z.string(),
    songId: z.number().nullable(),
    resolvesConflictRecordId: z.number().nullable().optional(),
    reason: z.string().nullable().optional(),
    acknowledged: z.boolean(),
    processedAt: z.string(),
});

export const SyncRecordItemSchema = z.discriminatedUnion('action', [
    SyncRecordItemBaseSchema.extend({ action: z.literal('CreateRemote'), data: CreateRemoteDataSchema.nullable().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('UpdateRemote'), data: UpdateRemoteDataSchema.nullable().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('CreateLocal'), data: SongModifiedAtDataSchema.nullable().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('UpdateLocal'), data: SongModifiedAtDataSchema.nullable().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('Delete'), data: z.null().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('Unlink'), data: SongModifiedAtDataSchema.nullable().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('Link'), data: SongModifiedAtDataSchema.nullable().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('Rename'), data: RenameDataSchema.nullable().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('Skipped'), data: z.null().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('Conflict'), data: ConflictDataSchema.nullable().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('UpdateTimestamp'), data: UpdateTimestampDataSchema.nullable().optional() }),
    SyncRecordItemBaseSchema.extend({ action: z.literal('Error'), data: ErrorDataSchema.nullable().optional() }),
]);

export type SyncRecordItem = z.infer<typeof SyncRecordItemSchema>;

export type RenameData = z.infer<typeof RenameDataSchema>;
export type SongModifiedAtData = z.infer<typeof SongModifiedAtDataSchema>;
export type CreateRemoteData = z.infer<typeof CreateRemoteDataSchema>;
export type UpdateRemoteData = z.infer<typeof UpdateRemoteDataSchema>;
export type ConflictData = z.infer<typeof ConflictDataSchema>;
export type UpdateTimestampData = z.infer<typeof UpdateTimestampDataSchema>;
export type ErrorData = z.infer<typeof ErrorDataSchema>;

export const SyncActionCountsSchema = z.object({
    createRemoteCount: z.number(),
    updateRemoteCount: z.number(),
    skippedCount: z.number(),
    createLocalCount: z.number(),
    updateLocalCount: z.number(),
    deleteCount: z.number(),
    linkCount: z.number(),
    unlinkCount: z.number(),
    renameCount: z.number(),
    conflictCount: z.number(),
    updateTimestampCount: z.number(),
    errorCount: z.number(),
});

export type SyncActionCounts = z.infer<typeof SyncActionCountsSchema>;

export const SyncCheckResponseSchema = z.object({
    toCreate: z.array(SyncFileInfoItemSchema),
    toUpdate: z.array(SyncFileInfoSchema),
    potentialConflicts: z.array(SyncPotentialConflictItemSchema),
    records: z.array(SyncRecordItemSchema),
    skippedRecordIds: z.array(z.number()),
    counts: SyncActionCountsSchema,
});

export type SyncCheckResponse = z.infer<typeof SyncCheckResponseSchema>;

export const SyncCommitRequestSchema = z.object({
    direction: z.string().optional(),
});

export type SyncCommitRequest = z.infer<typeof SyncCommitRequestSchema>;

export const SyncCommitResponseSchema = z.object({
    createRemoteCount: z.number(),
    updateRemoteCount: z.number(),
    skippedCount: z.number(),
    createLocalCount: z.number(),
    updateLocalCount: z.number(),
    deleteCount: z.number(),
    linkCount: z.number(),
    unlinkCount: z.number(),
    renameCount: z.number(),
    conflictCount: z.number(),
    updateTimestampCount: z.number(),
    errorCount: z.number(),
    committedAt: z.coerce.date(),
});

export type SyncCommitResponse = z.infer<typeof SyncCommitResponseSchema>;

export const SyncCompleteResponseSchema = z.object({
    createRemoteCount: z.number(),
    updateRemoteCount: z.number(),
    skippedCount: z.number(),
    createLocalCount: z.number(),
    updateLocalCount: z.number(),
    deleteCount: z.number(),
    linkCount: z.number(),
    unlinkCount: z.number(),
    renameCount: z.number(),
    conflictCount: z.number(),
    updateTimestampCount: z.number(),
    errorCount: z.number(),
});

export type SyncCompleteResponse = z.infer<typeof SyncCompleteResponseSchema>;

export const SyncUploadResponseSchema = z.object({
    success: z.boolean(),
    songId: z.number().nullable(),
    recordId: z.number().nullable(),
    action: SyncRecordActionSchema.nullable(),
    data: z.union([CreateRemoteDataSchema, UpdateRemoteDataSchema, SongModifiedAtDataSchema, RenameDataSchema, ConflictDataSchema, UpdateTimestampDataSchema, ErrorDataSchema, z.null()]).nullable(),
    counts: SyncActionCountsSchema,
});

export type SyncUploadResponse = z.infer<typeof SyncUploadResponseSchema>;

export const CreatePendingActionsResponseSchema = z.object({
    records: z.array(SyncRecordItemSchema),
});

export type CreatePendingActionsResponse = z.infer<typeof CreatePendingActionsResponseSchema>;

export const AcknowledgeActionRequestSchema = z.object({
    recordIds: z.array(z.number()),
    modifiedAt: z.string().optional(),
});

export type AcknowledgeActionRequest = z.infer<typeof AcknowledgeActionRequestSchema>;

export const SyncActionRecordResponseItemSchema = z.object({
    id: z.number(),
    action: SyncRecordActionSchema,
    data: z.union([CreateRemoteDataSchema, UpdateRemoteDataSchema, SongModifiedAtDataSchema, RenameDataSchema, ConflictDataSchema, UpdateTimestampDataSchema, ErrorDataSchema, z.null()]).nullable().optional(),
    resolvesConflictRecordId: z.number().nullable().optional(),
});

export type SyncActionRecordResponseItem = z.infer<typeof SyncActionRecordResponseItemSchema>;

export const AcknowledgeActionResponseSchema = z.object({
    success: z.boolean(),
    records: z.array(SyncActionRecordResponseItemSchema).optional(),
    counts: SyncActionCountsSchema,
});

export type AcknowledgeActionResponse = z.infer<typeof AcknowledgeActionResponseSchema>;

export const CreateDeviceRequestSchema = z.object({
    name: z.string(),
    icon: z.string().optional(),
    color: z.string().optional(),
    namingTemplate: z.string().optional(),
    importOnPurchase: z.boolean().optional(),
});

export type CreateDeviceRequest = z.infer<typeof CreateDeviceRequestSchema>;

export const CreateDeviceItemSchema = z.object({
    id: z.number(),
    name: z.string(),
    icon: z.string().nullable(),
    color: z.string().nullable(),
    namingTemplate: z.string().nullable(),
    importOnPurchase: z.boolean(),
    ownerId: z.number(),
    createdAt: z.string(),
    lastSyncAt: z.string().nullable(),
    songCount: z.number(),
});

export type CreateDeviceItem = z.infer<typeof CreateDeviceItemSchema>;

export const CreateDeviceResponseSchema = z.object({
    device: CreateDeviceItemSchema,
});

export type CreateDeviceResponse = z.infer<typeof CreateDeviceResponseSchema>;

export const UpdateDeviceRequestSchema = z.object({
    icon: z.string().optional(),
    color: z.string().optional(),
    namingTemplate: z.string().optional(),
    importOnPurchase: z.boolean().optional(),
});

export type UpdateDeviceRequest = z.infer<typeof UpdateDeviceRequestSchema>;

export const UpdateDeviceItemSchema = z.object({
    id: z.number(),
    name: z.string(),
    icon: z.string().nullable(),
    color: z.string().nullable(),
    namingTemplate: z.string().nullable(),
    importOnPurchase: z.boolean(),
});

export type UpdateDeviceItem = z.infer<typeof UpdateDeviceItemSchema>;

export const UpdateDeviceResponseSchema = z.object({
    device: UpdateDeviceItemSchema,
});

export type UpdateDeviceResponse = z.infer<typeof UpdateDeviceResponseSchema>;

export const ListDeviceItemSchema = z.object({
    id: z.number(),
    name: z.string(),
    icon: z.string().nullable(),
    color: z.string().nullable(),
    namingTemplate: z.string().nullable(),
    importOnPurchase: z.boolean(),
    ownerId: z.number().optional(),
    createdAt: z.string().optional(),
    lastSyncAt: z.string().nullable().optional(),
    songCount: z.number(),
});

export type ListDeviceItem = z.infer<typeof ListDeviceItemSchema>;

export const ListDevicesResponseSchema = z.object({
    devices: z.array(ListDeviceItemSchema),
});

export type ListDevicesResponse = z.infer<typeof ListDevicesResponseSchema>;

export const GetDeviceItemSchema = ListDeviceItemSchema;

export type GetDeviceItem = z.infer<typeof GetDeviceItemSchema>;

export const GetDeviceResponseSchema = z.object({
    device: GetDeviceItemSchema,
});

export type GetDeviceResponse = z.infer<typeof GetDeviceResponseSchema>;

export const SyncSessionItemSchema = z.object({
    id: z.number(),
    startedAt: z.string(),
    completedAt: z.string().nullable(),
    status: z.string(),
    isDryRun: z.boolean(),
    createRemoteCount: z.number(),
    updateRemoteCount: z.number(),
    skippedCount: z.number(),
    createLocalCount: z.number(),
    updateLocalCount: z.number(),
    deleteCount: z.number(),
    linkCount: z.number(),
    unlinkCount: z.number(),
    renameCount: z.number(),
    conflictCount: z.number(),
    updateTimestampCount: z.number(),
    errorCount: z.number(),
    repositoryPath: z.string().nullable(),
});

export type SyncSessionItem = z.infer<typeof SyncSessionItemSchema>;

export const ListSyncSessionsResponseSchema = z.object({
    sessions: z.array(SyncSessionItemSchema),
});

export type ListSyncSessionsResponse = z.infer<typeof ListSyncSessionsResponseSchema>;

export const SyncRecordResponseItemSchema = z.object({
    filePath: z.string(),
    action: SyncRecordActionSchema,
    songId: z.number().nullable(),
    reason: z.string().nullable(),
    data: z.union([CreateRemoteDataSchema, UpdateRemoteDataSchema, SongModifiedAtDataSchema, RenameDataSchema, ConflictDataSchema, UpdateTimestampDataSchema, ErrorDataSchema, z.null()]).nullable().optional(),
    resolvesConflictRecordId: z.number().nullable().optional(),
    processedAt: z.string(),
});

export type SyncRecordResponseItem = z.infer<typeof SyncRecordResponseItemSchema>;

export const ListSyncRecordsResponseSchema = z.object({
    records: z.array(SyncRecordResponseItemSchema),
    nextCursor: z.string().nullable(),
    hasMore: z.boolean(),
    totalCount: z.number(),
});

export type ListSyncRecordsResponse = z.infer<typeof ListSyncRecordsResponseSchema>;

export const ProblemDetailsSchema = z.object({
    type: z.string().optional(),
    title: z.string().optional(),
    status: z.number().optional(),
    detail: z.string().optional(),
    errors: z.record(z.string(), z.array(z.string())).optional(),
});

export type ProblemDetails = z.infer<typeof ProblemDetailsSchema>;

export class ApiError extends Error {
    status: number;
    details?: ProblemDetails;
    responseBody?: string;
    url?: string;

    constructor(opts: { status: number; message: string; details?: ProblemDetails; responseBody?: string; url?: string }) {
        super(opts.message);
        this.name = 'ApiError';
        this.status = opts.status;
        this.details = opts.details;
        this.responseBody = opts.responseBody;
        this.url = opts.url;
    }
}

export const PruneSessionsRequestSchema = z.object({
    all: z.boolean(),
});

export type PruneSessionsRequest = z.infer<typeof PruneSessionsRequestSchema>;

export const PruneSessionsResponseSchema = z.object({
    deletedCount: z.number(),
});

export type PruneSessionsResponse = z.infer<typeof PruneSessionsResponseSchema>;

export const DeleteSessionResponseSchema = z.object({
    success: z.boolean(),
});

export type DeleteSessionResponse = z.infer<typeof DeleteSessionResponseSchema>;

export const SyncConflictResolveItemSchema = z.object({
    path: z.string(),
    songId: z.number().nullable(),
    fileContentBase64: z.string(),
    localModifiedAt: z.string(),
});

export type SyncConflictResolveItem = z.infer<typeof SyncConflictResolveItemSchema>;

export const SyncResolveConflictsRequestSchema = z.object({
    conflicts: z.array(SyncConflictResolveItemSchema),
});

export type SyncResolveConflictsRequest = z.infer<typeof SyncResolveConflictsRequestSchema>;

export const SyncConflictErrorItemSchema = z.object({
    path: z.string(),
    reason: z.string(),
});

export type SyncConflictErrorItem = z.infer<typeof SyncConflictErrorItemSchema>;

export const SyncResolveConflictsResponseSchema = z.object({
    toUpload: z.array(SyncFileInfoItemSchema),
    resolved: z.array(SyncFileInfoItemSchema),
    conflicts: z.array(SyncConflictErrorItemSchema),
    conflictRecords: z.array(SyncActionRecordResponseItemSchema),
    updateTimestampRecords: z.array(SyncActionRecordResponseItemSchema),
    counts: SyncActionCountsSchema,
});

export type SyncResolveConflictsResponse = z.infer<typeof SyncResolveConflictsResponseSchema>;

export const ReportSyncErrorRequestSchema = z.object({
    filePath: z.string(),
    errorMessage: z.string(),
    songId: z.number().nullable().optional(),
});

export type ReportSyncErrorRequest = z.infer<typeof ReportSyncErrorRequestSchema>;

export const ReportSyncErrorResponseSchema = z.object({
    counts: SyncActionCountsSchema,
});

export type ReportSyncErrorResponse = z.infer<typeof ReportSyncErrorResponseSchema>;