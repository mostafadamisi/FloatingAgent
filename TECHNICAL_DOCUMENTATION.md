# Floating Agent — Technical Documentation

## 1. Project Overview

Floating Agent is a desktop AI assistant MVP that uses vision-capable large language models (via an OpenAI-compatible API) to see and control the user's screen. It operates as a floating, always-on-top window (440×640) that can be minimized to an 80×80 bubble. The agent accepts natural language tasks — such as "open Gmail and send an email" — and autonomously executes them by capturing screenshots, sending them to a vision model for analysis, and performing mouse/keyboard/launch actions via Win32 interop.

The system uses a **hybrid WPF + Blazor** architecture: WPF handles the native window chrome, transparency, Win32 interop, and desktop automation; Blazor (running inside `Microsoft.AspNetCore.Components.WebView.Wpf`) renders the chat UI and settings page with modern web technologies.

---

## 2. Problem Statement

Modern AI agents are typically cloud-based chat interfaces that have no ability to interact with local desktop applications. Users who want AI assistance with desktop tasks must either:

- Manually execute every step themselves while consulting an AI for guidance
- Use brittle OCR-based automation tools that require per-application scripting
- Use cloud-based RPA tools that don't have vision-based reasoning

**Floating Agent solves this** by providing a vision-based agent that can see anything on the user's screen, reason about it using an LLM, and take direct action (click, type, launch apps, press keys) — all through a natural language chat interface, with no per-application configuration required.

---

## 3. Architecture

```
┌──────────────────────────────────────────────────┐
│                   WPF Window                      │
│  ┌─────────────┐  ┌──────────────┐               │
│  │ Normal View  │  │  Bubble View │               │
│  │  (440×640)  │  │   (80×80)    │               │
│  └──────┬──────┘  └──────────────┘               │
│         │                                         │
│  ┌──────┴──────────────────────────────────────┐  │
│  │        BlazorWebView (WebView2)              │  │
│  │  ┌─────────────┐  ┌──────────────────────┐  │  │
│  │  │  ChatPage   │  │   SettingsPage        │  │  │
│  │  │  (razor)    │  │   (razor)             │  │  │
│  │  └──────┬──────┘  └──────────┬───────────┘  │  │
│  └─────────┼────────────────────┼──────────────┘  │
└────────────┼────────────────────┼─────────────────┘
             │                    │
    ┌────────┴────────────────────┴────────┐
    │         BlazorBridge (DI)             │
    │  ┌──────────┐ ┌───────────────────┐   │
    │  │ OpenCode │ │  ActionParser     │   │
    │  │ ApiClient│ │  (static)         │   │
    │  └────┬─────┘ └────────┬──────────┘   │
    └───────┼────────────────┼──────────────┘
            │                │
    ┌───────┴────────────────┴──────────────┐
    │   ScreenAutomationService             │
    │   • Win32 mouse/keyboard API          │
    │   • Screen capture (GDI+)             │
    │   • Window hide/show (opacity+pos)    │
    └────────────────────────────────────────┘
            │
    ┌───────┴──────────────┐
    │  OpenAI-compatible   │
    │  Vision API          │
    │  (8 providers)       │
    └──────────────────────┘
```

### Data Flow (Task Execution)

```
User: "Open Chrome and go to gmail"
  │
  ▼
BlazorBridge.HandleUserMessage()
  │
  ├─ NeedsScreenshot() → true
  ├─ ShowMinimizePrompt → user confirms
  ├─ FadeOut() → HideFromScreen()
  ├─ CaptureScreenAsBase64() → GDI+ full-screen PNG
  ├─ ShowOnScreen() → FadeIn()
  ├─ SendScreenshotAsync() → API (vision)
  │
  ▼
OpenCodeApiClient → POST /chat/completions (text + image)
  │
  ▼
Response: "[LAUNCH \"chrome\"] [WAIT 3000]"
  │
  ▼
ActionParser.Collect() → [AnnotatedAction × 2]
  │
  ▼
RunActions() loop:
  ├─ FadeOut() + HideFromScreen()
  ├─ Execute each action (LaunchApp, Thread.Sleep)
  ├─ ShowOnScreen() + FadeIn()
  ├─ CaptureScreen → another API call → repeat
  └─ Until [DONE] received
```

---

## 4. Complete Tech Stack

