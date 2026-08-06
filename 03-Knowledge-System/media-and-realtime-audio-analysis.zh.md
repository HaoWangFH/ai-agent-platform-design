# 媒体、视觉与实时语音架构分析报告
# Media, Vision & Realtime Audio Architecture Analysis

本文档记录了 AI EOS 智能体平台中 **媒体（视觉与语音）** 功能的设计分析与技术路线图，对比了当前基于文件的检测与未来 **实时语音流（Live Realtime Audio Streaming）** 的架构设计。

---

## 1. 当前媒体与语音功能矩阵

平台目前为本地媒体文件处理提供了完整的 **C#** 和 **F#** 实现：

| 功能 / 工具名称 | 能力描述 | 支持的大模型 | 实现状态 |
| :--- | :--- | :--- | :--- |
| `inspect_image` | 检测本地图像（`.png`、`.jpg`、`.webp`、`.svg`），识别 MIME 类型与大小，输出 Base64 Data URI（`data:image/png;base64,...`）。 | `gpt-4o`、`gpt-4o-mini`、`claude-3-5-sonnet`、`gemini-1.5-pro` | ✅ 已完成（`MediaTools.cs` / `MediaTools.fs`） |
| `inspect_audio` | 检测语音文件（`.mp3`、`.wav`、`.m4a`、`.ogg`、`.flac`），提取大小与 MIME 类型，输出 Base64 Data URI（`data:audio/mp3;base64,...`）。 | `gemini-1.5-pro`、`gpt-4o-audio-preview` | ✅ 已完成（`MediaTools.cs` / `MediaTools.fs`） |
| `transcribe_audio` | 语音转文本（STT），将语音文件转译为文本 Payload。 | OpenAI `whisper-1`、本地 Whisper | ✅ 已完成（`MediaTools.cs` / `MediaTools.fs`） |
| `text_to_speech` | 文本转语音（TTS），将文本提示合成语言 `.mp3` 声音文件。 | OpenAI `tts-1`、`tts-1-hd` | ✅ 已完成（`MediaTools.cs` / `MediaTools.fs`） |

---

## 2. 大模型支持分析

### 🖼️ 视觉模型
- **Azure / OpenAI `gpt-4o`** *（`.env` 中的默认配置）*：原生高分辨率视觉支持。
- **Anthropic `claude-3-5-sonnet`**：在复杂软件工程架构图与 UI 原型推断方面表现卓越。
- **Google `gemini-1.5-pro`**：超大上下文窗口，原生支持多模态图像与文档。

### 🎧 语音模型
- **`whisper-1`**：快速离线/文件语音转文字模型。
- **`tts-1` / `tts-1-hd`**：声音合成输出模型。

---

## 3. 未来路线图：实时语音流（Live Realtime Audio Streaming）

对于全双工实时语音交互（<300ms 延迟），将使用连续流协议取代标准的 HTTP REST 端点。

### ⚡ 架构对比

| 维度 | 预录制音频文件（当前实现） | 实时语音流（未来规划） |
| :--- | :--- | :--- |
| **通信协议** | HTTP REST API / JSON 函数调用 | WebSockets (`wss://`) / WebRTC |
| **Azure 模型部署** | `gpt-4o` | `gpt-4o-realtime-preview` / `gpt-4o-mini-realtime-preview` |
| **响应延迟** | 1.0 – 3.0 秒 | 200 – 400 毫秒 |
| **数据格式** | 磁盘上的 `.mp3`/`.wav` 文件 | 连续 PCM 16-bit 24kHz 原始音频缓冲区 |
| **交互样式** | 轮次式命令执行 | 全双工语音输入与语音输出 |

---

## 4. 实时语音技术实现蓝图

后续迭代开发实时语音时，按以下架构图构建组件：

```
[ 麦克风 PCM 输入 ] ---> [ .NET ClientWebSocket ] ---> [ Azure gpt-4o-realtime-preview (WSS) ]
                                                                             |
[ 扬声器音频输出 ] <--- [ 音频播放缓冲区 ] <--- [ 语音 PCM 数据块 ]
```

1. **Azure 模型部署**：
   - 在 Azure OpenAI 门户中部署 `gpt-4o-realtime-preview` 模型。

2. **C# / F# WebSocket 客户端 (`RealtimeAudioClient`)**：
   - 使用 `System.Net.WebSockets.ClientWebSocket`。
   - 发送 `session.update` 事件，配置声音音色（`alloy`, `echo`, `shimmer`）与函数工具定义。

3. **音频 I/O 驱动**：
   - 使用 NAudio / PortAudio 采集麦克风 PCM 音频帧。
   - 将接收到的 PCM 数据块直接送入系统扬声器进行即时语音播放。
