import * as grpc from '@grpc/grpc-js'
import { GrpcOptions, GrpcTransport } from '@protobuf-ts/grpc-transport'
import { RpcOptions } from "@protobuf-ts/runtime-rpc";


import { LeaderboardClient } from './generated/gm.client'


type GrpcOptionsWithoutHost = RpcOptions & Pick<GrpcOptions, "channelCredentials" | "clientOptions">;


export const createGrpcClient = (endpoint: string, grpcOptions: GrpcOptionsWithoutHost) => {
  if (!endpoint) {
    throw new Error('GrpcClient endpoint is required')
  }

  if (!grpcOptions.channelCredentials) {
    grpcOptions.channelCredentials = grpc.credentials.createSsl(null, null, null, {
      checkServerIdentity: () => null,
      rejectUnauthorized: false,
    });
  }

  const transport = new GrpcTransport({
    host: endpoint,
    ...grpcOptions,
  });

  

  
  return new LeaderboardClient(transport);
  


}