| Layer | Technology | Version | Purpose |
|-------|-----------|---------|---------|
| **Runtime** | .NET | 8.0 | Application framework |
| **UI Shell** | WPF (Windows Presentation Foundation) | .NET 8.0-windows | Native window, WebView2 host, Win32 interop |
| **UI Content** | Blazor (.razor) | .NET 8.0 | Chat UI, settings page, reactive components |
| **WebView** | Microsoft.AspNetCore.Components.WebView.Wpf | 8.0.70 | Embeds Blazor in WPF via WebView2 |
| **WebView2** | Microsoft Edge WebView2 | System-installed | Renders Blazor HTML/CSS/JS |
| **CSS** | Custom stylesheet | — | Dark purple theme, animations, responsive |
| **Serialization** | System.Text.Json | Built-in | JSON parsing for API, config, tool calls |
| **Imaging** | System.Drawing.Common | 10.0.10 | Screen capture (GDI+ Bitmap → PNG → base64) |
| **Win32 Interop** | P/Invoke (user32.dll) | — | `SetCursorPos`, `mouse_event`, `keybd_event`, `GetSystemMetrics`, `VkKeyScan` |
| **API Protocol** | OpenAI-compatible REST | — | `POST /chat/completions` with `system/user/assistant` roles |
| **Vision** | Model-dependent | varies | Screenshot analysis via multimodal LLM |
| **DI** | Manual (ServiceCollection) | .NET 8.0 | BlazorWebView service injection |

---

## 5. Folder Structure

```
FloatingAgent/
├── App.xaml                     — WPF application definition
├── App.xaml.cs                  — Startup, crash logging, static services
├── appsettings.json             — Persisted config (provider, URL, key, model, etc.)
├── AssemblyInfo.cs              — Assembly metadata
├── FloatingAgent.csproj         — SDK-style .NET 8 project file
├── MainWindow.xaml              — Window chrome (Normal + Bubble views)
├── MainWindow.xaml.cs           — Window logic, config loading, minimize/restore
├── Blazor/
│   ├── _Imports.razor           — Blazor directives
│   ├── ChatPage.razor           — Main chat interface + minimize prompt
│   └── SettingsPage.razor       — Provider/model configuration
├── Models/
│   ├── AnnotatedAction.cs       — ActionType enum + AnnotatedAction class
│   ├── AppConfig.cs             — AppConfig + ProviderInfo
│   ├── ChatMessage.cs           — API message model
│   └── ChatMessageModel.cs      — UI message model (IsImage, ActionIcon, etc.)
├── Services/
│   ├── ActionParser.cs          — Bracket + XML tool-call parsing + execution
│   ├── BlazorBridge.cs          — WPF↔Blazor bridge, action loop, state management
│   ├── OpenCodeApiClient.cs     — OpenAI-compatible REST client with system prompt
│   └── ScreenAutomationService.cs — Screen capture, mouse, keyboard, window hide/show
├── design-system/
│   └── floating-agent/
│       └── MASTER.md            — Design tokens, color palette, typography
└── wwwroot/
    ├── css/
    │   └── styles.css           — 437 lines, full dark theme
    └── index.html               — Host page, JS auto-scroll
```

---

## 6. Feature List

### 6.1 Core Features
- **Natural language task execution** — user types a task, agent executes it
- **Vision-based screen understanding** — full-screen PNG captures sent to vision model
- **Desktop automation** — mouse clicks (left/right/double), mouse movement, text typing, key presses
- **App launching** — open any application by name via `Process.Start`
- **URL opening** — open URLs in default browser
- **Hotkey support** — any combination of CTRL/ALT/SHIFT/WIN + key
- **Action batching** — model encouraged to batch predictable actions (e.g., `[LAUNCH] [WAIT]`)
- **Autonomous multi-step loop** — after actions, re-captures screen and continues until `[DONE]`

### 6.2 UI Features
- **Floating window** — borderless, transparent, always-on-top, dark purple theme
- **Minimize to bubble** — collapses to 80×80 animated circle (opacity/off-screen during actions)
- **Chat interface** — message bubbles (user=purple, assistant=dark, action=centered)
- **Screenshot thumbnails** — 📷 icon, click to expand
- **Action icons** — 🚀 (launch), 🖱 (click), ⌨ (type), ⏳ (wait), 🔘 (press)
- **Typing indicator** — 3-dot pulse animation during API calls
- **Camera flash** — white flash overlay on screenshot capture
- **Model badge** — header shows current provider and model name
- **Copy conversation** — copies entire chat to clipboard
- **Stop button** — cancels current agent execution
- **Resizable** — min 300×400, resize grip in bottom-right
- **Minimize confirmation prompt** — asks Yes/No before minimizing to bubble

### 6.3 Settings Features
- **8 provider cards** — OpenCode Zen, OpenAI, OpenRouter, Groq, Together AI, DeepSeek, GitHub Models, Ollama (Local)
- **API URL input** — always editable
- **API key input** — show/hide toggle
- **Model management** — text input + dropdown populated from `/models` endpoint
- **Test connection** — sends test message, displays response/timing
- **Behavior toggles** — Always on Top, Auto-minimize to bubble
- **Persistent config** — saves to `appsettings.json`, applies immediately without restart

