# -*- coding: utf-8 -*-
"""If defence cannot be made worth buying, raise what it defends against.

Hull's ceiling is whatever the sea takes from you, and right now the sea takes 1.3% of your income.
No discount on that bill matters. This sweeps obstacle damage upward and reports, at each level of
danger: what the player loses, whether hull starts winning the value chart, and -- the part that
decides it -- whether the run still flows.
"""
import sys, io, json, subprocess, shutil, os, importlib

sys.path.insert(0, 'tools')
SRC = "Assets/Resources/GameData/game-data.json"
BAK = SRC + ".danger"


def measure(mult):
    d = json.load(io.open(BAK, encoding='utf-8'))
    for o in d['obstacles']:
        o['damage'] = int(round(o['damage'] * mult))
    io.open(SRC, 'w', encoding='utf-8', newline='\n').write(json.dumps(d, ensure_ascii=False, indent=2))

    import upgrade_value; importlib.reload(upgrade_value); U = upgrade_value
    inc = rep = wr = 0.0
    lead = {k: 0 for k in ['hook', 'hold', 'engine', 'hull']}
    for level in range(12):
        tier, zi, bid = U.context(level)
        base = {k: level for k in lead}
        vfm = {}
        for k in lead:
            up = dict(base); up[k] += 1
            gain = U.cycle_income(up, tier, zi, bid) - U.cycle_income(base, tier, zi, bid)
            vfm[k] = gain / U.UP[k][level]
        lead[max(vfm, key=vfm.get)] += 1
        cycles = (90 * 60 / (U.DAY + [105, 150, 180][tier])) / 3 / 4
        gross = U.cycle_income(base, tier, zi, bid) + U.repair_bill(zi, level, tier)
        inc += gross * cycles * .75
        rep += U.repair_bill(zi, level, tier) * cycles
        wr += gross * U.wreck_odds(zi, level, tier) * cycles * .75

    out = subprocess.run([sys.executable, "tools/sim_session.py", "0.60", "r", "d"],
                         capture_output=True, text=True).stdout
    ships = sum(1 for l in out.splitlines() if "SHIP-" in l)
    done = "12/12/12/12" in out
    dry = worst = 0
    for line in out.splitlines():
        p = line.split()
        if len(p) >= 5 and p[0].replace('.', '').isdigit():
            if len(p) > 5: dry = 0
            else: dry += 1; worst = max(worst, dry)
    loss = (rep + wr) / max(inc, 1) * 100
    return loss, lead, ships, done, worst


shutil.copy(SRC, BAK)
print(f"{'nguy hiem':>10} {'mat % thu nhap':>15} {'dan dau (hook/hold/eng/hull)':>32} {'tau@60%':>8} {'xong':>6} {'treo':>5}")
for m in [1, 2, 3, 4, 6, 8]:
    loss, lead, ships, done, worst = measure(m)
    tag = ""
    if lead['hull'] >= 2 and done and worst <= 3: tag = "  <- hull co gia tri MA VAN muot"
    elif lead['hull'] >= 2: tag = "  <- hull co gia tri nhung flow hong"
    print(f"{'x'+str(m):>10} {loss:>14.1f}% "
          f"{str([lead['hook'],lead['hold'],lead['engine'],lead['hull']]):>32} "
          f"{ships:>8} {str(done):>6} {worst:>5}{tag}")
shutil.copy(BAK, SRC); os.remove(BAK)
print("\n(da khoi phuc game-data.json ve nguyen trang - day chi la do thu)")
