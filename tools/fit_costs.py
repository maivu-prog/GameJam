# -*- coding: utf-8 -*-
"""Solve the cost curve so the wall from NOTE-game-balance-math.md 1.1 actually forms.

The note's r = 1.07..1.15 is derived under ADDITIVE income. This game's income is multiplicative --
deeper bands and further zones multiply the value of every single catch -- so income itself grows
about x18 across the run. Copying 1.07 here would guarantee the opposite of what the note wants:
income outruns cost, no wall, and the player drowns in coins with nothing to buy.

The LAW is what transfers: cost growth must beat income growth. So r is solved against measured
income, not copied. Everything below is fitted, not guessed.
"""
import json, io, subprocess, sys, shutil, os

SRC = "Assets/Resources/GameData/game-data.json"
BAK = "Assets/Resources/GameData/game-data.json.fitbak"
WEIGHT = {"hook": 1.25, "engine": 1.05, "hold": .95, "hull": .75}   # by how much each branch pays back
LEVELS = 12


def round5(x):
    return max(5, int(round(x / 5.0)) * 5)


def curve(base, r, w):
    return [round5(base * w * (r ** i)) for i in range(LEVELS)]


def write(d, base, r, shipmul, esc=1.0):
    cur = {k: curve(base, r, w) for k, w in WEIGHT.items()}
    for row in d["upgrades"]:
        row["cost"] = cur[row["branch"]][row["level"] - 1]
    # A new ship must cost like a new ship: priced off the tier it opens, not a flat number.
    # Priced off the tier each ship opens. `esc` makes the later ships relatively dearer, which is the
    # only lever that stops all three bunching once income plateaus.
    tier_cost = [sum(cur[k][i * 4] for k in cur) for i in range(3)]
    d["tuning"]["NewShipCosts"] = [round5(c * shipmul * (esc ** i)) for i, c in enumerate(tier_cost)]
    io.open(SRC, "w", encoding="utf-8", newline="\n").write(json.dumps(d, ensure_ascii=False, indent=2))
    return cur, d["tuning"]["NewShipCosts"]


def dry_spell(eff):
    """Longest run of cycles that bought nothing. A wall the player can climb is fine; a wall they
    stare at for six cycles is not, and that is what this keeps out of the fit."""
    out = subprocess.run([sys.executable, "tools/sim_session.py", str(eff), "r", "d"],
                         capture_output=True, text=True).stdout
    worst = run = 0
    for line in out.splitlines():
        p = line.split()
        if len(p) >= 5 and p[0].replace('.', '').isdigit():
            if len(p) > 5: run = 0
            else:
                run += 1
                worst = max(worst, run)
    return worst


def arc(eff):
    """Minute each ship lands, plus final levels, from the real simulator."""
    out = subprocess.run([sys.executable, "tools/sim_session.py", str(eff), "r", "d"],
                         capture_output=True, text=True).stdout
    ships, done, coins = [], None, None
    for line in out.splitlines():
        if "SHIP-" in line:
            for tok in line.split():
                if tok.startswith("SHIP-"):
                    ships.append(int(float(line.split()[0])))
        if line.startswith("ket thuc:"):
            done = "12/12/12/12" in line
        p = line.split()
        if len(p) > 5 and p[0].replace('.', '').isdigit():
            coins = float(p[4])
    return ships, done, coins


TARGET = [25, 52, 80]   # a ship at roughly a third, two thirds, and near the end of 90 minutes


def spread_score(ships):
    """How far the three New Ship moments sit from an even spread across the session.
    Hitting one target exactly matters far less than not bunching two ships into the first half."""
    if len(ships) != 3: return 1e9
    return sum((a - b) ** 2 for a, b in zip(ships, TARGET)) ** .5


def main():
    shutil.copy(SRC, BAK)
    d = json.load(io.open(SRC, encoding="utf-8"))
    print(f"{'r':>5} {'base':>4} {'esc':>4} {'tong':>7} {'gia tau':>20}  {'moc tau @75%':>18} {'du':>10}")
    best = None
    for r in [1.23, 1.26, 1.29]:
      for base in [45, 55, 65]:
        for esc in [1.0, 1.5, 2.0, 2.6]:
            cur, ship = write(json.load(io.open(BAK, encoding="utf-8")), base, r, 1.6, esc)
            total = sum(sum(v) for v in cur.values()) + sum(ship)
            ships, done, coins = arc(0.75)
            ok = bool(done)
            mark = ""
            if ok and len(ships) == 3:
                s6, d6, _ = arc(0.60)
                slow_ships = len(s6)
                stuck = max(dry_spell(0.60), dry_spell(0.50))
                # Never trade a smooth climb for a prettier ship spacing: stalling is what the player
                # feels, ship timing is only what the spreadsheet sees.
                score = (spread_score(ships) + 8 * (coins / max(total, 1))
                         + 25 * max(0, stuck - 2) + 15 * max(0, 2 - slow_ships))
                mark = f"  lech={spread_score(ships):.0f} treo={stuck} tau60={slow_ships}"
                if best is None or score < best[0]:
                    best = (score, r, base, total, ships, coins, esc); mark += " <<"
            print(f"{r:>5} {base:>4} {esc:>4} {total:>7} {str(ship):>20}  {str(ships):>18} {coins:>10.0f}{mark}")
    print()
    if best:
        _, r, base, total, ships, coins, esc = best
        print(f"CHON: r={r} base={base} esc={esc} tong={total} moc tau={ships} du={coins:.0f}")
        write(json.load(io.open(BAK, encoding="utf-8")), base, r, 1.6, esc)
    else:
        print("khong bo nao dat -> khoi phuc ban goc")
        shutil.copy(BAK, SRC)
    os.remove(BAK)


main()
