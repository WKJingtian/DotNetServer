import { GrpcOptions } from '@protobuf-ts/grpc-transport';
import { RpcOptions } from "@protobuf-ts/runtime-rpc";
import { LeaderboardClient } from './generated/gm.client';
type GrpcOptionsWithoutHost = RpcOptions & Pick<GrpcOptions, "channelCredentials" | "clientOptions">;
export declare const createGrpcClient: (endpoint: string, grpcOptions: GrpcOptionsWithoutHost) => LeaderboardClient;
export {};
