# -*- coding: utf-8 -*-
"""Would more armour, or cheaper repairs, make the hull branch worth choosing?

Both are the same bet: that what the player LOSES to the sea is big enough to be worth paying to
avoid. So measure the loss first. Whatever hull can ever be worth is capped by it -- a discount on a
bill that barely exists buys nothing, however generous the discount.
"""
import sys, io, json, subprocess, shutil, os

sys.path.insert(0, 'tools')
SRC = "Assets/Resources/GameData/game-data.json"
BAK = SRC + ".lever"


def session(eff):
    """Total income, total repair spend, and total wreck losses over one 90-minute run."""
    import importlib
    import upgrade_value
    importlib.reload(upgrade_value)
    U = upgrade_value
    income = repairs = wrecked = 0.0
    for tier, zi, bid in [(0, 3, 'A'), (1, 6, 'B'), (2, 9, 'C')]:
        # roughly a third of the session is spent in each tier
        cycles = (90 * 60 / (U.DAY + [105, 150, 180][tier])) / 3
        for lvl in range(tier * 4, tier * 4 + 4):
            lv = {k: lvl for k in ['hook', 'hold', 'engine', 'hull']}
            gross = U.cycle_income(lv, tier, zi, bid) + U.repair_bill(zi, lvl, tier)
            rep = U.repair_bill(zi, lvl, tier)
            wr = U.wreck_odds(zi, lvl, tier)
            income += gross * cycles / 4 * eff
            repairs += rep * cycles / 4
            wrecked += gross * wr * cycles / 4 * eff
    return income, repairs, wrecked


def variant(name, **econ):
    d = json.load(io.open(BAK, encoding='utf-8'))
    d['economy'].update(econ)
    io.open(SRC, 'w', encoding='utf-8', newline='\n').write(json.dumps(d, ensure_ascii=False, indent=2))
    inc, rep, wr = session(0.75)
    total_up = sum(u['cost'] for u in d['upgrades']) + sum(d['tuning']['NewShipCosts'])
    loss = rep + wr
    print(f"{name:32} thu {inc:>10,.0f} | sua {rep:>7,.0f} | chim {wr:>9,.0f} "
          f"| mat {loss/max(inc,1)*100:>5.1f}% thu nhap = {loss/total_up*100:>5.1f}% tong nang cap")
    return loss


shutil.copy(SRC, BAK)
base = json.load(io.open(BAK, encoding='utf-8'))['economy']
print("Tat ca do o hieu suat 75%, ca phien 90 phut. 'mat' = tien sua + gia tri khoang ca chim.\n")
b = variant("HIEN TAI", **{})
print()
variant("giap x3 (0.06 -> 0.18)", hullArmorPerLevel=.18, tierArmorBonus=.60)
variant("giap x6 (0.06 -> 0.36)", hullArmorPerLevel=.36, tierArmorBonus=1.2)
variant("sua tau GIAM NUA", repairCostPerMissingHp=1)
variant("sua tau MIEN PHI", repairCostPerMissingHp=0)
print()
print(f"=> TRAN CUNG: bo het ca tien sua LAN rui ro chim cung chi dang {b:,.0f} xu ca phien.")
shutil.copy(BAK, SRC); os.remove(BAK)
