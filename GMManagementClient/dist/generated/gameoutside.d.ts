import { ServiceType } from "@protobuf-ts/runtime-rpc";
import type { BinaryWriteOptions } from "@protobuf-ts/runtime";
import type { IBinaryWriter } from "@protobuf-ts/runtime";
import type { BinaryReadOptions } from "@protobuf-ts/runtime";
import type { IBinaryReader } from "@protobuf-ts/runtime";
import type { PartialMessage } from "@protobuf-ts/runtime";
import { MessageType } from "@protobuf-ts/runtime";
export interface GetGroupIdRequest {
    seasonNumber: number;
    divisionNumber: number;
}
export interface GetGroupIdResponse {
    groupId: number;
}
declare class GetGroupIdRequest$Type extends MessageType<GetGroupIdRequest> {
    constructor();
    create(value?: PartialMessage<GetGroupIdRequest>): GetGroupIdRequest;
    internalBinaryRead(reader: IBinaryReader, length: number, options: BinaryReadOptions, target?: GetGroupIdRequest): GetGroupIdRequest;
    internalBinaryWrite(message: GetGroupIdRequest, writer: IBinaryWriter, options: BinaryWriteOptions): IBinaryWriter;
}
export declare const GetGroupIdRequest: GetGroupIdRequest$Type;
declare class GetGroupIdResponse$Type extends MessageType<GetGroupIdResponse> {
    constructor();
    create(value?: PartialMessage<GetGroupIdResponse>): GetGroupIdResponse;
    internalBinaryRead(reader: IBinaryReader, length: number, options: BinaryReadOptions, target?: GetGroupIdResponse): GetGroupIdResponse;
    internalBinaryWrite(message: GetGroupIdResponse, writer: IBinaryWriter, options: BinaryWriteOptions): IBinaryWriter;
}
export declare const GetGroupIdResponse: GetGroupIdResponse$Type;
export declare const Group: ServiceType;
export {};
