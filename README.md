# Semantic Kernel Chat Application

A .NET 9 console application that implements a chat interface using Microsoft's Semantic Kernel and Azure OpenAI services.

## Features
- Console-based chat interface with Azure OpenAI
- Real-time response streaming
- Conversation history management

## Setup
1. Configure Azure OpenAI credentials in `appsettings.Development.json`:



<br><br><br>

#
#
#

<br><br><br>

# Enhanced Functionalities

- A custom GitPlugin to fetch Git commits
- A prompt plugin to generate release notes
- A system prompt to guide the LLM
- Patch SemVer version on new release
- Store latest version in persistent file `version.txt`
- Codebase indexing & keyword search (`docsearch`)

---

## ✅ Prerequisites

1. Fill `appsettings.Development.json` like:
   ```json
   {
     "ModelName": "gpt-4.1", // or your model
     "Endpoint": "https://your-openai-endpoint.openai.azure.com/",
     "ApiKey": "your-api-key"
   }
   ```

2. Run the project:
   ```sh
   dotnet run
   ```

---

## ✅ Functionality Test Steps

### 🔹 1. Set Git Repository Path
Prompt:
```
setrepo C:\Path\To\Your\GitRepo
```
Expected:
```
Repository path set to: ...
```

---

### 🔹 2. Get Latest Git Commits
Prompt:
```
getlatestcommits 5
```
Expected:
```
- Commit message 1 (by Author on Date)
- Commit message 2 ...
```

---

### 🔹 3. Generate Release Notes
Prompt:
```
generate release notes based on last 5 commits
```
Expected:
```
## Changelog

### Commits
- Fix bug X by 'name' on 'date'
- Add feature Y ...
```

---

### 🔹 4. Debug Kernel Functions
Set breakpoints inside:
- `GitPlugin.GetLatestCommits(...)`
- `GitPlugin.CreatePlugin(...)`
- Lambda function in `SetRepositoryPath(...)`

Inspect values and outputs while testing.

---

### 🔹 5. System Prompt Validation
Prompt:
```
What can you do?
```
Expected:
```
You can read Git commits and generate release notes...
```

---

### 🔹 6. Version Management
Prompt:
```
getlatestversion
```
Expected:
```
1.0.0
```

Prompt:
```
bumppatchversion
```
Expected:
```
Version bumped to 1.0.1
```

---

### 🔹 7. Index The Code

After you run `setrepo`, the app will automatically scan **all** `.cs` files under your repo, break them into 10-line chunks, and store them under a `"codebase"` collection.

You can re-index anytime by reissuing:
```text
Me > setrepo C:\Path\To\Your\GitRepo
```
You’ll see:
```text
Indexing .cs files into memory...
Indexed 42 files into memory.
```

---

### 🔹 8. Search The Code

Use the `docsearch` command followed by any keyword or snippet:
```text
Me > docsearch InitializeComponent
```
Expected output (top matching chunks):
```text
Agent >
- public void InitializeComponent() { … }
- this.InitializeComponent();
…
```

---

## 📦 Files of Interest

| File | Purpose |
|------|---------|
| `Program.cs` | Main chatbot loop and plugin registration |
| `GitPlugin.cs` | Custom plugin to access Git commit history and versioning |
| `ReleaseNotes/skprompt.txt` | Prompt template for changelog |
| `ReleaseNotes/config.json` | Prompt configuration |
| `appsettings.Development.json` | Azure OpenAI config |
| `version.txt` | Stores the current release version |
| `CodeIndexer.cs` | Splits `.cs` into chunks & saves into in-memory store |
| `TextMemoryPlugin.cs` | In-process store with `SaveAsync`/`RetrieveAsync`/`SearchAsync` |

---