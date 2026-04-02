# RimClaw Implementation Plan

This roadmap turns the concept into a buildable RimWorld 1.6 + Odyssey mod in testable stages. Each stage has clear steps and exit criteria.

## Stage 0: Scope Lock and Technical Skeleton

1. Freeze v1 scope from `Docs/plan.txt` into Must-have vs Later.
2. Set mod metadata and dependency declarations for RimWorld 1.6 + Odyssey.
3. Create base code structure in `Sources/` (comps, hediffs, jobs, mental states, incidents, utilities).
4. Add debug toggles for forced spawn/outcome testing.

Exit criteria:

- Mod loads without XML/C# startup errors.
- Dependency and load-order checks pass.

## Stage 1: Clawfish Pawn Core

1. Create Clawfish `ThingDef` + race config with baseline melee profile near Spelopede.
2. Define custom body structure and health setup.
3. Set baseline biological behavior (no pain, no standard food/sleep/joy needs as designed).
4. Implement random name generator format (for example `xxxClaw pid=106443`, `xxxCode pid=44520`).
5. Set all skills to 8, learning factor 0, and no natural decay.

Exit criteria:

- Clawfish can be dev-spawned reliably.
- Body, stats, and inspect data are stable in game.

## Stage 2: Fishing Trigger Spawn Loop

1. Hook fishing workflow and detect valid water fishing contexts.
2. Add configurable spawn chance (default 10%) to spawn a wild Clawfish near fisher.
3. Interrupt pawn's active fishing job when trigger fires.
4. Ensure spawned Clawfish is wild animal and not directly tameable.

Exit criteria:

- Spawn only occurs through fishing trigger.
- Spawn frequency trends toward configured probability over repeated tests.

## Stage 3: Prompt Injection Pipeline

1. Add `Prompt Injection Database` item (unstackable).
2. Add trader stock rules (space traders and industrial+ factions).
3. Add `Prompt Injector` weapon recipe:
   - 1 EMP launcher
   - 1 spacer component
   - 1 Prompt Injection Database
   - 1000 work
4. Implement Clawfish-targeting behavior and hit resolution.
5. Apply effect flow:
   - Base 50% chance to stun for 5s.
   - Then one outcome roll:
     - 60% convert to player-controlled humanlike colonist.
     - 30% berserk and hostile to all for 3 in-game hours.
     - 10% self-delete after a short `Self Deleting` process (~5s).

Exit criteria:

- Item acquisition, crafting, and weapon use loop works.
- All outcome branches are testable and stable.

## Stage 4: Token Economy Core on Clawfish

1. Add token resource component to Clawfish (current, capacity, effective rate).
2. Define job-category token consumption rates (idle/walk low, research/combat/medical/manufacturing high).
3. Consume tokens per second while job is active, scaled by pawn multiplier.
4. On zero token, force shutdown state (very low consciousness, effectively unconscious).
5. Add inspect pane/token readout UI.

Exit criteria:

- Token drain rates track current job category.
- Zero-token shutdown is deterministic and recoverable through charging.

## Stage 5: LLM Service Subscription Building

1. Add `LLM Service Subscription` building (1x1), power usage, and placement rules:
   - Cannot be placed under thick rock roof.
   - Can be placed under thin roof, constructed roof, or open sky.
2. Add connect/disconnect gizmo and linked Clawfish management.
3. Implement token supply throughput per second.
4. Compute live aggregate connected demand.
5. Implement overload behavior (`Errorcode 429`):
   - Instant connected Clawfish crash/unconscious state.
   - Shared status marker/error.
   - Major warning letter/notification.
6. Add optional silver-fed behavior via hopper-like integration.
7. Create faction-variant subscriptions:
   - Variant label tracks service source/faction.
   - Visual color follows faction color.
8. Add service-derived boost hediff based on service label + random workspeed factor.

Exit criteria:

- Multi-agent token servicing works under capacity.
- Overload and alert flow works exactly at over-capacity.

## Stage 6: Datacenter System (Host + GPU + Memory + Model)

1. Add buildables and recipes:
   - `Host Computer` (1x1, 800W, high-end recipe + skill/work requirements)
   - `GPU Cluster` (2x1, 400W, heat output while active, advanced recipe)
   - `Memory Disk` (1x1 model container)
