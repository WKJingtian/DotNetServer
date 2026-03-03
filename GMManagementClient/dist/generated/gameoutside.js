"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.Group = exports.GetGroupIdResponse = exports.GetGroupIdRequest = void 0;
const runtime_rpc_1 = require("@protobuf-ts/runtime-rpc");
const runtime_1 = require("@protobuf-ts/runtime");
const runtime_2 = require("@protobuf-ts/runtime");
const runtime_3 = require("@protobuf-ts/runtime");
const runtime_4 = require("@protobuf-ts/runtime");
class GetGroupIdRequest$Type extends runtime_4.MessageType {
    constructor() {
        super("chillyroom.buildinggame.v1.GetGroupIdRequest", [
            { no: 1, name: "season_number", kind: "scalar", T: 5 },
            { no: 2, name: "division_number", kind: "scalar", T: 5 }
        ]);
    }
    create(value) {
        const message = globalThis.Object.create((this.messagePrototype));
        message.seasonNumber = 0;
        message.divisionNumber = 0;
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
                    message.seasonNumber = reader.int32();
                    break;
                case 2:
                    message.divisionNumber = reader.int32();
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
        if (message.seasonNumber !== 0)
            writer.tag(1, runtime_1.WireType.Varint).int32(message.seasonNumber);
        if (message.divisionNumber !== 0)
            writer.tag(2, runtime_1.WireType.Varint).int32(message.divisionNumber);
        let u = options.writeUnknownFields;
        if (u !== false)
            (u == true ? runtime_2.UnknownFieldHandler.onWrite : u)(this.typeName, message, writer);
        return writer;
    }
}
exports.GetGroupIdRequest = new GetGroupIdRequest$Type();
class GetGroupIdResponse$Type extends runtime_4.MessageType {
    constructor() {
        super("chillyroom.buildinggame.v1.GetGroupIdResponse", [
            { no: 1, name: "group_id", kind: "scalar", T: 5 }
        ]);
    }
    create(value) {
        const message = globalThis.Object.create((this.messagePrototype));
        message.groupId = 0;
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
                    message.groupId = reader.int32();
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
        if (message.groupId !== 0)
            writer.tag(1, runtime_1.WireType.Varint).int32(message.groupId);
        let u = options.writeUnknownFields;
        if (u !== false)
            (u == true ? runtime_2.UnknownFieldHandler.onWrite : u)(this.typeName, message, writer);
        return writer;
    }
}
exports.GetGroupIdResponse = new GetGroupIdResponse$Type();
exports.Group = new runtime_rpc_1.ServiceType("chillyroom.buildinggame.v1.Group", [
    { name: "GetGroupId", options: {}, I: exports.GetGroupIdRequest, O: exports.GetGroupIdResponse }
]);
//# sourceMappingURL=gameoutside.js.map