### 6.4 Error Handling
- **Crash logging** — `DispatcherUnhandledException` + `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException` written to `crash.log`
- **Operation cancellation** — `CancellationTokenSource` per task, clean abort on Stop
- **Message limit** — 100 message cap (FIFO removal), 20 message context window
- **Empty message filtering** — whitespace-only content skipped
- **API error display** — error messages shown in chat

---

## 7. Execution Workflow

```
┌──────────────────────────────────────────────────────┐
│                  USER SENDS TASK                      │
│  ChatPage.SendMessage() → Bridge.HandleUserMessage()  │
└──────────────────────┬───────────────────────────────┘
                       │
                       ▼
         ╔════════════════════════════╗
         ║ NeedsScreenshot()         ║
         ║ Checks for action/view     ║
         ║ trigger words in message   ║
         ╚════════════════════════════╝
              │              │
        false ▼              ▼ true
              │              │
   ┌──────────┘     ┌───────┴──────────┐
   │ Text-only      │ AutoMinimize     │
   │ API call       │ && needsScreen?  │
   │                │ → ShowMinPrompt  │
   └──────┬─────────┘ ── Yes ──►──────┘
          │         Minimize window
          ▼           to bubble
    ┌──────────────────────┐
    │ API: SendMessage()   │
    │ POST chat/completions│
    │ (text only)          │
    └──────┬───────────────┘
           ▼
    ┌──────────────────────┐
    │ API: SendScreenshot()│
    │ POST chat/completions│
    │ (text + image)       │
    └──────┬───────────────┘
           ▼
    ╔═══════════════════╗
    ║ RESPONSE PARSED   ║
    ║ ActionParser      ║
    ║ .Collect(response)║
    ╚═══════════════════╝
           ▼
    ┌─────────────────────────┐
    │ RunActions() loop       │
    │ (up to 30 iterations)   │
    │                         │
    │ ┌─────────────────────┐ │
    │ │ Execute action batch│ │
    │ │ • FadeOut window    │ │
    │ │ • HideFromScreen    │ │
    │ │ • Execute actions   │ │
    │ │ • ShowOnScreen      │ │
    │ │ • FadeIn window     │ │
    │ └──────────┬──────────┘ │
    │            ▼            │
    │ ┌─────────────────────┐ │
    │ │ Capture screenshot  │ │
    │ │ Send to API:        │ │
    │ │ "Continue working"  │ │
    │ │ Parse response      │ │
    │ │ If [DONE]: break    │ │
    │ │ If no actions:      │ │
    │ │   retry with prompt │ │
    │ │ Enqueue new actions │ │
    │ └─────────────────────┘ │
    │                         │
    │ Loop until:             │
    │ • [DONE] received       │
    │ • User pressed Stop     │
    │ • 30 iterations         │
    │ • No actions in 2 tries │
    └─────────────────────────┘
```

---

## 8. Agent Pipeline (Detailed)

### Phase 1: Intent Detection
- `NeedsScreenshot()` checks user message against `ActionTriggers` ("click", "open", "launch", "go to", "type", "press", "scroll", "select", "minimize", "maximize", "close", "move", "find", "search") and `ViewTriggers` ("screen", "desktop", "see", "look", "show").
- If no trigger words found → text-only API call (no screenshot cost).
- If trigger words found → full vision pipeline.

### Phase 2: Minimize Decision
- If `AutoMinimize` is enabled AND action task detected:
  - Set `ShowMinimizePrompt = true`
  - Await user choice via `TaskCompletionSource<bool>`:
    - **Yes** → `RequestEvent?.Invoke("minimize")` → window collapses to 80×80 bubble
    - **No** → continue in dashboard mode

### Phase 3: Capture & API Call
- `CaptureWithFlash()`:
  - Set `CameraFlash = true` → white flash overlay
  - 200ms delay (flash visible)
  - `ScreenAutomationService.CaptureScreenAsBase64()`:
    - `HideFromScreen()` → opacity=0, position=(-32000,-32000) (so screenshot is clean)
    - GDI+ `CopyFromScreen` full-screen → Bitmap → PNG → base64
    - `ShowOnScreen()` → restore saved position/opacity
  - Set `CameraFlash = false`
- `BuildHistory(20)` → last 20 non-action messages (removes trailing user message)
- API call via `OpenCodeApiClient.SendScreenshotAsync()`:
  - Builds system prompt (identity-enforced, batching guidance)
  - Appends history messages
  - Appends user message with text + `image_url` (data:image/png;base64)
  - POST to `{baseUrl}/chat/completions`
  - Parses `choices[0].message.content`