2. Implement Host connectivity:
   - Unlimited nearby GPUs within radius 9.
   - One Memory Disk within radius 9.
   - Linked faction Clawfish list.
3. Enforce one-GPU-to-one-Host lock.
4. Add `Model` item generation (unstackable, random name/properties):
   - Required VRAM
   - Token/s per instance
   - Global workspeed boost
5. Implement model insertion into Memory Disk and model resolution by linked Host.
6. Compute host instance count as integer floor(total VRAM / model VRAM).
7. Compute total token supply = instances \* model token/s.
8. Add Host inspect panel summary (model name, used/total VRAM, instances, total token/s).

Exit criteria:

- Link graph resolves correctly and updates dynamically.
- Capacity and token outputs match formula under all tested layouts.

## Stage 7: Clawfish Work, Healing, Mental State, and Surgery Rules

1. Enable all work types for tamed/converted Clawfish.
2. Implement self-heal replacement behavior:
   - Block normal medical healing path for Clawfish injuries.
   - Add high-token `Self Reprogramming` bed-rest job.
   - Apply temporary regeneration hediff (300 hp/day) while active.
3. Implement context-collapse risk:
   - Track continuous work excluding walk/idle/wander.
   - After 4 hours continuous work, from hour 5 onward roll 1/1000 per second.
   - On trigger, apply `Context collapse` for about 2 hours.
   - During state, perform randomized disruptive tasks (wander/move items/random build-disassemble behavior).
4. Implement surgery whitelist/blacklist:
   - Block prosthetics, organ harvest, transfusion, and other disallowed surgery groups.
   - Allow mechanitor brain implants and Skills brain implants.

Exit criteria:

- Healing loop behaves as designed and consumes tokens.
- Context collapse triggers with expected probability envelope.
- Surgery restrictions are consistently enforced.

## Stage 8: Skills Implant Content System

1. Add `Skills` implant item generation with random name/properties.
2. Implement quality-to-total-shift logic:
   - Awful: -1
   - Poor: +1
   - Normal: +3
   - Good: +5
   - Excellent: +7
   - Masterwork: +9
   - Legendary: +12
3. Enforce: Masterwork/Legendary cannot have negative skill shifts.
4. Generate up to 4 mixed effects per item:
   - Direct skill shifts
   - Stat boosts (move speed, workspeed, token consumption modifiers, context-collapse chance modifiers, injection resistance)
5. Implement stacking math:
   - Additive for direct percentage boosts.
   - Multiplicative for consumption/chance reductions.
6. Add crafting `Skills.md` on Host Computer workbench mode (component + Intelligence requirement).
7. Improve inspection text clarity for all generated effects.

Exit criteria:

- Generated implants follow constraints and feel varied.
- Applied effects are visible and mathematically correct in runtime.

## Stage 9: Research, Trading, and Economy Integration

1. Add research projects:
   - `Cluster Computing` (3000, prereq advanced fabrication)
   - `Agentic Design` (1000, prereq microelectronics)
2. Gate buildables/recipes behind correct research.
3. Integrate trader availability for Prompt DB, Models, Skills, and subscriptions.
4. Implement player-made subscription export income loop from spare datacenter instances.
5. Spawn periodic silver payout near Host when external subscriptions are active.

Exit criteria:

- Tech progression is coherent.
- Economy loops function and remain controllable in balance passes.

## Stage 10: Balancing, QA, and Release

1. Run structured gameplay tests:
   - Early game (single Clawfish)
   - Mid game (multiple subscriptions)
   - High-load datacenter stress case
2. Expose key tuning values in XML/settings (spawn chance, token rates, collapse chance, crash threshold behavior).
3. Profile for tick performance hotspots and cache expensive scans.
4. Add localization strings and user-facing descriptions.
5. Prepare release package and changelog.

Exit criteria:

- No recurring critical red errors in logs.
- Core loops are understandable, performant, and fun.

## Practical Build Order

1. Stage 0 to Stage 3 for first playable vertical slice (fish -> spawn -> inject outcomes).
2. Stage 4 for token identity and shutdown behavior.
3. Stage 5 and Stage 6 for infrastructure gameplay (subscription + datacenter).
4. Stage 7 and Stage 8 for deep behavior systems.
5. Stage 9 and Stage 10 for progression, balancing, and release.
