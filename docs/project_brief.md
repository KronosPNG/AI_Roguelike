# Project Brief — Adaptive Boss AI Thesis

**Document version:** 0.2
**Author:** Luigi Turco
**Date created:** 2025-08-20

---

## 1. Title

**2D Roguelike‑Inspired Game with Adaptive Boss AI**

*(WIP Title)*

---

## 2. Synopsis (one paragraph)

This project implements and evaluates a deep reinforcement learning (DRL) driven boss for a compact 2D game. The game has a fixed layout of four item rooms followed by a boss room; item rewards in the item rooms are randomized per run. The research focus is the boss: a single encounter whose behavior (combat tactics and item selection) adapts to different player builds and playstyles using PyTorch‑based DRL. The goal is to demonstrate that an adaptive ML policy can (1) respond to repeated player strategies, (2) counter different archetypes, and (3) produce an alternative paradigm to traditional NPCs AIs.

---

## 3. Research question & hypothesis

**Research question:** Can a deep reinforcement learning policy produce an adaptive boss that dynamically changes behaviour and item selection to counter different player builds and improve performance over repeated encounters?
**Hypothesis:** A DRL boss conditioned on compact, well‑engineered observations of the player build and recent actions will (a) create a new paradigm in video game AI and (b) exhibit measurable adaptation over repeated encounters with the same player build.

---

## 4. Genre / Scope clarification

* **Genre label:** 2D roguelike‑inspired (fixed map layout).
* **Important note:** The game is *not* a full procedural roguelike — the room layout is fixed (4 item rooms + 1 boss room). What is randomized are the item rewards and the boss's learned behavior. This fixed layout reduces scope while keeping stochastic variation by randomized rewards and adaptive boss AI.

---

## 5. Objectives (SMART)

1. **Implement a compact playable game loop** (2D arena with 4 item rooms + boss room) and a deterministic baseline boss.
2. **Design and implement a Gym‑like boss environment wrapper** with an observation vector and a multi‑discrete action space.
3. **Train DRL boss policies (PPO in PyTorch)** against scripted player archetypes and mixed opponents, export best policies to ONNX.
4. **Integrate trained policies into the game engine** (Godot) for in‑engine playtesting and hot‑swap between ML and rule‑based boss.
5. **Run reproducible experiments** (multiple seeds) and report metrics: win rate, encounter duration, action entropy, and adaptation curves.
6. **Produce thesis artifacts**: reproducible code/data/models, figures, playtest videos, and written analysis defending or refuting the hypothesis.

---

## 6. Minimum Viable Product (frozen for thesis)

* **Game map:** fixed sequence of 4 item rooms followed by one boss room.
* **Items available (per room):** a curated set derived from these classes: Dagger (melee/light), Longsword (melee/medium), Longbow (ranged), Staff (magic). Elements: Physical, Fire, Ice. Armors: Light, Medium, Heavy. Consumables: Heal, Damage buff.
* **Boss:** single boss encounter with ML policy.
* **Training:** PyTorch PPO; logs and model registry.
* **Evaluation:** scripted player archetypes (melee spammable, tank, kiter/mage) + mixed testing.

> **MVP freeze rule:** items or mechanics outside the list above must be added only via an explicit scope change story.

---

## 7. Success criteria / Metrics

* **Primary metric:** Win rate of ML boss vs. each player archetype (mean ± std over N seeds and M evaluation episodes).
* **Secondary metrics:** mean encounter duration, action entropy, consumable usage rate, and time‑to‑adaptation curve (episodes-to-improvement against a repeated player build).
* **Qualitative evidence:** playtest notes, video clips showing emergent counterplay or strategy shifts.
* **Reproducibility:** a documented experiment that reproduces at least one published result (figure) using provided Dockerfile/scripts.

---

## 8. Key deliverables

1. Playable game prototype with four item rooms + boss room (engine build).
2. Gym‑style boss environment and training scripts in PyTorch.
3. Trained policy artifacts (ONNX) and model registry metadata.
4. Evaluation scripts and experiment reports (figures + tables).
5. Playtest logs and short video captures.
6. Thesis write‑up and reproducibility package (Dockerfile + instructions).

---

## 9. High‑level timeline

* **Week 0 (setup):** scaffold repo, stack docs, MVP item freeze.
* **Weeks 1–6 (foundation):** baseline playable boss + item rooms + logging.
* **Weeks 7–8 (env):** Gym wrapper, observation/action mapping, scripted archetypes.
* **Weeks 9–12 (ML prototyping):** PPO training vs single archetype → mixed opponents, small experiments.
* **Weeks 13–15 (integration):** export ONNX, integrate in engine, hot‑swap, playtesting.
* **Weeks 16–19 (experiments):** run final experiments (multi‑seed), collect metrics, produce figures.
* **Weeks 20–22 (write‑up & polish):** thesis writing, reproducibility checks, final playtests.

---

## 10. Risks & Mitigations (top 5)

1. **Risk:** Training takes too long / compute bottleneck.
   **Mitigation:** use small models, vectorized envs, frame‑skip, limit obs/action complexity.
2. **Risk:** RL policy exploits game physics / produces degenerate behaviour.
   **Mitigation:** implement hard guardrails in environment, use reward penalties for illegal/out‑of‑bounds behavior, run randomized seeds.
3. **Risk:** Scope creep (adding too many items/mechanics).
   **Mitigation:** keep MVP freeze; any additions require explicit scope change story.
4. **Risk:** Non‑reproducible experiments.
   **Mitigation:** log seeds, env configs, package versions, and provide Dockerfile.
5. **Risk:** Limited time for thesis writing and experiments.
   **Mitigation:** prioritize experiments that directly support the hypothesis; automate evaluation and logging.

---

## 11. Resources & Tech Stack (minimum)

* **Engine:** Unity (recommended) or Godot.
* **Training:** PyTorch (PPO implementation).
* **Model export/inference:** ONNX → Barracuda (Unity) or equivalent.
* **Experiment tracking:** Weights & Biases (optional) or local JSON/CSV logs.
* **Versioning:** git + Git LFS for models/data.
* **Reproducibility:** Dockerfile + requirements.txt.

---

## 12. Assumptions

* The boss fight can be abstracted into a Gym‑style environment amenable to RL training.
* The limited set of items and fixed room layout provides enough behavioral variety for meaningful ML adaptation.
* Training resources (local GPU or cloud) will be available for prototyping and final runs.

---

## 13. Definitions (short)

* **Archetype:** a scripted player style (e.g., melee spammer).
* **NPC:** Non Playable Character.
* **Episode:** a single boss encounter run from entering boss room to win/lose or timeout.
* **Policy:** trained ML agent (boss).

---

*End of project brief*
