# WordLens 功能实施计划

## 项目时间线

```mermaid
gantt
    title WordLens 功能增强实施计划
    dateFormat  YYYY-MM-DD
    section 阶段一：数据模型
    更新ProviderConfig添加IsEnabled          :a1, 2025-01-01, 1d
    创建TranslationResult模型                :a2, after a1, 1d
    创建OcrHotkeyService                     :a3, after a1, 1d
    更新HotkeyManagerService                 :a4, after a3, 1d
    
    section 阶段二：翻译服务
    重构TranslationService                   :b1, after a4, 2d
    实现并行翻译逻辑                          :b2, after b1, 2d
    添加异常处理                             :b3, after b2, 1d
    
    section 阶段三：UI更新
    更新PopupWindowViewModel                 :c1, after b3, 2d
    重新设计PopupWindowView                  :c2, after c1, 2d
    更新SettingsViewModel                    :c3, after c1, 1d
    
    section 阶段四：日志集成
    配置ZLogger                              :d1, after c2, 1d
    添加服务日志                             :d2, after d1, 2d
    
    section 阶段五：测试
    单元测试                                 :e1, after d2, 2d
    集成测试                                 :e2, after e1, 2d
    文档完善                                 :e3, after e2, 1d
```

## 架构流程图

### 翻译流程

```mermaid
flowchart TD
    A[用户按下快捷键] --> B{HotkeyManagerService}
    B -->|翻译快捷键| C[HotkeyService]
    B -->|OCR快捷键| D[OcrHotkeyService预留]
    
    C --> E[SelectionService获取选中文本]
    E --> F[发送ShowPopupMessage]
    
    F --> G[ApplicationViewModel接收消息]
    G --> H[创建PopupWindowView]
    H --> I[PopupWindowViewModel初始化]
    
    I --> J[调用TranslateAsync]
    J --> K[TranslationService.TranslateAsync]
    
    K --> L{获取启用的翻译源}
    L -->|翻译源1| M1[并行任务1]
    L -->|翻译源2| M2[并行任务2]
    L -->|翻译源N| MN[并行任务N]
    
    M1 --> N1[OpenAIProvider.TranslateAsync]
    M2 --> N2[OpenAIProvider.TranslateAsync]
    MN --> NN[OpenAIProvider.TranslateAsync]
    
    N1 --> O1{翻译成功?}
    N2 --> O2{翻译成功?}
    NN --> ON{翻译成功?}
    
    O1 -->|是| P1[TranslationResult成功]
    O1 -->|否| Q1[TranslationResult失败+错误信息]
    O2 -->|是| P2[TranslationResult成功]
    O2 -->|否| Q2[TranslationResult失败+错误信息]
    ON -->|是| PN[TranslationResult成功]
    ON -->|否| QN[TranslationResult失败+错误信息]
    
    P1 --> R[Task.WhenAll等待所有任务]
    Q1 --> R
    P2 --> R
    Q2 --> R
    PN --> R
    QN --> R
    
    R --> S[返回List TranslationResult]
    S --> T[更新PopupWindowViewModel.TranslationResults]
    T --> U[UI显示所有翻译结果]
    
    style K fill:#e1f5ff
    style R fill:#e1f5ff
    style U fill:#d4edda
```

### 日志记录流程

```mermaid
flowchart LR
    A[应用启动] --> B[Program.cs配置ZLogger]
    B --> C[创建日志目录]
    C --> D[配置文件日志输出]
    D --> E[注册到DI容器]
    
    E --> F1[HotkeyService]
    E --> F2[TranslationService]
    E --> F3[SettingsService]
    E --> F4[OcrHotkeyService]
    
    F1 -->|热键触发| G1[logger.ZLogInformation]
    F2 -->|翻译开始/成功/失败| G2[logger.ZLogInformation/Warning/Error]
    F3 -->|配置加载/保存| G3[logger.ZLogInformation]
    F4 -->|OCR热键触发| G4[logger.ZLogInformation]
    
    G1 --> H[日志文件: wordlens-yyyyMMdd.log]
    G2 --> H
    G3 --> H
    G4 --> H
    
    style B fill:#fff3cd
    style H fill:#d4edda
```

### 配置管理流程

```mermaid
flowchart TD
    A[应用启动] --> B[SettingsService.LoadAsync]
    B --> C{配置文件存在?}
    
    C -->|是| D[读取JSON文件]
    C -->|否| E[使用默认配置]
    
    D --> F[反序列化为AppSettings]
    E --> F
    
    F --> G[包含ProviderConfig列表]
    G --> H{每个Provider}
    H --> I[检查IsEnabled属性]
    
    I -->|true| J[启用的翻译源]
    I -->|false| K[禁用的翻译源]
    
    L[用户修改设置] --> M[SettingsViewModel.SaveSettingsAsync]
    M --> N[SettingsService.SaveAsync]
    N --> O[序列化为JSON]
    O --> P[写入配置文件]
    P --> Q[记录日志]
    
    style F fill:#e1f5ff
    style P fill:#d4edda
```

## 数据流图

### TranslationResult 数据流

