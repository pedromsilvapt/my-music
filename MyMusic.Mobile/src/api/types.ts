import {z} from 'zod';

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
});

export type SyncStartRequest = z.infer<typeof SyncStartRequestSchema>;

export const SyncStartResponseSchema = z.object({
    sessionId: z.number(),
});

export type SyncStartResponse = z.infer<typeof SyncStartResponseSchema>;

export const SyncFileInfoSchema = SyncFileInfoItemSchema;

export const SyncCheckRequestSchema = z.object({
    files: z.array(SyncFileInfoItemSchema),
    force: z.boolean(),
});

export type SyncCheckRequest = z.infer<typeof SyncCheckRequestSchema>;

export const SyncCheckResponseSchema = z.object({
    toCreate: z.array(SyncFileInfoItemSchema),
    toUpdate: z.array(SyncFileInfoSchema),
});

export type SyncCheckResponse = z.infer<typeof SyncCheckResponseSchema>;

export const SyncRecordsRequestSchema = z.object({
    records: z.array(z.object({
        filePath: z.string(),
        action: z.string(),
        source: z.string().optional(),
        songId: z.number().optional(),
        errorMessage: z.string().optional(),
        reason: z.string().optional(),
    })),
});

export type SyncRecordsRequest = z.infer<typeof SyncRecordsRequestSchema>;

export const SyncRecordsResponseSchema = z.object({
    success: z.boolean(),
});

export type SyncRecordsResponse = z.infer<typeof SyncRecordsResponseSchema>;

export const SyncCompleteResponseSchema = z.object({
    createdCount: z.number(),
    updatedCount: z.number(),
    skippedCount: z.number(),
    downloadedCount: z.number(),
    removedCount: z.number(),
    errorCount: z.number(),
});

export type SyncCompleteResponse = z.infer<typeof SyncCompleteResponseSchema>;

export const SyncUploadResponseSchema = z.object({
    success: z.boolean(),
    songId: z.number(),
});

export type SyncUploadResponse = z.infer<typeof SyncUploadResponseSchema>;

export const PendingActionItemSchema = z.object({
    songId: z.number(),
    path: z.string(),
    action: z.string(),
});

export type PendingActionItem = z.infer<typeof PendingActionItemSchema>;

export const GetPendingActionsResponseSchema = z.object({
    actions: z.array(PendingActionItemSchema),
});

export type GetPendingActionsResponse = z.infer<typeof GetPendingActionsResponseSchema>;

export const AcknowledgeActionRequestSchema = z.object({
    songId: z.number(),
});

export type AcknowledgeActionRequest = z.infer<typeof AcknowledgeActionRequestSchema>;

export const AcknowledgeActionResponseSchema = z.object({
    success: z.boolean(),
});

export type AcknowledgeActionResponse = z.infer<typeof AcknowledgeActionResponseSchema>;

export const CreateDeviceRequestSchema = z.object({
    name: z.string(),
    icon: z.string().optional(),
    color: z.string().optional(),
    namingTemplate: z.string().optional(),
});

export type CreateDeviceRequest = z.infer<typeof CreateDeviceRequestSchema>;

export const CreateDeviceItemSchema = z.object({
    id: z.number(),
    name: z.string(),
    icon: z.string().nullable(),
    color: z.string().nullable(),
    namingTemplate: z.string().nullable(),
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
});

export type UpdateDeviceRequest = z.infer<typeof UpdateDeviceRequestSchema>;

export const UpdateDeviceItemSchema = z.object({
    id: z.number(),
    name: z.string(),
    icon: z.string().nullable(),
    color: z.string().nullable(),
    namingTemplate: z.string().nullable(),
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
    createdCount: z.number(),
    updatedCount: z.number(),
    skippedCount: z.number(),
    downloadedCount: z.number(),
    removedCount: z.number(),
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
    action: z.string(),
    source: z.string(),
    songId: z.number().nullable(),
    errorMessage: z.string().nullable(),
    reason: z.string().nullable(),
    processedAt: z.string(),
});

export type SyncRecordResponseItem = z.infer<typeof SyncRecordResponseItemSchema>;

export const ListSyncRecordsResponseSchema = z.object({
    records: z.array(SyncRecordResponseItemSchema),
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

export type ApiError = {
    status: number;
    message: string;
    details?: ProblemDetails;
};