# Delivery gates

**Script:** `Game/World/Gate.cs` — **Prefab:** `Prefab/Decoration/Gate`

A delivery trigger resolves the luggage outcome, records one real delivery, applies score, and recycles the object.

```text
trigger collider
  -> Luggage.TryGetFromCollider
  -> reject an already delivered item
  -> read LevelContext.CurrentConfig
  -> ScoringRules.Resolve(...)
  -> RoundScoreContext.TryRecordDelivery(last player, delta)
  -> mark delivered and recycle
```

The score record is attempted before `IsDelivered` is set, so a missing round context cannot silently consume luggage. Attribution uses the persistent `lastGrabber`, which also credits a thrown delivery. `PlayerGrab.GetPlayerIndex()` reads the real `PlayerInput.playerIndex`.

## Scoring priority

| Priority | Condition | Config value |
|---:|---|---|
| 1 | wrong numbered gate | `scoreWrongGateDelivery` |
| 2 | required wash/wrap missing | `scoreMissingProcess` |
| 3 | valid delivery | `scoreCorrectDelivery` |

`ScoringRules.Resolve` is a pure tested function. Delivered counts are tracked directly, not derived from score. See [round and scoring](../systems/round-and-scoring.md).

## Multiple gates

Each gate has a per-instance `gateNumber`. With one gate, luggage receives no destination and that gate accepts all items. With two or more, the spawner assigns a random active gate number and the timer UI displays it. Duplicate gate numbers produce a warning.

Gate number is a correct scene override: it identifies this placement. Do not apply a configured level instance's number back onto every gate prefab instance.