```mermaid
flowchart LR
    A[TranslationService] -->|创建| B[TranslationResult对象]
    B -->|ProviderName| C[翻译源名称]
    B -->|Result| D[翻译文本]
    B -->|IsSuccess| E[成功状态]
    B -->|ErrorMessage| F[错误信息]
    B -->|IsLoading| G[加载状态]
    
    B --> H[List TranslationResult]
    H --> I[PopupWindowViewModel]
    I --> J[ObservableCollection TranslationResult]
    J --> K[UI ItemsControl]
    
    K --> L1[翻译源1卡片]
    K --> L2[翻译源2卡片]
    K --> L3[翻译源N卡片]
    
    style B fill:#e1f5ff
    style J fill:#d4edda
```

## 组件依赖关系

```mermaid
graph TD
    A[Program.cs] --> B[App.axaml.cs]
    B --> C[ServiceCollection]
    
    C --> D1[ILogger]
    C --> D2[ISettingsService]
    C --> D3[IHotkeyService]
    C --> D4[IOcrHotkeyService]
    C --> D5[IHttpClientFactory]
    
    D2 --> E1[SettingsService]
    D3 --> E2[HotkeyService]
    D4 --> E3[OcrHotkeyService]
    
    E1 --> F1[TranslationService]
    E2 --> F2[HotkeyManagerService]
    E3 --> F2
    D5 --> F1
    D1 --> F1
    D1 --> F2
    D1 --> E1
    D1 --> E2
    D1 --> E3
    
    F2 --> G1[SelectionService]
    
    F1 --> H1[PopupWindowViewModel]
    F2 --> H2[ApplicationViewModel]
    E1 --> H3[SettingsViewModel]
    E2 --> H3
    E3 --> H3
    
    H1 --> I1[PopupWindowView]
    H3 --> I2[MainWindowView]
    
    style C fill:#fff3cd
    style F1 fill:#e1f5ff
    style F2 fill:#e1f5ff
```

## UI 组件层次结构

```mermaid
graph TD
    A[MainWindow] --> B[SettingsView]
    
    B --> C1[快捷键设置区域]
    C1 --> C11[翻译快捷键输入框]
    C1 --> C12[OCR快捷键输入框]
    
    B --> C2[翻译源管理区域]
    C2 --> C21[翻译源列表]
    C21 --> C211[翻译源项]
    C211 --> C2111[名称]
    C211 --> C2112[BaseUrl]
    C211 --> C2113[ApiKey]
    C211 --> C2114[Model]
    C211 --> C2115[IsEnabled复选框]
    
    B --> C3[代理设置区域]
    
    D[PopupWindow] --> E[工具栏]
    D --> F[原文区域]
    D --> G[翻译结果区域]
    
    G --> H[ItemsControl]
    H --> I1[翻译结果卡片1]
    H --> I2[翻译结果卡片2]
    H --> IN[翻译结果卡片N]
    
    I1 --> J11[翻译源名称]
    I1 --> J12[状态图标]
    I1 --> J13[翻译文本/错误信息]
    
    style B fill:#e1f5ff
    style G fill:#d4edda
    style C2115 fill:#fff3cd
```

## 关键决策点

### 1. 多翻译源实现方式

**选择：并行异步请求（Task.WhenAll）**

优点：
- ✅ 最快的响应速度
- ✅ 用户体验最好
- ✅ 充分利用异步能力

缺点：
- ⚠️ 可能同时产生多个HTTP请求
- ⚠️ 需要处理部分成功的情况

替代方案：
- 串行请求：速度慢，但简单
- 优先级队列：复杂度高

### 2. UI显示方式

**选择：垂直列表（ItemsControl）**

优点：
- ✅ 可以同时看到所有结果
- ✅ 便于对比不同翻译
- ✅ 滚动查看更多结果

缺点：
- ⚠️ 占用更多垂直空间

替代方案：
- TabControl：节省空间，但需要切换标签

### 3. 日志框架

**选择：ZLogger**

优点：
- ✅ 高性能结构化日志
- ✅ 支持异步写入
- ✅ 零分配日志记录
- ✅ 已在项目中使用

### 4. OCR功能

**选择：预留接口，暂不实现**

理由：
- 需要更多时间评估OCR引擎
- 可以在后续版本中添加
- 不影响当前核心功能

## 风险管理

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| API限流导致请求失败 | 中 | 中 | 添加重试机制和错误提示 |
| 多个翻译源同时失败 | 低 | 高 | 至少保留一个可用的翻译源 |
| 配置文件损坏 | 低 | 中 | 配置文件备份和验证 |
| 日志文件过大 | 中 | 低 | 实现日志轮转和清理策略 |
| 内存泄漏 | 低 | 高 | 正确处理IDisposable和事件订阅 |

## 成功标准

### 功能性标准
- ✅ OCR热键可以正确捕获和保存
- ✅ 多个翻译源可以并行请求
- ✅ UI正确显示所有翻译结果
- ✅ 翻译源可以单独启用/禁用
- ✅ 日志正确记录到文件

### 性能标准
- ⏱️ 翻译响应时间 < 5秒（单个翻译源）
- ⏱️ UI更新无明显延迟
- 💾 内存使用 < 100MB
- 📝 日志写入不影响主线程

### 质量标准
- 🧪 单元测试覆盖率 > 70%
- 🐛 零已知的Critical Bug
- 📚 完整的技术文档
- 🎨 符合现有UI设计风格

## 下一步行动

准备好开始实施了吗？我建议按以下顺序进行：

1. **先实施数据模型和服务层**（较为独立，风险低）
2. **然后更新UI层**（依赖于服务层）
3. **最后添加日志**（可以逐步添加）

现在可以切换到 Code 模式开始实施代码了！