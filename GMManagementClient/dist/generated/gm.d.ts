import { ServiceType } from "@protobuf-ts/runtime-rpc";
import type { BinaryWriteOptions } from "@protobuf-ts/runtime";
import type { IBinaryWriter } from "@protobuf-ts/runtime";
import type { BinaryReadOptions } from "@protobuf-ts/runtime";
import type { IBinaryReader } from "@protobuf-ts/runtime";
import type { PartialMessage } from "@protobuf-ts/runtime";
import { MessageType } from "@protobuf-ts/runtime";
export interface GetLeaderboardTypesRequest {
}
export interface GetLeaderboardTypesResponse {
    leaderboardTypes: string[];
}
export interface GetLeaderboardWithPaginationRequest {
    leaderboardType: string;
    rankStartIndex: number;
    numberOfRanks: number;
}
export interface GetLeaderboardWithPaginationResponse {
    ranks: Rank[];
}
export interface Rank {
    playerId: string;
    score: string;
}
export interface RemovePlayersFromLeaderboardRequest {
    leaderboardTypes: string[];
    playerIds: string[];
}
export interface RemovePlayersFromLeaderboardResponse {
    failedList: RemoveFailedInfo[];
}
export interface RemoveFailedInfo {
    leaderboardType: string;
    playerId: string;
    reason: string;
}
declare class GetLeaderboardTypesRequest$Type extends MessageType<GetLeaderboardTypesRequest> {
    constructor();
    create(value?: PartialMessage<GetLeaderboardTypesRequest>): GetLeaderboardTypesRequest;
    internalBinaryRead(reader: IBinaryReader, length: number, options: BinaryReadOptions, target?: GetLeaderboardTypesRequest): GetLeaderboardTypesRequest;
    internalBinaryWrite(message: GetLeaderboardTypesRequest, writer: IBinaryWriter, options: BinaryWriteOptions): IBinaryWriter;
}
export declare const GetLeaderboardTypesRequest: GetLeaderboardTypesRequest$Type;
declare class GetLeaderboardTypesResponse$Type extends MessageType<GetLeaderboardTypesResponse> {
    constructor();
    create(value?: PartialMessage<GetLeaderboardTypesResponse>): GetLeaderboardTypesResponse;
    internalBinaryRead(reader: IBinaryReader, length: number, options: BinaryReadOptions, target?: GetLeaderboardTypesResponse): GetLeaderboardTypesResponse;
    internalBinaryWrite(message: GetLeaderboardTypesResponse, writer: IBinaryWriter, options: BinaryWriteOptions): IBinaryWriter;
}
export declare const GetLeaderboardTypesResponse: GetLeaderboardTypesResponse$Type;
declare class GetLeaderboardWithPaginationRequest$Type extends MessageType<GetLeaderboardWithPaginationRequest> {
    constructor();
    create(value?: PartialMessage<GetLeaderboardWithPaginationRequest>): GetLeaderboardWithPaginationRequest;
    internalBinaryRead(reader: IBinaryReader, length: number, options: BinaryReadOptions, target?: GetLeaderboardWithPaginationRequest): GetLeaderboardWithPaginationRequest;
    internalBinaryWrite(message: GetLeaderboardWithPaginationRequest, writer: IBinaryWriter, options: BinaryWriteOptions): IBinaryWriter;
}
export declare const GetLeaderboardWithPaginationRequest: GetLeaderboardWithPaginationRequest$Type;
declare class GetLeaderboardWithPaginationResponse$Type extends MessageType<GetLeaderboardWithPaginationResponse> {
    constructor();
    create(value?: PartialMessage<GetLeaderboardWithPaginationResponse>): GetLeaderboardWithPaginationResponse;
    internalBinaryRead(reader: IBinaryReader, length: number, options: BinaryReadOptions, target?: GetLeaderboardWithPaginationResponse): GetLeaderboardWithPaginationResponse;
    internalBinaryWrite(message: GetLeaderboardWithPaginationResponse, writer: IBinaryWriter, options: BinaryWriteOptions): IBinaryWriter;
}
export declare const GetLeaderboardWithPaginationResponse: GetLeaderboardWithPaginationResponse$Type;
declare class Rank$Type extends MessageType<Rank> {
    constructor();
    create(value?: PartialMessage<Rank>): Rank;
    internalBinaryRead(reader: IBinaryReader, length: number, options: BinaryReadOptions, target?: Rank): Rank;
    internalBinaryWrite(message: Rank, writer: IBinaryWriter, options: BinaryWriteOptions): IBinaryWriter;
}
export declare const Rank: Rank$Type;
declare class RemovePlayersFromLeaderboardRequest$Type extends MessageType<RemovePlayersFromLeaderboardRequest> {
    constructor();
    create(value?: PartialMessage<RemovePlayersFromLeaderboardRequest>): RemovePlayersFromLeaderboardRequest;
    internalBinaryRead(reader: IBinaryReader, length: number, options: BinaryReadOptions, target?: RemovePlayersFromLeaderboardRequest): RemovePlayersFromLeaderboardRequest;
    internalBinaryWrite(message: RemovePlayersFromLeaderboardRequest, writer: IBinaryWriter, options: BinaryWriteOptions): IBinaryWriter;
}
export declare const RemovePlayersFromLeaderboardRequest: RemovePlayersFromLeaderboardRequest$Type;
declare class RemovePlayersFromLeaderboardResponse$Type extends MessageType<RemovePlayersFromLeaderboardResponse> {
    constructor();
    create(value?: PartialMessage<RemovePlayersFromLeaderboardResponse>): RemovePlayersFromLeaderboardResponse;
    internalBinaryRead(reader: IBinaryReader, length: number, options: BinaryReadOptions, target?: RemovePlayersFromLeaderboardResponse): RemovePlayersFromLeaderboardResponse;
    internalBinaryWrite(message: RemovePlayersFromLeaderboardResponse, writer: IBinaryWriter, options: BinaryWriteOptions): IBinaryWriter;
}
export declare const RemovePlayersFromLeaderboardResponse: RemovePlayersFromLeaderboardResponse$Type;
declare class RemoveFailedInfo$Type extends MessageType<RemoveFailedInfo> {
    constructor();
    create(value?: PartialMessage<RemoveFailedInfo>): RemoveFailedInfo;
    internalBinaryRead(reader: IBinaryReader, length: number, options: BinaryReadOptions, target?: RemoveFailedInfo): RemoveFailedInfo;
    internalBinaryWrite(message: RemoveFailedInfo, writer: IBinaryWriter, options: BinaryWriteOptions): IBinaryWriter;
}
export declare const RemoveFailedInfo: RemoveFailedInfo$Type;
export declare const Leaderboard: ServiceType;
export {};
