# Media, Vision & Realtime Audio Architecture Analysis
# 媒体、视觉与实时语音架构分析报告

This document captures the design analysis and technical roadmap for **Media (Vision & Audio)** capabilities in the AI EOS Agent Platform, comparing current file-based inspection against future **Live Realtime Audio Streaming**.

---

## 1. Current Media & Audio Capability Matrix

The platform currently provides full **C#** and **F#** implementations for local media file processing:

| Feature / Tool Name | Capability | Supporting Models | Implementation Status |
| :--- | :--- | :--- | :--- |
| `inspect_image` | Inspects local images (`.png`, `.jpg`, `.webp`, `.svg`), detects MIME type, size, and outputs Base64 Data URIs (`data:image/png;base64,...`). | `gpt-4o`, `gpt-4o-mini`, `claude-3-5-sonnet`, `gemini-1.5-pro` | ✅ Completed (`MediaTools.cs` / `MediaTools.fs`) |
| `inspect_audio` | Inspects audio files (`.mp3`, `.wav`, `.m4a`, `.ogg`, `.flac`), extracts size, MIME type, and outputs Base64 Data URIs (`data:audio/mp3;base64,...`). | `gemini-1.5-pro`, `gpt-4o-audio-preview` | ✅ Completed (`MediaTools.cs` / `MediaTools.fs`) |
| `transcribe_audio` | Speech-to-Text (STT) transcription converting audio files into text payload. | OpenAI `whisper-1`, Local Whisper | ✅ Completed (`MediaTools.cs` / `MediaTools.fs`) |
| `text_to_speech` | Text-to-Speech (TTS) synthesizing text prompts into spoken `.mp3` audio files. | OpenAI `tts-1`, `tts-1-hd` | ✅ Completed (`MediaTools.cs` / `MediaTools.fs`) |

---

## 2. Model Support Analysis

### 🖼️ Vision Models
- **Azure / OpenAI `gpt-4o`** *(Default in `.env`)*: High-resolution native vision support.
- **Anthropic `claude-3-5-sonnet`**: Superior visual engineering diagram and UI mockup reasoning.
- **Google `gemini-1.5-pro`**: Large context window with native multimodal image and document support.

### 🎧 Audio Models
- **`whisper-1`**: Fast offline/file transcription model.
- **`tts-1` / `tts-1-hd`**: Voice synthesis for audio outputs.

---

## 3. Future Roadmap: Live Realtime Audio Streaming

For full-duplex conversational voice (<300ms latency), standard HTTP REST endpoints are replaced with continuous streaming protocols.

### ⚡ Architectural Comparison

| Dimension | Pre-Recorded Audio (Current) | Live Realtime Audio (Future) |
| :--- | :--- | :--- |
| **Protocol** | HTTP REST API / JSON Function Calling | WebSockets (`wss://`) / WebRTC |
| **Azure Model Deployment** | `gpt-4o` | `gpt-4o-realtime-preview` / `gpt-4o-mini-realtime-preview` |
| **Latency** | 1.0 – 3.0 seconds | 200 – 400 milliseconds |
| **Data Format** | Local `.mp3`/`.wav` files on disk | Continuous PCM 16-bit 24kHz raw audio buffer |
| **Interaction Style** | Turn-based command execution | Full-duplex speech-in, speech-out |

---

## 4. Technical Implementation Blueprint for Live Audio

When picking up Live Realtime Audio in future iterations, implement the following components:

```
[ Microphone PCM Input ] ---> [ .NET ClientWebSocket ] ---> [ Azure gpt-4o-realtime-preview (WSS) ]
                                                                             |
[ Speaker Audio Output ] <--- [ Audio Playback Buffer ] <--- [ Speech PCM Chunks ]
```

1. **Azure Model Deployment**:
   - Deploy `gpt-4o-realtime-preview` in Azure OpenAI Portal.

2. **C# / F# WebSocket Client (`RealtimeAudioClient`)**:
   - Use `System.Net.WebSockets.ClientWebSocket`.
   - Send `session.update` events configuring voice (`alloy`, `echo`, `shimmer`) and function tool definitions.

3. **Audio I/O Drivers**:
   - Capture microphone PCM audio frames using NAudio / PortAudio.
   - Stream PCM chunks back to system speaker for instant voice playback.
