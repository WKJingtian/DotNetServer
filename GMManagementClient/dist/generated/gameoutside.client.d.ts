import type { RpcTransport } from "@protobuf-ts/runtime-rpc";
import type { ServiceInfo } from "@protobuf-ts/runtime-rpc";
import type { GetGroupIdResponse } from "./gameoutside";
import type { GetGroupIdRequest } from "./gameoutside";
import type { UnaryCall } from "@protobuf-ts/runtime-rpc";
import type { RpcOptions } from "@protobuf-ts/runtime-rpc";
export interface IGroupClient {
    getGroupId(input: GetGroupIdRequest, options?: RpcOptions): UnaryCall<GetGroupIdRequest, GetGroupIdResponse>;
}
export declare class GroupClient implements IGroupClient, ServiceInfo {
    private readonly _transport;
    typeName: string;
    methods: import("@protobuf-ts/runtime-rpc").MethodInfo<any, any>[];
    options: {
        [extensionName: string]: import("@protobuf-ts/runtime").JsonValue;
    };
    constructor(_transport: RpcTransport);
    getGroupId(input: GetGroupIdRequest, options?: RpcOptions): UnaryCall<GetGroupIdRequest, GetGroupIdResponse>;
}
