# 生成协议目录约束

- 本目录只接受 `OpenWorldDaiStar.ProtocolGen` 生成的 `*.g.cs`、`protocol.lock.json` 和本约束文件。
- 禁止手写或直接修改 request、response、payload、result、`IDataObject`、enum、formatter 及其他生成代码。
- 协议结构的唯一来源是调用方通过 `--stub-root` 显式提供的 stub；缺失或冲突时必须停止生成，不得推测字段。
- 需要改变协议闭包时，只修改 `OpenWorldDaiStar.ProtocolGen/protocol-roots.json`，然后重新运行生成器。
- 生成 source 与 `protocol.lock.json` 必须由同一次生成产生，并作为同一组变更提交。
- 完成生成后必须运行 ProtocolGen `verify`，确认输入 hash、类型 shape 和输出 hash 一致。