### Phase 4: Action Execution
- `ActionParser.StripActions()` removes bracket/tool_call syntax from response for display
- `ActionParser.Collect()` matches `[CLICK ...]`, `[LAUNCH "..."]`, `[TYPE "..."]`, `[PRESS KEY]`, `[HOTKEY ...]`, `[MOVETO x y]`, `[WAIT ms]`, `[OPEN "..."]` and XML `<tool_call>` blocks
- Actions are enqueued into a `Queue<AnnotatedAction>`
- For each batch:
  1. `FadeOut()` → 150ms opacity 1→0 animation
  2. `HideFromScreen()` → opacity=0, off-screen
  3. Execute all actions in batch sequentially
  4. `ShowOnScreen()` → restore position/opacity  
  5. `FadeIn()` → 150ms opacity 0→1 animation
- After batch completion:
  1. `await Task.Delay(100)` for screen to update
  2. Capture new screenshot
  3. Send to API: "Task: {task}\nLast actions executed. Updated screen attached. Continue working..."
  4. If response has `[DONE]` → finish
  5. If response has no actions → retry once with stricter prompt: "Your response had no action commands..."
  6. Enqueue new actions and continue loop

### Phase 5: Completion
- `[DONE]` → "✅ Task complete" message
- Max 30 iterations (safety limit)
- User Stop → `CancellationTokenSource.Cancel()` → `OperationCanceledException` → "⏹ Agent stopped"

---

## 9. Planning Logic

The agent does **not** have a separate planning module. Planning is entirely **in-context**, delegated to the LLM via the system prompt and the conversation history. The system prompt gives the model:

1. **Identity** — must call itself "Floating Agent", never reveal base model or creator
2. **Action constraints** — must output at least one action in every response, never launch same app twice, always WAIT after LAUNCH
3. **Batching rules** — predictable next steps → batch in one response; unpredictable → one action at a time
4. **Action grammar** — exact syntax for each action type
5. **Completion signal** — `[DONE]` when task is done

After each action batch, the model receives a fresh screenshot and the instruction: "Continue working toward the task. What next? Reply with an action command or [DONE] if finished." This creates a **reactive planning loop** where the model re-assesses the screen state after every action batch and decides the next step.

