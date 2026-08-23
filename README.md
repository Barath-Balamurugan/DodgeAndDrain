# DodgeAndDrain

## Improvement checklist

This is the working list of improvements to complete and validate during closed
testing before applying for Google Play production access.

### Fix before the next tester build

- [ ] **Repair the game-over results UI.** The normal score, game-over score,
  and best score currently point to the same text element. Give the final-score
  and best-score fields their own UI text, and add a clear **GAME OVER** heading.

- [ ] **Refresh the ammo UI when draining gives ammo.** `PlayerAmmo.Add()`
  changes the amount but does not update the on-screen counter. A player should
  immediately see that draining has restored ammunition.

- [ ] **Use one ammo source of truth.** Remove the unused duplicate ammo values
  from `GameManager` and keep `PlayerAmmo` as the single system that stores and
  displays ammunition.

- [ ] **Make restart a complete fresh run.** Reset the player's position,
  ammunition, drain state, score, energy, and active enemies/drainables when
  Restart is pressed.

### Improve player understanding and feel

- [ ] **State the gameplay loop on the start screen:** “Drain energy to refill
  ammo. Shoot enemies. Survive for score.” Explain what Score, Energy, and Ammo
  each mean.

- [ ] **Polish the tutorial copy.** Replace “your speed wont be the same will
  draining” with “While draining, you move more slowly.” Rename the tutorial's
  final **DRAIN** button to **LET'S GO** or **START RUN**.

- [ ] **Improve combat readability.** Keep the neon look, but make the player,
  enemies, and drainable objects easier to distinguish against the dark arena.
  Use stronger silhouettes/colours and clear wind-up indicators for enemy dashes
  and shots.
  - [x] Give each enemy type a distinct silhouette: circle chaser, triangle
    dasher, and square shooter. Keep the existing enemy colours and player
    appearance unchanged.
  - [x] Telegraph attacks: a red expanding ring and direction line for dashers,
    a short aim line for shooters, and a bright trail for enemy projectiles.
  - [x] Make drain heat readable: label the gauge, transition it from green to
    amber to red, and display a three-second **OVERHEATED** countdown.
  - [x] Add one-hit death feedback: a short flash, screen shake, and optional
    device vibration.

- [ ] **Add a gentler opening.** Give the player a 3–5 second safe period, or
  start with only chaser enemies, before introducing the full enemy mix.

- [ ] **Make death useful.** The game-over panel should show final score, best
  score, survival time, and one contextual tip, such as “Drain cyan orbs to
  regain ammo.”

### Prepare for longer sessions

- [ ] **Avoid repeated whole-scene searches.** Shooting and spawning currently
  scan all active objects/enemies. Track active enemies directly and use object
  pooling for enemies and projectiles as the game scales.

### Closed-test feedback to collect

Ask each tester to report on:

1. Whether the tutorial and controls were easy to understand.
2. Whether draining, ammo recovery, score, and overheating made sense.
3. Whether the first minute felt fair and enjoyable.
4. Any crashes, freezes, control problems, or unreadable UI.
5. Whether the game-over and restart screens clearly explained the result and
   encouraged another run.
