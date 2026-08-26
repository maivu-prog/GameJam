# -*- coding: utf-8 -*-
"""Is the 90 minutes a steady climb, or does it stall?

A wall is meant to make the player WANT the next ship, not to leave them sailing in circles with
nothing to buy. This walks the same cycles sim_session.py does and reports the dry spells: how many
cycles in a row bought nothing, and how long the player stared at a price they could not reach.
"""
import sys, io, subprocess

DRY_WARN = 3          # cycles in a row with no purchase before it reads as "stuck"


def parse(eff):
    out = subprocess.run([sys.executable, "tools/sim_session.py", str(eff), "r", "d"],
                         capture_output=True, text=True).stdout
    rows = []
    for line in out.splitlines():
        p = line.split()
        if len(p) >= 5 and p[0].replace('.', '').isdigit():
            minute, coins = float(p[0]), float(p[4])
            bought = p[5:] if len(p) > 5 else []
            rows.append((minute, coins, bought))
    return rows, out


def report(eff):
    rows, out = parse(eff)
    print(f"===== hieu suat {int(eff*100)}% =====")
    dry = worst = 0
    worst_at = None
    spells = []
    for minute, coins, bought in rows:
        if bought:
            if dry >= DRY_WARN: spells.append((worst_at, dry))
            dry = 0
        else:
            if dry == 0: worst_at = minute
            dry += 1
            if dry > worst: worst = dry
    if dry >= DRY_WARN: spells.append((worst_at, dry))

    cyc = len(rows)
    buys = sum(1 for _, _, b in rows if b)
    print(f"  {cyc} chu ky, {buys} chu ky co mua ({buys/max(cyc,1)*100:.0f}%)")
    print(f"  chuoi khong mua dai nhat: {worst} chu ky")
    if spells:
        print("  cac doan treo:")
        for at, n in spells:
            print(f"    phut {at:.0f} -> im lang {n} chu ky (~{n*5:.0f} phut)")
    else:
        print("  khong co doan treo nao qua", DRY_WARN, "chu ky")
    verdict = ("MUOT" if worst <= 2 else "CHAP NHAN DUOC" if worst <= 4 else "KET - can ha gia")
    print(f"  => {verdict}\n")
    return worst


w = [report(e) for e in (0.75, 0.60, 0.50)]
print("xau nhat qua ca ba muc:", max(w), "chu ky lien tiep khong mua duoc gi")
