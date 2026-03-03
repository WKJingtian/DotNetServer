"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.GroupClient = void 0;
const gameoutside_1 = require("./gameoutside");
const runtime_rpc_1 = require("@protobuf-ts/runtime-rpc");
class GroupClient {
    constructor(_transport) {
        this._transport = _transport;
        this.typeName = gameoutside_1.Group.typeName;
        this.methods = gameoutside_1.Group.methods;
        this.options = gameoutside_1.Group.options;
    }
    getGroupId(input, options) {
        const method = this.methods[0], opt = this._transport.mergeOptions(options);
        return (0, runtime_rpc_1.stackIntercept)("unary", this._transport, method, opt, input);
    }
}
exports.GroupClient = GroupClient;
//# sourceMappingURL=gameoutside.client.js.map