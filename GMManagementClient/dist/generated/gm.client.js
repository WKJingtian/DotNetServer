"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.LeaderboardClient = void 0;
const gm_1 = require("./gm");
const runtime_rpc_1 = require("@protobuf-ts/runtime-rpc");
class LeaderboardClient {
    constructor(_transport) {
        this._transport = _transport;
        this.typeName = gm_1.Leaderboard.typeName;
        this.methods = gm_1.Leaderboard.methods;
        this.options = gm_1.Leaderboard.options;
    }
    getLeaderboardTypes(input, options) {
        const method = this.methods[0], opt = this._transport.mergeOptions(options);
        return (0, runtime_rpc_1.stackIntercept)("unary", this._transport, method, opt, input);
    }
    getLeaderboardWithPagination(input, options) {
        const method = this.methods[1], opt = this._transport.mergeOptions(options);
        return (0, runtime_rpc_1.stackIntercept)("unary", this._transport, method, opt, input);
    }
    removePlayersFromLeaderboard(input, options) {
        const method = this.methods[2], opt = this._transport.mergeOptions(options);
        return (0, runtime_rpc_1.stackIntercept)("unary", this._transport, method, opt, input);
    }
}
exports.LeaderboardClient = LeaderboardClient;
//# sourceMappingURL=gm.client.js.map