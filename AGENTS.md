# OpenWorldDaiStar Agent 工作守则

## 适用范围

- 本文件适用于整个 repository；更深层目录中的 `AGENTS.md` 可以增加更严格的局部约束。
- 若根目录存在 `AGENTS.local.md`，开始工作前必须读取；该文件只用于本地补充信息，不得加入 Git、转述到公开文档或复制到生成产物。
- 文档主体使用中文；协议类型、代码符号、路径和工具名称保留原始英文。

## 架构边界

- 服务端保持单一 ASP.NET Core 进程，不拆分额外运行项目。
- 运行时不使用数据库、migration、文件快照或其他持久化机制。
- 账号和 Session 只允许保存在当前进程内存中，进程结束后必须自然清空。
- 不得加入 MasterData 下载、落盘、缓存或本地解析能力，除非用户明确重新授权该架构变更。
- 功能按 `OpenWorldDaiStar.Server/Features/<Name>/` 组织，避免无实际用途的分层和接口。

## IL2CPP 分析前置

- 进行客户端逻辑分析、协议行为确认或 C# 源码还原时，必须使用 [`$il2cpp-to-csharp`](https://github.com/MetaMikuAI/il2cpp-to-csharp-skill) skill。
- 开始分析前必须具备 `stub/`、[`Il2CppDumper`](https://github.com/Perfare/Il2CppDumper) `dump/`，以及完整 pseudo-C 来源；pseudo-C 可以来自预先导出的 `split-c/`，也可以来自已完成分析的 IDA database，并通过 [`IDA Pro MCP`](https://github.com/mrexodia/ida-pro-mcp) 访问。
- `stub/` 用于确认 namespace、类型、继承、方法签名、字段、enum、serialization attributes 和 VA/RVA 注释。
- `dump/script.json` 用于核对方法与地址映射，`dump/stringliteral.json` 用于解析真实字符串；禁止根据上下文猜测字符串。
- 没有 IDA 时，以 `split-c/` 中目标函数的完整 pseudo-C 作为主要行为证据；具备已完成分析的 IDA database 并可通过 `IDA Pro MCP` 访问时，以 IDA pseudo-C 作为主要行为证据。
- pseudo-C 缺失、截断或映射不明确时必须停止相关还原并说明缺口，不得补写未经证明的分支、条件或业务行为。
- skill 中出现的 VA、函数名、类型名和路径均为示例，不得复制为当前客户端证据。

## 通信协议

- 所有通信 model 必须由 `OpenWorldDaiStar.ProtocolGen` 从调用方显式提供的 `stub` 路径机械生成。
- `split-c/` 与 IDA 只用于确认行为，不得替代 `stub/` 成为通信 model 的字段来源。
- 业务代码不得声明或复制 request、response、payload、result、`IDataObject`、enum 或 formatter。
- stub 证据缺失或冲突时停止相关实现，不得使用匿名对象、字典、`object` 或临时 DTO 绕过。
- `OpenWorldDaiStar.Server/Generated/Protocol/` 还受该目录内 `AGENTS.md` 约束。

## 配置与公开边界

- `appsettings.json` 是本地配置，不得加入 Git；可提交配置只使用 `appsettings.example.json`。
- 可提交文件不得包含凭据、Token、私钥、绝对本机路径、非回环 IP 或可直接访问的外部业务 URL。
- example 配置和测试 URL 只允许使用 `127.0.0.1`。
- `docs/` 与 `AGENTS.local.md` 属于本地资料，不得加入 Git。

## 验证

- 修改后至少运行 `dotnet test OpenWorldDaiStar.sln --no-restore -m:1`。
- 提交前运行 `dotnet format OpenWorldDaiStar.sln --verify-no-changes --no-restore`。
- 只有调用方明确提供可用 stub 根目录时，才运行 ProtocolGen 的 `verify`；不得在项目中硬编码本机 stub 绝对路径。
