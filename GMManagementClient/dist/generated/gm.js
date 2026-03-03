"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.Leaderboard = exports.RemoveFailedInfo = exports.RemovePlayersFromLeaderboardResponse = exports.RemovePlayersFromLeaderboardRequest = exports.Rank = exports.GetLeaderboardWithPaginationResponse = exports.GetLeaderboardWithPaginationRequest = exports.GetLeaderboardTypesResponse = exports.GetLeaderboardTypesRequest = void 0;
const runtime_rpc_1 = require("@protobuf-ts/runtime-rpc");
const runtime_1 = require("@protobuf-ts/runtime");
const runtime_2 = require("@protobuf-ts/runtime");
const runtime_3 = require("@protobuf-ts/runtime");
const runtime_4 = require("@protobuf-ts/runtime");
class GetLeaderboardTypesRequest$Type extends runtime_4.MessageType {
    constructor() {
        super("chillyroom.buildinggame.v1.gm.GetLeaderboardTypesRequest", []);
    }
    create(value) {
        const message = globalThis.Object.create((this.messagePrototype));
        if (value !== undefined)
            (0, runtime_3.reflectionMergePartial)(this, message, value);
        return message;
    }
    internalBinaryRead(reader, length, options, target) {
        let message = target ?? this.create(), end = reader.pos + length;
        while (reader.pos < end) {
            let [fieldNo, wireType] = reader.tag();
            switch (fieldNo) {
                default:
                    let u = options.readUnknownField;
                    if (u === "throw")
                        throw new globalThis.Error(`Unknown field ${fieldNo} (wire type ${wireType}) for ${this.typeName}`);
                    let d = reader.skip(wireType);
                    if (u !== false)
                        (u === true ? runtime_2.UnknownFieldHandler.onRead : u)(this.typeName, message, fieldNo, wireType, d);
            }
        }
        return message;
    }
    internalBinaryWrite(message, writer, options) {
        let u = options.writeUnknownFields;
        if (u !== false)
            (u == true ? runtime_2.UnknownFieldHandler.onWrite : u)(this.typeName, message, writer);
        return writer;
    }
}
exports.GetLeaderboardTypesRequest = new GetLeaderboardTypesRequest$Type();
class GetLeaderboardTypesResponse$Type extends runtime_4.MessageType {
    constructor() {
        super("chillyroom.buildinggame.v1.gm.GetLeaderboardTypesResponse", [
            { no: 1, name: "leaderboard_types", kind: "scalar", repeat: 2, T: 9 }
        ]);
    }
    create(value) {
        const message = globalThis.Object.create((this.messagePrototype));
        message.leaderboardTypes = [];
        if (value !== undefined)
            (0, runtime_3.reflectionMergePartial)(this, message, value);
        return message;
    }
    internalBinaryRead(reader, length, options, target) {
        let message = target ?? this.create(), end = reader.pos + length;
        while (reader.pos < end) {
            let [fieldNo, wireType] = reader.tag();
            switch (fieldNo) {
                case 1:
                    message.leaderboardTypes.push(reader.string());
                    break;
                default:
                    let u = options.readUnknownField;
                    if (u === "throw")
                        throw new globalThis.Error(`Unknown field ${fieldNo} (wire type ${wireType}) for ${this.typeName}`);
                    let d = reader.skip(wireType);
                    if (u !== false)
                        (u === true ? runtime_2.UnknownFieldHandler.onRead : u)(this.typeName, message, fieldNo, wireType, d);
            }
        }
        return message;
    }
    internalBinaryWrite(message, writer, options) {
        for (let i = 0; i < message.leaderboardTypes.length; i++)
            writer.tag(1, runtime_1.WireType.LengthDelimited).string(message.leaderboardTypes[i]);
        let u = options.writeUnknownFields;
        if (u !== false)
            (u == true ? runtime_2.UnknownFieldHandler.onWrite : u)(this.typeName, message, writer);
        return writer;
    }
}
exports.GetLeaderboardTypesResponse = new GetLeaderboardTypesResponse$Type();
class GetLeaderboardWithPaginationRequest$Type extends runtime_4.MessageType {
    constructor() {
        super("chillyroom.buildinggame.v1.gm.GetLeaderboardWithPaginationRequest", [
            { no: 1, name: "leaderboard_type", kind: "scalar", T: 9 },
            { no: 2, name: "rank_start_index", kind: "scalar", T: 5 },
            { no: 3, name: "number_of_ranks", kind: "scalar", T: 5 }
        ]);
    }
    create(value) {
        const message = globalThis.Object.create((this.messagePrototype));
        message.leaderboardType = "";
        message.rankStartIndex = 0;
        message.numberOfRanks = 0;
        if (value !== undefined)
            (0, runtime_3.reflectionMergePartial)(this, message, value);
        return message;
    }
    internalBinaryRead(reader, length, options, target) {
        let message = target ?? this.create(), end = reader.pos + length;
        while (reader.pos < end) {
            let [fieldNo, wireType] = reader.tag();
            switch (fieldNo) {
                case 1:
                    message.leaderboardType = reader.string();
                    break;
                case 2:
                    message.rankStartIndex = reader.int32();
                    break;
                case 3:
                    message.numberOfRanks = reader.int32();
                    break;
                default:
                    let u = options.readUnknownField;
                    if (u === "throw")
                        throw new globalThis.Error(`Unknown field ${fieldNo} (wire type ${wireType}) for ${this.typeName}`);
                    let d = reader.skip(wireType);
                    if (u !== false)
                        (u === true ? runtime_2.UnknownFieldHandler.onRead : u)(this.typeName, message, fieldNo, wireType, d);
            }
        }
        return message;
    }
    internalBinaryWrite(message, writer, options) {
        if (message.leaderboardType !== "")
            writer.tag(1, runtime_1.WireType.LengthDelimited).string(message.leaderboardType);
        if (message.rankStartIndex !== 0)
            writer.tag(2, runtime_1.WireType.Varint).int32(message.rankStartIndex);
        if (message.numberOfRanks !== 0)
            writer.tag(3, runtime_1.WireType.Varint).int32(message.numberOfRanks);
        let u = options.writeUnknownFields;
        if (u !== false)
            (u == true ? runtime_2.UnknownFieldHandler.onWrite : u)(this.typeName, message, writer);
        return writer;
    }
}
exports.GetLeaderboardWithPaginationRequest = new GetLeaderboardWithPaginationRequest$Type();
class GetLeaderboardWithPaginationResponse$Type extends runtime_4.MessageType {
    constructor() {
        super("chillyroom.buildinggame.v1.gm.GetLeaderboardWithPaginationResponse", [
            { no: 1, name: "ranks", kind: "message", repeat: 2, T: () => exports.Rank }
        ]);
    }
    create(value) {
        const message = globalThis.Object.create((this.messagePrototype));
        message.ranks = [];
        if (value !== undefined)
            (0, runtime_3.reflectionMergePartial)(this, message, value);
        return message;
    }
    internalBinaryRead(reader, length, options, target) {
        let message = target ?? this.create(), end = reader.pos + length;
        while (reader.pos < end) {
            let [fieldNo, wireType] = reader.tag();
            switch (fieldNo) {
                case 1:
                    message.ranks.push(exports.Rank.internalBinaryRead(reader, reader.uint32(), options));
                    break;
                default:
                    let u = options.readUnknownField;
                    if (u === "throw")
                        throw new globalThis.Error(`Unknown field ${fieldNo} (wire type ${wireType}) for ${this.typeName}`);
                    let d = reader.skip(wireType);
                    if (u !== false)
                        (u === true ? runtime_2.UnknownFieldHandler.onRead : u)(this.typeName, message, fieldNo, wireType, d);
            }
        }
        return message;
    }
    internalBinaryWrite(message, writer, options) {
        for (let i = 0; i < message.ranks.length; i++)
            exports.Rank.internalBinaryWrite(message.ranks[i], writer.tag(1, runtime_1.WireType.LengthDelimited).fork(), options).join();
        let u = options.writeUnknownFields;
        if (u !== false)
            (u == true ? runtime_2.UnknownFieldHandler.onWrite : u)(this.typeName, message, writer);
        return writer;
    }
}
exports.GetLeaderboardWithPaginationResponse = new GetLeaderboardWithPaginationResponse$Type();
class Rank$Type extends runtime_4.MessageType {
    constructor() {
        super("chillyroom.buildinggame.v1.gm.Rank", [
            { no: 1, name: "player_id", kind: "scalar", T: 3 },
            { no: 2, name: "score", kind: "scalar", T: 3 }
        ]);
    }
    create(value) {
        const message = globalThis.Object.create((this.messagePrototype));
        message.playerId = "0";
        message.score = "0";
        if (value !== undefined)
            (0, runtime_3.reflectionMergePartial)(this, message, value);
        return message;
    }
    internalBinaryRead(reader, length, options, target) {
        let message = target ?? this.create(), end = reader.pos + length;
        while (reader.pos < end) {
            let [fieldNo, wireType] = reader.tag();
            switch (fieldNo) {
                case 1:
                    message.playerId = reader.int64().toString();
                    break;
                case 2:
                    message.score = reader.int64().toString();
                    break;
                default:
                    let u = options.readUnknownField;
                    if (u === "throw")
                        throw new globalThis.Error(`Unknown field ${fieldNo} (wire type ${wireType}) for ${this.typeName}`);
                    let d = reader.skip(wireType);
                    if (u !== false)
                        (u === true ? runtime_2.UnknownFieldHandler.onRead : u)(this.typeName, message, fieldNo, wireType, d);
            }
        }
        return message;
    }
    internalBinaryWrite(message, writer, options) {
        if (message.playerId !== "0")
            writer.tag(1, runtime_1.WireType.Varint).int64(message.playerId);
        if (message.score !== "0")
            writer.tag(2, runtime_1.WireType.Varint).int64(message.score);
        let u = options.writeUnknownFields;
        if (u !== false)
            (u == true ? runtime_2.UnknownFieldHandler.onWrite : u)(this.typeName, message, writer);
        return writer;
    }
}
exports.Rank = new Rank$Type();
class RemovePlayersFromLeaderboardRequest$Type extends runtime_4.MessageType {
    constructor() {
        super("chillyroom.buildinggame.v1.gm.RemovePlayersFromLeaderboardRequest", [
            { no: 1, name: "leaderboard_types", kind: "scalar", repeat: 2, T: 9 },
            { no: 2, name: "player_ids", kind: "scalar", repeat: 1, T: 3 }
        ]);
    }
    create(value) {
        const message = globalThis.Object.create((this.messagePrototype));
        message.leaderboardTypes = [];
        message.playerIds = [];
        if (value !== undefined)
            (0, runtime_3.reflectionMergePartial)(this, message, value);
        return message;
    }
    internalBinaryRead(reader, length, options, target) {
        let message = target ?? this.create(), end = reader.pos + length;
        while (reader.pos < end) {
            let [fieldNo, wireType] = reader.tag();
            switch (fieldNo) {
                case 1:
                    message.leaderboardTypes.push(reader.string());
                    break;
                case 2:
                    if (wireType === runtime_1.WireType.LengthDelimited)
                        for (let e = reader.int32() + reader.pos; reader.pos < e;)
                            message.playerIds.push(reader.int64().toString());
                    else
                        message.playerIds.push(reader.int64().toString());
                    break;
                default:
                    let u = options.readUnknownField;
                    if (u === "throw")
                        throw new globalThis.Error(`Unknown field ${fieldNo} (wire type ${wireType}) for ${this.typeName}`);
                    let d = reader.skip(wireType);
                    if (u !== false)
                        (u === true ? runtime_2.UnknownFieldHandler.onRead : u)(this.typeName, message, fieldNo, wireType, d);
            }
        }
        return message;
    }
    internalBinaryWrite(message, writer, options) {
        for (let i = 0; i < message.leaderboardTypes.length; i++)
            writer.tag(1, runtime_1.WireType.LengthDelimited).string(message.leaderboardTypes[i]);
        if (message.playerIds.length) {
            writer.tag(2, runtime_1.WireType.LengthDelimited).fork();
            for (let i = 0; i < message.playerIds.length; i++)
                writer.int64(message.playerIds[i]);
            writer.join();
        }
        let u = options.writeUnknownFields;
        if (u !== false)
            (u == true ? runtime_2.UnknownFieldHandler.onWrite : u)(this.typeName, message, writer);
        return writer;
    }
}
exports.RemovePlayersFromLeaderboardRequest = new RemovePlayersFromLeaderboardRequest$Type();
class RemovePlayersFromLeaderboardResponse$Type extends runtime_4.MessageType {
    constructor() {
        super("chillyroom.buildinggame.v1.gm.RemovePlayersFromLeaderboardResponse", [
            { no: 1, name: "failed_list", kind: "message", repeat: 2, T: () => exports.RemoveFailedInfo }
        ]);
    }
    create(value) {
        const message = globalThis.Object.create((this.messagePrototype));
        message.failedList = [];
        if (value !== undefined)
            (0, runtime_3.reflectionMergePartial)(this, message, value);
        return message;
    }
    internalBinaryRead(reader, length, options, target) {
        let message = target ?? this.create(), end = reader.pos + length;
        while (reader.pos < end) {
            let [fieldNo, wireType] = reader.tag();
            switch (fieldNo) {
                case 1:
                    message.failedList.push(exports.RemoveFailedInfo.internalBinaryRead(reader, reader.uint32(), options));
                    break;
                default:
                    let u = options.readUnknownField;
                    if (u === "throw")
                        throw new globalThis.Error(`Unknown field ${fieldNo} (wire type ${wireType}) for ${this.typeName}`);
                    let d = reader.skip(wireType);
                    if (u !== false)
                        (u === true ? runtime_2.UnknownFieldHandler.onRead : u)(this.typeName, message, fieldNo, wireType, d);
            }
        }
        return message;
    }
    internalBinaryWrite(message, writer, options) {
        for (let i = 0; i < message.failedList.length; i++)
            exports.RemoveFailedInfo.internalBinaryWrite(message.failedList[i], writer.tag(1, runtime_1.WireType.LengthDelimited).fork(), options).join();
        let u = options.writeUnknownFields;
        if (u !== false)
            (u == true ? runtime_2.UnknownFieldHandler.onWrite : u)(this.typeName, message, writer);
        return writer;
    }
}
exports.RemovePlayersFromLeaderboardResponse = new RemovePlayersFromLeaderboardResponse$Type();
class RemoveFailedInfo$Type extends runtime_4.MessageType {
    constructor() {
        super("chillyroom.buildinggame.v1.gm.RemoveFailedInfo", [
            { no: 1, name: "leaderboard_type", kind: "scalar", T: 9 },
            { no: 2, name: "player_id", kind: "scalar", T: 3 },
            { no: 3, name: "reason", kind: "scalar", T: 9 }
        ]);
    }
    create(value) {
        const message = globalThis.Object.create((this.messagePrototype));
        message.leaderboardType = "";
        message.playerId = "0";
        message.reason = "";
        if (value !== undefined)
            (0, runtime_3.reflectionMergePartial)(this, message, value);
        return message;
    }
    internalBinaryRead(reader, length, options, target) {
        let message = target ?? this.create(), end = reader.pos + length;
        while (reader.pos < end) {
            let [fieldNo, wireType] = reader.tag();
            switch (fieldNo) {
                case 1:
                    message.leaderboardType = reader.string();
                    break;
                case 2:
                    message.playerId = reader.int64().toString();
                    break;
                case 3:
                    message.reason = reader.string();
                    break;
                default:
                    let u = options.readUnknownField;
                    if (u === "throw")
                        throw new globalThis.Error(`Unknown field ${fieldNo} (wire type ${wireType}) for ${this.typeName}`);
                    let d = reader.skip(wireType);
                    if (u !== false)
                        (u === true ? runtime_2.UnknownFieldHandler.onRead : u)(this.typeName, message, fieldNo, wireType, d);
            }
        }
        return message;
    }
    internalBinaryWrite(message, writer, options) {
        if (message.leaderboardType !== "")
            writer.tag(1, runtime_1.WireType.LengthDelimited).string(message.leaderboardType);
        if (message.playerId !== "0")
            writer.tag(2, runtime_1.WireType.Varint).int64(message.playerId);
        if (message.reason !== "")
            writer.tag(3, runtime_1.WireType.LengthDelimited).string(message.reason);
        let u = options.writeUnknownFields;
        if (u !== false)
            (u == true ? runtime_2.UnknownFieldHandler.onWrite : u)(this.typeName, message, writer);
        return writer;
    }
}
exports.RemoveFailedInfo = new RemoveFailedInfo$Type();
exports.Leaderboard = new runtime_rpc_1.ServiceType("chillyroom.buildinggame.v1.gm.Leaderboard", [
    { name: "GetLeaderboardTypes", options: {}, I: exports.GetLeaderboardTypesRequest, O: exports.GetLeaderboardTypesResponse },
    { name: "GetLeaderboardWithPagination", options: {}, I: exports.GetLeaderboardWithPaginationRequest, O: exports.GetLeaderboardWithPaginationResponse },
    { name: "RemovePlayersFromLeaderboard", options: {}, I: exports.RemovePlayersFromLeaderboardRequest, O: exports.RemovePlayersFromLeaderboardResponse }
]);
//# sourceMappingURL=gm.js.map