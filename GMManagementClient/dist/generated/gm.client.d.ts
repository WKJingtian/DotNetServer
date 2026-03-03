import type { RpcTransport } from "@protobuf-ts/runtime-rpc";
import type { ServiceInfo } from "@protobuf-ts/runtime-rpc";
import type { RemovePlayersFromLeaderboardResponse } from "./gm";
import type { RemovePlayersFromLeaderboardRequest } from "./gm";
import type { GetLeaderboardWithPaginationResponse } from "./gm";
import type { GetLeaderboardWithPaginationRequest } from "./gm";
import type { GetLeaderboardTypesResponse } from "./gm";
import type { GetLeaderboardTypesRequest } from "./gm";
import type { UnaryCall } from "@protobuf-ts/runtime-rpc";
import type { RpcOptions } from "@protobuf-ts/runtime-rpc";
export interface ILeaderboardClient {
    getLeaderboardTypes(input: GetLeaderboardTypesRequest, options?: RpcOptions): UnaryCall<GetLeaderboardTypesRequest, GetLeaderboardTypesResponse>;
    getLeaderboardWithPagination(input: GetLeaderboardWithPaginationRequest, options?: RpcOptions): UnaryCall<GetLeaderboardWithPaginationRequest, GetLeaderboardWithPaginationResponse>;
    removePlayersFromLeaderboard(input: RemovePlayersFromLeaderboardRequest, options?: RpcOptions): UnaryCall<RemovePlayersFromLeaderboardRequest, RemovePlayersFromLeaderboardResponse>;
}
export declare class LeaderboardClient implements ILeaderboardClient, ServiceInfo {
    private readonly _transport;
    typeName: string;
    methods: import("@protobuf-ts/runtime-rpc").MethodInfo<any, any>[];
    options: {
        [extensionName: string]: import("@protobuf-ts/runtime").JsonValue;
    };
    constructor(_transport: RpcTransport);
    getLeaderboardTypes(input: GetLeaderboardTypesRequest, options?: RpcOptions): UnaryCall<GetLeaderboardTypesRequest, GetLeaderboardTypesResponse>;
    getLeaderboardWithPagination(input: GetLeaderboardWithPaginationRequest, options?: RpcOptions): UnaryCall<GetLeaderboardWithPaginationRequest, GetLeaderboardWithPaginationResponse>;
    removePlayersFromLeaderboard(input: RemovePlayersFromLeaderboardRequest, options?: RpcOptions): UnaryCall<RemovePlayersFromLeaderboardRequest, RemovePlayersFromLeaderboardResponse>;
}
