# AI Closed-Loop Game Development

## The Problem

AI coding assistants can write game code, but they are **blind to the result**.
They write C#, check compilation, and hope it works.
The human must open Unity, enter Play Mode, and manually verify.

## The Solution

Close the feedback loop: give AI the ability to **see** the game running.

```
AI writes code → compiles → enters Play Mode → observes state → judges → fixes → repeats
     ↑                                                                         │
     └─────────────────── fully automated loop ────────────────────────────────┘
```

## Three Layers of Automated Verification

| Layer | Method | Tools |
|-------|--------|-------|
| **State Assertion** | Read runtime values → assert expected | `runtime-query` |
| **Visual Check** | Screenshot → AI multimodal analysis | `screenshot-capture` |
| **Behavior Replay** | Invoke actions → query results → compare | `runtime-invoke` + `runtime-query` |

## Implementation with Unity AI Bridge

### 1. Act
Use `runtime-invoke` to call static methods (game commands, test helpers, state setters).

### 2. Wait
Use `wait_playmode.py` for timing — handles domain reload, heartbeat checks.
For frame-level waits, use `editor-application-set-state` (pause/step).

### 3. Observe
- **State**: `runtime-query` reads any MonoBehaviour's public fields by type name
- **Visuals**: `screenshot-capture` saves a PNG for multimodal AI analysis
- **Logs**: `console-get-logs` captures Unity console output

### 4. Judge
AI analyzes the observed state against expectations:
- Field value assertions (health > 0, position changed, score increased)
- Screenshot analysis (UI elements visible, no rendering artifacts)
- Log analysis (no errors, expected events logged)

### 5. Fix
If the judgment is FAIL, AI modifies the code, waits for recompilation
(`wait_compile.py`), and retests.

## Example Workflow

```
# 1. Write a feature
→ Create PlayerHealth.cs with static DamagePlayer(int amount) method

# 2. Compile
→ wait_compile.py --refresh

# 3. Enter Play Mode
→ editor-application-set-state (isPlaying=true)
→ wait_playmode.py

# 4. Test
→ runtime-query "PlayerHealth" fields="currentHealth"
   → { "currentHealth": 100 }

→ runtime-invoke "PlayerHealth" "DamagePlayer" "[25]"
   (Note: runtime-invoke only works with public static methods.
    For instance methods, use reflection-method-call instead.)

→ runtime-query "PlayerHealth" fields="currentHealth"
   → { "currentHealth": 75 }  ✓ PASS

→ screenshot-capture tag="after-damage"
   → AI sees health bar decreased  ✓ PASS

# 5. Exit Play Mode
→ editor-application-set-state (isPlaying=false)
```

## Key Insight

AI agents can't play games at 60fps — but they don't need to.
The pattern is: **Act → Wait → Observe → Judge → Repeat**.

This turns game testing from a manual, visual process into an
automated, data-driven one that AI can execute independently.
