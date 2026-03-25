# Auto-Playtest Example

Demonstrates how AI agents can automatically test gameplay in Play Mode
using Unity AI Bridge — without any human interaction.

## What This Example Does

1. Enter Play Mode via `editor-application-set-state`
2. Wait for game to initialize via `wait_playmode.py`
3. Query initial game state via `runtime-query`
4. Take a screenshot via `screenshot-capture`
5. Invoke a game action via `runtime-invoke`
6. Query updated state to verify the result
7. Take another screenshot for visual comparison
8. Exit Play Mode

## The Key Insight

AI agents can't play games at 60fps — but they don't need to.
The pattern is: **Act → Wait → Observe → Judge → Repeat**.

- **Act**: Call `runtime-invoke` to trigger game actions
- **Wait**: `sleep(N)` or use `wait_playmode.py` for the game to process
- **Observe**: Call `runtime-query` to read game state + `screenshot-capture` for visuals
- **Judge**: AI analyzes the state/screenshot to decide PASS/FAIL
- **Repeat**: Fix code if FAIL, recompile (`wait_compile.py`), test again

## Example: Verify a Health System

Assume your project has a `PlayerHealth` MonoBehaviour with a public `currentHealth` field
and a static `DamagePlayer(int amount)` method.

### Step-by-step

```bash
# 1. Enter Play Mode
python3 bridge.py editor-application-set-state '{"isPlaying": true}'
python3 scripts/wait_playmode.py

# 2. Check initial health
python3 bridge.py runtime-query '{"typeName": "PlayerHealth", "fields": "currentHealth,maxHealth"}'
# → {"typeName":"PlayerHealth","instanceCount":1,"instances":[{"gameObject":"Player","fields":{"currentHealth":100,"maxHealth":100}}]}

# 3. Screenshot before damage
python3 bridge.py screenshot-capture '{"tag": "before-damage"}'

# 4. Apply damage
python3 bridge.py runtime-invoke '{"typeName": "PlayerHealth", "methodName": "DamagePlayer", "arguments": "[30]"}'

# 5. Verify health decreased
python3 bridge.py runtime-query '{"typeName": "PlayerHealth", "fields": "currentHealth"}'
# → {"fields":{"currentHealth":70}}  ✓ PASS: 100 - 30 = 70

# 6. Screenshot after damage
python3 bridge.py screenshot-capture '{"tag": "after-damage"}'
# → AI verifies health bar visual decreased

# 7. Exit Play Mode
python3 bridge.py editor-application-set-state '{"isPlaying": false}'
```

## Writing Your Own Playtest

1. **Identify testable state**: What public fields on your MonoBehaviours represent game state?
2. **Identify trigger methods**: What static methods can change game state?
3. **Define expected outcomes**: After action X, field Y should equal Z
4. **Chain the tools**: act → wait → query → assert → screenshot

## Requirements

- Unity AI Bridge package installed in your project
- MonoBehaviours with public fields (for `runtime-query`)
- Public static methods for game actions (for `runtime-invoke`)
- No special test framework needed — works with any Unity project
