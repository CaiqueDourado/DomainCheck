"""Cover image for the README (docs/img/cover.png and cover.pt-BR.png). Run: python docs/make_cover.py"""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch
from pathlib import Path

OUT = Path(__file__).parent / "img"
INK, MUTED, LINE, BG = "#111111", "#555555", "#d9dde3", "#fdfdfc"
C = {"ink": INK, "muted": MUTED, "blue": "#2a78d6", "orange": "#eb6834", "aqua": "#1baf7a"}
for fam in ("Inter", "DejaVu Sans"):
    try:
        matplotlib.font_manager.findfont(fam, fallback_to_default=False); plt.rcParams["font.family"] = fam; break
    except ValueError:
        pass

NAME, MARK, MARK_COLOR = 'DomainCheck', '.br', 'blue'
T = {'': {'tag': 'Is this domain\navailable?',
      'sub': "Registro.br for .br, the registry's WHOIS\nfor every other extension",
      'head': '$ domaincheck --words "casa das panelas"',
      'lines': [('casadaspanelas.com.br     registered', 'muted'),
                ('casadaspanelas.com        registered', 'muted'),
                ('casa-das-panelas.com.br   available', 'aqua', 1),
                ('casa-das-panelas.com      available', 'aqua', 1),
                ('casapanelas.com.br        available', 'aqua', 1),
                ('', 'ink'),
                ('source: registro.br · dns · whois', 'muted')],
      'mono': 8.4,
      'checks_t': 'how it decides',
      'checks': ['URL → domain, punycode',
                 'DNS pre-check, instant',
                 'Registro.br for .br',
                 'WHOIS server found via IANA',
                 'name generator from keywords'],
      'foot': '.NET 8 · zero dependencies · CLI domaincheck · illustrative output'},
 '.pt-BR': {'tag': 'Esse domínio\nestá livre?',
            'sub': 'Registro.br para .br, o WHOIS do registro\npara qualquer outra extensão',
            'head': '$ domaincheck --words "casa das panelas"',
            'lines': [('casadaspanelas.com.br     registrado', 'muted'),
                      ('casadaspanelas.com        registrado', 'muted'),
                      ('casa-das-panelas.com.br   livre', 'aqua', 1),
                      ('casa-das-panelas.com      livre', 'aqua', 1),
                      ('casapanelas.com.br        livre', 'aqua', 1),
                      ('', 'ink'),
                      ('fonte: registro.br · dns · whois', 'muted')],
            'mono': 8.4,
            'checks_t': 'como decide',
            'checks': ['URL → domínio, punycode',
                       'checagem por DNS, na hora',
                       'Registro.br para .br',
                       'servidor WHOIS achado pela IANA',
                       'gerador de nomes por palavras'],
            'foot': '.NET 8 · sem dependências · CLI domaincheck · saída ilustrativa'}}

OUT.mkdir(parents=True, exist_ok=True)
for suf, t in T.items():
    fig = plt.figure(figsize=(11, 5.6), dpi=110); fig.patch.set_facecolor(BG)
    ax = fig.add_axes([0, 0, 1, 1]); ax.set_xlim(0, 110); ax.set_ylim(0, 56); ax.axis("off")
    ax.add_patch(FancyBboxPatch((5, 41), 9, 9, boxstyle="round,pad=0,rounding_size=2", fc=C[MARK_COLOR], ec="none"))
    ax.text(9.5, 45.5, MARK, color="white", fontsize=17 if len(MARK) > 1 else 22, weight="bold", ha="center", va="center")
    ax.text(17, 45.5, NAME, fontsize=24 if len(NAME) <= 10 else 18.5, weight="bold", color=INK, va="center")
    ax.text(5, 32.5, t["tag"], fontsize=15, color=INK, va="center", linespacing=1.25)
    ax.text(5, 22.5, t["sub"], fontsize=10.5, color=MUTED, va="center", linespacing=1.35)
    # panel: what it looks like
    px, py, pw, ph = 39, 8, 39, 42
    ax.add_patch(FancyBboxPatch((px, py), pw, ph, boxstyle="round,pad=0,rounding_size=1.2", fc="white", ec=LINE, lw=1.2))
    ax.text(px + 2, py + ph - 3.5, t["head"], fontsize=10.5, weight="bold", color=INK)
    step = min(4.6, 34 / max(len(t["lines"]), 1))
    for i, (s, col, *bold) in enumerate(t["lines"]):
        ax.text(px + 2, py + ph - 9 - i * step, s, fontsize=t.get("mono", 8.3), color=C[col], family="monospace",
                weight="bold" if bold else "normal")
    # checklist
    tx = 81
    ax.text(tx, 46, t["checks_t"], fontsize=11, weight="bold", color=INK)
    for j, s in enumerate(t["checks"]):
        ax.text(tx, 40.5 - j * 5, "✓  " + s, fontsize=9, color=INK)
    ax.text(5, 3.2, t["foot"], fontsize=8.6, color=MUTED)
    ax.text(105, 3.2, "github.com/CaiqueDourado", fontsize=8.6, color=MUTED, ha="right")
    fig.savefig(OUT / f"cover{suf}.png", facecolor=BG); plt.close(fig)
print("ok")
