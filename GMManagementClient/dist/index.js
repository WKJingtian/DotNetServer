"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.createGrpcClient = void 0;
const grpc = require("@grpc/grpc-js");
const grpc_transport_1 = require("@protobuf-ts/grpc-transport");
const gm_client_1 = require("./generated/gm.client");
const createGrpcClient = (endpoint, grpcOptions) => {
    if (!endpoint) {
        throw new Error('GrpcClient endpoint is required');
    }
    if (!grpcOptions.channelCredentials) {
        grpcOptions.channelCredentials = grpc.credentials.createSsl(null, null, null, {
            checkServerIdentity: () => null,
            rejectUnauthorized: false,
        });
    }
    const transport = new grpc_transport_1.GrpcTransport({
        host: endpoint,
        ...grpcOptions,
    });
    return new gm_client_1.LeaderboardClient(transport);
};
exports.createGrpcClient = createGrpcClient;
//# sourceMappingURL=index.js.map