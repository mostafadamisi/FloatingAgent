# Floating Agent

A desktop AI assistant that can **see and control your screen**. Powered by any OpenAI-compatible vision model. Runs as a floating, always-on-top window — accepts natural language tasks and executes them autonomously.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4)](https://www.microsoft.com/windows)

---

## Demo

```
User:  "Open Chrome and go to gmail.com, sign in and send an email"
Agent: [LAUNCH "chrome"] [WAIT 3000]
       [TYPE "gmail.com"] [PRESS ENTER] [WAIT 3000]
       [CLICK 500 350 left] [TYPE "myemail@gmail.com"] ...
       ... continues until task is done
       [DONE]
```

---

## Features

- **Vision-based screen understanding** — full-screen captures sent to a multimodal LLM
- **Desktop automation** — click, type, press keys, launch apps, open URLs, hotkeys
- **8 API providers** — OpenAI, Groq, Together AI, OpenRouter, DeepSeek, GitHub Models, Ollama (local), or OpenCode Zen (free)
- **Autonomous multi-step execution** — re-assesses screen after every action, continues until `[DONE]`
- **Minimize to bubble** — collapses to 80×80 floating circle during tasks
- **Settings UI** — switch providers/models, test connection, all persisted to `appsettings.json`

---

## Requirements

- **Windows** 10 or 11
- **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**
- **[Microsoft Edge WebView2](https://developer.microsoft.com/microsoft-edge/webview2/)** (usually pre-installed on Win 10/11)
- **An API key** from one of the supported providers (or use a local Ollama model with no key)

---

## Getting Started

### 1. Clone

```bash
git clone https://github.com/YOUR_USERNAME/FloatingAgent.git
cd FloatingAgent
```

### 2. Configure

```bash
cp appsettings.example.json appsettings.json
```

Edit `appsettings.json`:
```json
{
  "OpenCodeApiUrl": "https://opencode.ai/zen/v1",
  "ApiKey": "your-api-key-here",
  "Model": "mimo-v2.5-free"
}
```

Or configure inside the app via the Settings panel (⚙ gear icon).

### 3. Run

```bash
dotnet run
```

The floating window appears in the bottom-right corner of your screen.

---

## Usage

### Basic commands

| You type | What happens |
|----------|-------------|
| "Open Chrome" | Agent captures screen, opens Chrome |
| "Click the search bar" | Moves cursor to search bar and clicks |
| "Type hello world" | Types text at current cursor position |
| "Press CTRL+A" | Executes the hotkey |
| "What's on my screen?" | Captures and describes the screen |
| "Open google.com" | Opens URL in default browser |

### Minimize behavior

When you send a task that requires screen actions, a prompt asks:

> **Minimize to bubble while task runs?**
> [Yes] [No, stay visible]

- **Yes** — window minimizes to an 80×80 bubble; click it to restore
- **No, stay visible** — dashboard stays open throughout execution

### Switching providers

Open Settings (⚙ gear in header) → pick any provider card:
- **OpenCode Zen** — free vision model (default)
- **OpenAI** — GPT-4o, best quality
- **Groq** — very fast inference (Llama 3.2 90B)
- **Together AI** — open-source models
- **OpenRouter** — access many models
- **DeepSeek** — vision-capable
- **GitHub Models** — free tier
- **Ollama (Local)** — no API key needed, fully offline

Click **Test** to verify connectivity, then **Save**.

---

## Architecture

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8 (Windows) |
| UI Shell | WPF (borderless, transparent, always-on-top) |
| UI Content | Blazor via `BlazorWebView` |
| Automation | Win32 P/Invoke (`user32.dll`) |
| Screen Capture | GDI+ (`System.Drawing.Common`) |
| Vision API | OpenAI-compatible `/chat/completions` |

```
WPF Window → BlazorWebView → ChatPage/SettingsPage
                ↓
         BlazorBridge (orchestrator)
            ↙           ↘
  OpenCodeApiClient   ActionParser
        ↓                   ↓
  Vision API        ScreenAutomationService
                        ↓
                 Win32 mouse/keyboard
```

---

## Project Structure

```
FloatingAgent/
├── App.xaml(.cs)           — WPF app entry, crash logging
├── MainWindow.xaml(.cs)    — Window chrome, minimize/restore
├── Blazor/
│   ├── ChatPage.razor      — Chat interface
│   └── SettingsPage.razor   — Provider/model config
├── Models/
│   ├── AppConfig.cs        — Config + 8 provider presets
│   ├── ChatMessageModel.cs — UI message model
│   └── AnnotatedAction.cs  — Action types
├── Services/
│   ├── BlazorBridge.cs     — State, action loop, history
│   ├── ActionParser.cs     — Bracket/XML tool-call parser
│   ├── OpenCodeApiClient.cs — REST client + system prompt
│   └── ScreenAutomationService.cs — Win32 automation
└── wwwroot/
    ├── index.html           — Blazor host page
    └── css/styles.css       — Full dark theme
```

---

## Building from Source

```bash
dotnet build
dotnet publish -c Release -o publish
```

The standalone executable will be in `publish/FloatingAgent.exe`.

---

## How Actions Work

The agent outputs actions using bracket syntax anywhere in its response:

```
[LAUNCH "chrome"]        Launch an application
[OPEN "url"]             Open a URL
[CLICK x y left]         Mouse click (left/right/double)
[MOVETO x y]             Move mouse to coordinates
[TYPE "text"]            Type text at cursor
[PRESS KEY]              Press a named key (ENTER, TAB, ESC...)
[HOTKEY CTRL+A]          Press modifier combination
[WAIT 2000]              Wait N milliseconds
[DONE]                   Signal task complete
```

XML tool-call format is also supported:
```xml
<tool_call>{"name":"click","arguments":{"x":100,"y":200,"button":"left"}}</tool_call>
```

---

## Security

- **API keys are stored** in `appsettings.json` (text file on your machine)
- `appsettings.json` is gitignored — your key never gets committed
- The window hides (opacity=0, off-screen) during screenshots so it doesn't appear in captures
- All API communication goes directly from your machine to the provider's endpoint

---

## Tech Stack

- **.NET 8** with WPF + Blazor Hybrid
- **WebView2** for Blazor rendering
- **Win32 P/Invoke** (`SetCursorPos`, `mouse_event`, `keybd_event`, `VkKeyScan`)
- **System.Drawing.Common** for screen capture
- **OpenAI-compatible REST API** for vision LLM access

---

## License

[MIT](LICENSE)

---

## Links

- [LinkedIn Post](https://linkedin.com/in/YOUR_LINKEDIN) <!-- update this -->
- [Report a bug](https://github.com/YOUR_USERNAME/FloatingAgent/issues)

---

*Built with .NET 8, WPF, and Blazor.*