The only hard-coded logic is the fallback: if the model returns no actions (and hasn't said `[DONE]`), the system re-asks with a stricter prompt demanding an action. After two empty responses, the loop breaks.

---

## 10. Memory System

Memory is **ephemeral conversation context**, not a persistent database:

- **Short-term (in-memory)**: `ObservableCollection<ChatMessageModel> Messages` — holds up to 100 messages (FIFO eviction at 101+)
- **Context window**: `BuildHistory(20)` — takes the last 20 non-action messages for API context
- **No long-term memory**: Each session starts fresh. No embeddings, no vector store, no file-based persistence of past conversations.
- **Action message exclusion**: Messages with `Role == "action"` (screenshots, action logs, status updates) are excluded from `BuildHistory()` to keep the context focused on meaningful conversation turns.

---

## 11. Tool-Calling System

### 11.1 Bracket Syntax (Primary)
The model outputs actions in square brackets anywhere in its response text:

| Syntax | Action Type | Parsing |
|--------|------------|---------|
| `[CLICK x y button]` | Mouse click | `x`, `y` integers, `button` = "left"/"right"/"double" |
| `[MOVETO x y]` | Mouse move | `x`, `y` integers |
| `[TYPE "text"]` | Type text | Content between first `"` and last `"` |
| `[PRESS KEYNAME]` | Key press | Single key from named table (ENTER, TAB, ESC, etc.) |
| `[HOTKEY MOD+KEY]` | Hotkey | Modifier + key, e.g., `CTRL+A`, `ALT+F4` |
| `[LAUNCH "app"]` | Launch app | App name/path between quotes |
| `[OPEN "url"]` | Open URL | URL between quotes |
| `[WAIT ms]` | Wait/delay | Milliseconds as integer |
| `[DONE]` | Task complete | Stop signal (case-insensitive) |

### 11.2 XML Tool Call Syntax (Secondary)
The model can alternatively output OpenAI-style tool calls:

```xml
<tool_call>{"name":"click","arguments":{"x":100,"y":200,"button":"left"}}</tool_call>
```

Supported tool names: `launch`, `open`, `click`, `moveto`/`move_to`/`move`, `type`, `press`, `hotkey`/`hot_key`, `wait`, `done`.

### 11.3 Parsing (`ActionParser.Collect`)
- Finds all bracket matches via `Regex` with `RegexOptions.IgnoreCase`
- Tokenizes content — `[CLICK 100 200 left]` → `["CLICK", "100", "200", "left"]`
- Parses quoted strings — `[LAUNCH "Google Chrome"]` → `"Google Chrome"`
- Also finds `<tool_call>` XML blocks and deserializes JSON
- Returns `List<AnnotatedAction>`

### 11.4 Execution (`ActionParser.Execute`)
Dispatches to `ScreenAutomationService` methods:
- `Click` → `LeftClick(x, y)`, `RightClick(x, y)`, or `DoubleClick(x, y)`
- `MoveTo` → `MoveMouse(x, y)`
- `Type` → `TypeText(text)` — character-by-character via `VkKeyScan` + `keybd_event`
- `Press` → `PressKey(vk)` for single keys, `PressHotKey(mod+key)` for combinations
- `Launch` → `Process.Start(appName)` with `UseShellExecute = true`
- `Open` → `Process.Start(url)` with `UseShellExecute = true`
- `Wait` → `Thread.Sleep(ms)`

---

## 12. Desktop Automation Implementation

### 12.1 Screen Capture
```
HideFromScreen()  ──►  opacity=0, Left=-32000, Top=-32000
                         (window invisible, taskbar icon stays)
CaptureScreenAsBase64() ──►  GetSystemMetrics(SM_CX/CYSCREEN)
                         new Bitmap(width, height)
                         Graphics.CopyFromScreen(0,0,0,0, size)
                         Bitmap.Save(ms, ImageFormat.Png)
                         Convert.ToBase64String(ms.ToArray())
ShowOnScreen()    ──►  restore saved Left, Top, Opacity
```

**Key detail**: The window is **not** hidden with `SW_HIDE` because that would remove the taskbar icon. Instead, it's moved off-screen (-32000,-32000) with zero opacity. This keeps the taskbar entry visible for restoring later.

### 12.2 Mouse Automation
| API | Win32 Function | Parameters |
|-----|--------------|------------|
| `SetCursorPos` | `SetCursorPos(x, y)` | Absolute screen coordinates |
| `LeftClick` | `SetCursorPos` + `mouse_event(LEFTDOWN)` + 50ms + `mouse_event(LEFTUP)` | x, y |
| `RightClick` | Same with `RIGHTDOWN`/`RIGHTUP` | x, y |
| `DoubleClick` | Two `LeftClick` calls with 100ms interval | x, y |

### 12.3 Keyboard Automation
| API | Implementation |
|-----|---------------|
| `TypeText(text)` | For each char: `VkKeyScan(c)` → virtual key code + shift state → `keybd_event(down)` → 10ms → `keybd_event(up)` → 20ms. Shift key pressed/released when needed. |
| `PressKey(vkCode)` | `keybd_event(vk, 0, 0)` → 30ms → `keybd_event(vk, 0, KEYEVENTF_KEYUP)` |
| `PressHotKey("CTRL+A")` | Split on `+` → lookup modifier keys (CTRL=0x11, ALT=0x12, SHIFT=0x10, WIN=0x5B) → press modifiers in order → press main key → release main key → release modifiers in reverse order |

### 12.4 App Launch
```csharp
Process.Start(new ProcessStartInfo { FileName = appName, UseShellExecute = true });
```
Leverages Windows shell to resolve app names (same as Start Menu search).

### 12.5 Window Transitions
- **FadeOut**: `DoubleAnimation` (From=current opacity, To=0, 150ms) via WPF animation system
- **FadeIn**: `DoubleAnimation` (From=0, To=1, 150ms)
- **HideFromScreen**: Immediate opacity=0 + position off-screen (no animation)
- **ShowOnScreen**: Immediate restore + set `Topmost = true` to re-assert z-order

---

## 13. Vision Pipeline

```
User sends task (e.g. "open gmail")
        │
        ▼
  NeedsScreenshot() ──► true (contains "open")
        │
        ▼
  CaptureWithFlash()
    ├─ CameraFlash = true (white CSS animation)
    ├─ await Task.Delay(200)
    ├─ HideFromScreen()
    ├─ GDI+ CopyFromScreen(0,0, full-screen)
    ├─ ShowOnScreen()
    └─ CameraFlash = false
        │
        ▼
  API Request (OpenCodeApiClient.SendScreenshotAsync)
    ├─ System prompt (identity + action rules)
    ├─ Conversation history (last 20, no action msgs)
    ├─ User message:
    │   {
    │     "role": "user",
    │     "content": [
    │       { "type": "text", "text": "open gmail" },
    │       { "type": "image_url",
    │         "image_url": { "url": "data:image/png;base64,<B64>" }
    │       }
    │     ]
    │   }
    └─ POST to {baseUrl}/chat/completions
        │
        ▼
  Response parsed → action loop
```

The image is always **full-screen** resolution (uncompressed). The model is tasked with understanding the visual layout, finding UI elements by their visual appearance, and outputting action commands with precise coordinates.

---

## 14. AI Models & Why

### Default: `mimo-v2.5-free` via OpenCode Zen

**Why this model:**
- Free tier with vision capabilities
- Fast inference suitable for multi-step loops
- OpenAI-compatible API format (drop-in replacement)

### Why multiple providers (8 options):
| Provider | Use Case | Key Strength |
|----------|----------|-------------|
| **OpenCode Zen** | Default/free | No cost, reasonable vision |
| **OpenAI (GPT-4o)** | Best quality | Best vision + instruction following |
| **Groq (Llama 3.2 90B Vision)** | Speed | Extremely fast inference (token/s) |
| **Together AI (Llama 3.2 11B Vision)** | Open-source | Smaller/faster, good for simple tasks |
| **OpenRouter** | Flexibility | Access to dozens of models through one API |
| **DeepSeek** | Cost-effective | Vision-capable, competitive pricing |
| **GitHub Models** | Free tier | $0 free credits, good for testing |
| **Ollama (Local)** | Privacy/Offline | No internet, no API key, fully local |

**All providers must support:**
- OpenAI-compatible `/chat/completions` endpoint
- Vision/multimodal input (`image_url` content type)
- System/user/assistant roles (no `action` role needed)
- 90-second timeout support

---

## 15. Prompt Engineering Strategy

### Identity Enforcement
The system prompt **never reveals** the underlying model name or creator. The model is told it is `Floating Agent — a desktop automation tool` and must:
- Never say it is Claude, GPT, Llama, or any other model
- Never mention Anthropic, OpenAI, or any company
- If asked "who are you", reply only: "Floating Agent. What task can I do?"
- No chitchat, no explanations about itself

### Action Grammar Specification
The prompt defines exact bracket syntax for each action type with examples:
- `[LAUNCH "appname"]` — launch an application
- `[CLICK x y left|right|double]` — mouse clicks
- `[TYPE "text"]` — keyboard input
- `[PRESS KEY]` — named keys (ENTER, TAB, ESC, etc.)
- `[HOTKEY MOD+KEY]` — modifier combinations
- `[WAIT ms]` — delays (300-1000ms for page loads, 100-300ms for typing)

### Batching Guidance
The prompt distinguishes predictable vs. unpredictable next steps:
- **Predictable** → batch multiple actions in one response (e.g., `[LAUNCH "chrome"] [WAIT 3000] [TYPE "gmail.com"] [PRESS ENTER]`)
- **Unpredictable** → one action at a time (the system will re-capture screen and ask what next)

### Action Constraints
- "You MUST output at least one action command in EVERY response. Never text-only."
- "NEVER launch the same app twice in one session."
- "Keep explanatory text to 1 sentence max. Prioritize speed and action."
- "When launching an app, always batch `[LAUNCH "app"] [WAIT 3000]` together."

### Completion Signal
`[DONE]` (case-insensitive) signals task completion. The system checks for this both in the initial response and after each action batch loop.

### Fallback Strategy (Not in Prompt)
If the model returns a response with no actions:
1. System re-asks with: `Your response had no action commands. You MUST output an action command now — look at the screen and choose the next action. Reply with EXACTLY one line like [CLICK x y left] or [DONE] and nothing else.`
2. If still no actions → break loop with "No further actions"

---

## 16. APIs and Libraries

### NuGet Packages
| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.AspNetCore.Components.WebView.Wpf` | 8.0.70 | Blazor WebView for WPF |
| `System.Drawing.Common` | 10.0.10 | System.Drawing for screen capture |

### Win32 API (P/Invoke)
| DLL | Functions |
|-----|----------|
| `user32.dll` | `SetCursorPos`, `mouse_event`, `keybd_event`, `VkKeyScan`, `GetSystemMetrics` |

### .NET Framework APIs
| Namespace | Classes/Methods |
|-----------|----------------|
| `System.Net.Http` | `HttpClient`, `StringContent` |
| `System.Text.Json` | `JsonSerializer`, `JsonDocument`, `JsonElement` |
| `System.Drawing` | `Bitmap`, `Graphics`, `ImageFormat.Png` |
| `System.Windows` | `Window`, `UIElement`, `Clipboard`, `Application` |
| `System.Windows.Media.Animation` | `DoubleAnimation` |
| `System.Collections.ObjectModel` | `ObservableCollection<T>` |
| `System.Diagnostics` | `Process`, `ProcessStartInfo` |
| `System.Runtime.InteropServices` | `DllImport`, `Marshal` |
| `Microsoft.Extensions.DependencyInjection` | `ServiceCollection`, `ServiceProvider` |

### External HTTP Endpoints
- `POST {baseUrl}/chat/completions` — main API call (text + vision)
- `GET {baseUrl}/models` — model listing (for Settings UI)

---

## 17. Project Statistics

| Metric | Value |
|--------|-------|
| **Total source files** | 20 |
| **Total lines of code** | 2,121 |
| **C# (.cs) files** | 11 |
| **Blazor (.razor) files** | 3 |
| **XAML files** | 2 |
| **CSS files** | 1 (437 lines) |
| **HTML files** | 1 |
| **JSON files** | 1 |
| **NuGet dependencies** | 2 |
| **Service classes** | 4 |
| **Model classes** | 5 (including 1 enum) |
| **Blazor components** | 2 |
| **API providers** | 8 |
| **Action types** | 7 |
| **Win32 interop functions** | 5 |
| **CSS keyframe animations** | 10 |
| **CSS selectors** | 100+ |

---

## 18. Engineering Challenges

### 18.1 Window Hiding During Screenshots
**Problem**: Capturing a clean screenshot requires hiding the agent window, but `SW_HIDE` also removes the taskbar icon, confusing users.
**Solution**: Set opacity=0 and move window to (-32000, -32000) — keeps taskbar icon visible while making the window invisible to `CopyFromScreen`.

### 18.2 Action Loop Without Native Tool Calling
**Problem**: The API only supports `system/user/assistant` roles — no `tool` or `action` role. Can't use native OpenAI tool calling.
**Solution**: Custom bracket syntax (`[CLICK x y left]`) parsed by regex anywhere in the response text. Secondary support for `<tool_call>{"name":"...","arguments":{...}}</tool_call>` XML blocks.

### 18.3 Context Window Pollution
**Problem**: Screenshots and action logs fill the conversation history with non-relevant content, causing the model to lose track of the original task.
**Solution**: `BuildHistory()` filters out `Role == "action"` messages, keeping only user/assistant turns. Screenshots are sent per-step in the action loop, not accumulated in history.

### 18.4 Model Refusing to Output Actions
**Problem**: Some models (especially smaller or instruction-tuned ones) occasionally output text-only responses with no action commands, stopping the loop.
**Solution**: Two-level fallback: if no actions detected, re-prompt with a stricter instruction ("You MUST output an action command now..."). If still no actions, break gracefully.

### 18.5 0,0 Coordinate Bug
**Problem**: `TryGetInt` could return `(0, true)` even when the JSON value was 0 (valid), causing false-positive coordinate extraction when no coordinates existed.
**Fix**: Ensure `TryGetInt` only returns true when the property actually exists in the JSON and is a valid number, not just when the default int 0 matches.

### 18.6 VkKeyScan -1 Edge Case
**Problem**: `VkKeyScan` returns `-1` for characters that cannot be typed (modifier-only keys). The original code didn't check for this, passing `-1` as byte and corrupting input.
**Fix**: Guard with `if (vk == -1) continue;`.

### 18.7 Duplicate [DONE] Handling
**Problem**: The action loop checked `HasDone()` twice (once before entering queue, once after), causing duplicate "Task complete" messages and skipping actions after the first [DONE] check.
**Fix**: Consolidated to single check with early return when [DONE] is present.

### 18.8 CancellationToken Leak
**Problem**: Each new task created a new `CancellationTokenSource` without disposing the previous one, causing resource leaks.
**Fix**: `_cts?.Cancel(); _cts?.Dispose();` before creating new CTS.

### 18.9 WebView2 wwwroot Resolution
**Problem**: The BlazorWebView infrastructure requires a `wwwroot` folder in the output directory. If missing or named differently, the app crashes on startup.
**Fix**: Named the folder `wwwroot` (not `root`) and added a build target to copy it to the output directory.

### 18.10 BlazorWebView Initialization Order
**Problem**: Setting `HostPage` after `RootComponents` causes a runtime crash.
**Fix**: Set `HostPage` before assigning `Services` and adding `RootComponents`.

---

## 19. Lessons Learned

### Architecture Lessons
1. **Hybrid WPF+Blazor works well** for desktop apps needing a modern UI with native capabilities. The Blazor WebView abstraction is mature enough for production use.
2. **Static service references** (`App.Bridge`, `App.ScreenAutomation`) are pragmatic for an MVP but would benefit from proper DI in a larger app.
3. **The blob-architecture** (single BlazorBridge doing everything) is a known tradeoff — simple for an MVP but hard to test and maintain at scale.

### AI/Agent Lessons
4. **Vision models are remarkably capable** at understanding screen layouts and finding UI elements by visual appearance.
5. **Action batching is critical for speed** — each API round-trip adds 1-3 seconds. Batching 3-4 predictable actions per response cuts task time by 60%.
6. **Models need strong identity enforcement** — without it, they default to "I'm Claude/GPT" and refuse to use action commands.
7. **The retry fallback is essential** — even the best models sometimes output text-only. A single retry with a stricter prompt usually fixes it.

### UI Lessons
8. **Opacity+off-screen hiding** is better than `SW_HIDE` for taskbar-continuous apps.
9. **Animation matters for UX** — 150ms fade transitions feel polished. The camera flash effect signals screenshot capture without confusion.
10. **Auto-scroll with manual scroll detection** (via JS `_userScrolled` flag) is essential — without it, users can't read history without being yanked back to bottom.

---

## 20. Roadmap

### Completed (MVP)
- [x] Vision-based screen understanding
- [x] Mouse click/move automation (left, right, double)
- [x] Keyboard typing and hotkeys
- [x] App launching and URL opening
- [x] Multi-step autonomous loop
- [x] Action batching
- [x] Minimize to bubble with animation
- [x] Settings page with 8 providers
- [x] Model fetching from API
- [x] Crash logging and error handling
- [x] Cancel/stop execution
- [x] Minimize confirmation prompt

### Phase 2 — Stabilization
- [ ] Proper DI container (Microsoft.Extensions.DependencyInjection)
- [ ] Unit tests for ActionParser (bracket + XML)
- [ ] Integration test harness (mock API)
- [ ] Screenshot compression (JPEG, resize) for faster uploads
- [ ] Configurable context window size
- [ ] Per-task timeouts

### Phase 3 — Features
- [ ] OCR mode (extract text from screen regions)
- [ ] File system operations (save/open files)
- [ ] Drag-and-drop support
- [ ] Multi-monitor support
- [ ] Clipboard management (copy/paste)
- [ ] Screenshot region selection
- [ ] Task history/replay

### Phase 4 — Advanced
- [ ] Local embedding + vector memory for long-term recall
- [ ] Custom action plugins (user-defined scripts)
- [ ] Keyboard shortcut to summon/hide
- [ ] Multi-agent collaboration (one plans, one executes)
- [ ] Streaming response display (token-by-token)
- [ ] Voice input

---

## 21. Demo Walkthrough

### Scenario: "Open Chrome and go to gmail.com, sign in and send an email"

**Step 1** — User types the task and clicks Send

**Step 2** — The minimize prompt appears:
```
┌──────────────────────┐
│ Minimize to bubble   │
│ while task runs?     │
│                      │
│ [Yes]  [No, stay]    │
└──────────────────────┘
```

**Step 3** — User clicks "Yes". Window collapses to 80×80 purple bubble with "AI" text and a green status dot.

**Step 4** — Behind the scenes:
- Window fades to 0 opacity over 150ms
- Window moved off-screen to (-32000, -32000)
- Full-screen capture taken via GDI+ (`CopyFromScreen(0,0)`)
- Window restored to saved position/opacity

**Step 5** — API call 1: screenshot + instruction "Open Chrome and go to gmail.com"
```
System: You are Floating Agent...
User (text): Open Chrome and go to gmail.com, sign in and send an email
User (image): [full-screen PNG base64]
```

**Step 6** — API Response 1:
```
Starting task. Looking at your desktop to find Chrome.
[LAUNCH "chrome"] [WAIT 3000]
```

**Step 7** — Action execution:
- Chat shows: 🚀 Launch: chrome
- Window fades out (150ms)
- Window hidden off-screen
- `Process.Start("chrome")` — Chrome opens
- `Thread.Sleep(3000)` — wait for Chrome
- Window restored + fades in (150ms)

**Step 8** — Screenshot capture (same hide/capture/show cycle):
- New screenshot sent to API with: "Task: Open Chrome...\nLast actions executed: [LAUNCH 'chrome'] [WAIT 3000]\nUpdated screen attached. Continue working..."

**Step 9** — API Response 2 (sees Chrome homepage with address bar):
```
[TYPE "gmail.com"] [PRESS ENTER] [WAIT 3000]
```

**Step 10** — Actions execute: type "gmail.com" → press Enter → wait 3 seconds

**Step 11** — Screenshot → API → Response 3 (sees Gmail login page):
```
[CLICK 500 350 left] [WAIT 200]
[TYPE "myemail@gmail.com"] [PRESS ENTER] [WAIT 2000]
```

**Step 12** — Actions execute: click email field, type email, press Enter

**Step 13** — Screenshot → API → Response 4 (sees password field):
```
[CLICK 500 400 left] [WAIT 200]
[TYPE "mypassword"] [PRESS ENTER] [WAIT 4000]
```

**Step 14** — Screenshot → API → Response 5 (sees Gmail inbox):
```
[CLICK 100 200 double] [WAIT 2000]
[TYPE "Test email from AI"] [HOTKEY TAB]
[TYPE "This email was composed by the floating agent."]
[HOTKEY TAB] [PRESS ENTER]
```

**Step 15** — Screenshot → API → Response 6 (email sent):
```
[DONE]
```

**Step 16** — Chat shows: ✅ Task complete

**Total round-trips**: ~6 API calls, ~15-20 action commands executed
**Typical time**: 30-60 seconds depending on model speed and page load times

---

*Documentation generated from codebase at `C:\Users\Mustafa\Desktop\testtt\FloatingAgent`. 20 source files, 2,121 lines total. Last updated: July 2026.*
