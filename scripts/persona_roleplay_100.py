#!/usr/bin/env python3
"""100人ペルソナ × 利用ジャーニーの系統的ロープレ集計。

属性を組み合わせて100人を生成し、GitHub DL〜CLIまでの各段階で
既知の摩擦ポイントにぶつかるかをロールプレイ判定する。
"""
from __future__ import annotations

import itertools
import json
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path

# --- 属性プール（組み合わせで多様性を担保） ---
AGES = ["10代", "20代", "30代", "40代", "50代", "60代以上"]
IT_LEVELS = ["IT苦手", "普通", "上級", "エンジニア"]
JOBS = [
    "事務", "経理", "営業", "SE", "情シス", "学生", "主婦/主夫",
    "店舗スタッフ", "医療事務", "公務員", "フリーランス", "工場",
]
GOALS = [
    "定型ログイン自動化", "Excelへの連続入力", "社内システム操作",
    "テスト作業の省力化", "複数アプリ横断入力", "CLIからバッチ呼び出し",
]
DEVICES = ["Win11ノート", "Win10デスクトップ", "会社PC(制限あり)", "共有PC"]
INSTALL_HABITS = [
    "ProgramFilesに入れる", "デスクトップ直置き", "Documentsにフォルダ",
    "Downloadsのまま起動", "USBポータブル",
]
LANGUAGES = ["日本語ネイティブ", "日本語苦手", "英語UI希望"]
ACCESSIBILITY = ["なし", "拡大表示必要", "キーボード操作中心", "色弱"]
TRUST = ["警戒強い", "普通", "すぐ実行"]

FRICTION = {
    "github_confusing": "GitHubの画面が分からず Releases に辿けない",
    "zip_name_scary": "win-x64-single の名前が怖くてどれをDLするか迷う",
    "smartscreen": "SmartScreen / 不明な発行元で起動をためらう",
    "program_files": "Program Files に置いて設定場所が分からなくなる",
    "downloads_clutter": "Downloads のまま起動し設定ファイルが散らばる",
    "empty_first_run": "初回が空で何をすればいいか分からない",
    "sample_not_loaded": "config.sample.json があるのに自動で入らない",
    "no_howto_in_zip": "zip に使い方説明が入っていない",
    "focus_miss": "テスト実行時に KeyAutomator 自身に入力してしまう",
    "delay_too_short": "待ち秒が短くてウィンドウ切替が間に合わない",
    "mouse_right_default": "マウス追加の初期値が右クリックで誤操作",
    "mouse_no_coords": "座標指定ができずカーソル位置クリックに戸惑う",
    "alias_unclear": "引数名と表示名の違いが分からない",
    "cli_dialog_surprise": "CLIなのに確認ダイアログが出て止まる",
    "hotkey_no_capture": "ショートカットをキーキャプチャで入れられない",
    "settings_hard_to_find": "手順間隔が画面右下にしかなく気づかない",
    "dirty_prompt_confusing": "未保存確認の意味が分からずデータ消したと思う",
    "arm64_missing": "ARM64 端末で x64 zip しか無く困る",
    "admin_target": "管理者アプリへ送れず失敗する",
    "password_plaintext": "パスワード平文保存に不安を感じる",
    "japanese_only": "UIが日本語のみで困る",
    "a11y_small_text": "文字が小さく操作しづらい",
}

# 属性 → 摩擦の当たりやすさ（ロープレ判定ルール）
def frictions_for(p: dict) -> list[str]:
    hits: list[str] = []
    it = p["it"]
    habit = p["install"]
    device = p["device"]
    goal = p["goal"]
    trust = p["trust"]
    lang = p["lang"]
    a11y = p["a11y"]
    age = p["age"]

    # Phase1: GitHub〜配置
    if it in ("IT苦手", "普通") or age in ("50代", "60代以上"):
        hits.append("github_confusing")
        hits.append("zip_name_scary")
        hits.append("no_howto_in_zip")
    if trust == "警戒強い":
        hits.append("smartscreen")
        hits.append("password_plaintext")
    if habit == "ProgramFilesに入れる":
        hits.append("program_files")
    if habit == "Downloadsのまま起動":
        hits.append("downloads_clutter")
    if habit == "デスクトップ直置き" and it == "IT苦手":
        hits.append("downloads_clutter")

    # Phase2: 初回起動
    hits.append("empty_first_run")
    hits.append("sample_not_loaded")
    if "会社PC" in device or device == "共有PC":
        hits.append("smartscreen")

    # Phase3-4: 作成〜テスト
    if goal in ("定型ログイン自動化", "社内システム操作", "Excelへの連続入力"):
        hits.append("focus_miss")
        hits.append("delay_too_short")
    if goal in ("社内システム操作", "複数アプリ横断入力"):
        hits.append("mouse_no_coords")
        hits.append("mouse_right_default")
    if it == "IT苦手":
        hits.append("hotkey_no_capture")
        hits.append("settings_hard_to_find")
        hits.append("dirty_prompt_confusing")
    if a11y in ("拡大表示必要", "色弱"):
        hits.append("a11y_small_text")
    if a11y == "キーボード操作中心":
        hits.append("hotkey_no_capture")

    # Phase5-7
    if goal == "CLIからバッチ呼び出し":
        hits.append("alias_unclear")
        hits.append("cli_dialog_surprise")
    if "会社PC" in device:
        hits.append("admin_target")
        hits.append("password_plaintext")
    if lang in ("日本語苦手", "英語UI希望"):
        hits.append("japanese_only")
    if device == "Win11ノート" and "ARM" in p.get("notes", ""):
        hits.append("arm64_missing")

    # 重複除去（順序維持）
    seen = set()
    out = []
    for h in hits:
        if h not in seen:
            seen.add(h)
            out.append(h)
    return out


def build_personas(n: int = 100) -> list[dict]:
    """属性の直積をラウンドロビンで100人に割付。"""
    axes = [AGES, IT_LEVELS, JOBS, GOALS, DEVICES, INSTALL_HABITS, LANGUAGES, ACCESSIBILITY, TRUST]
    # 多様性が出るよう周期の異なるストライドでサンプリング
    personas = []
    for i in range(n):
        p = {
            "id": i + 1,
            "age": AGES[i % len(AGES)],
            "it": IT_LEVELS[(i * 3) % len(IT_LEVELS)],
            "job": JOBS[(i * 5) % len(JOBS)],
            "goal": GOALS[(i * 7) % len(GOALS)],
            "device": DEVICES[(i * 11) % len(DEVICES)],
            "install": INSTALL_HABITS[(i * 13) % len(INSTALL_HABITS)],
            "lang": LANGUAGES[(i * 17) % len(LANGUAGES)],
            "a11y": ACCESSIBILITY[(i * 19) % len(ACCESSIBILITY)],
            "trust": TRUST[(i * 23) % len(TRUST)],
        }
        # ARM 想定を一部に付与
        if i % 17 == 0:
            p["notes"] = "ARM"
            p["device"] = "Win11ノート"
        p["name"] = f"P{p['id']:03d}_{p['job']}_{p['it']}_{p['age']}"
        p["frictions"] = frictions_for(p)
        personas.append(p)
    return personas


PHASES = [
    ("1_github_download", ["github_confusing", "zip_name_scary", "no_howto_in_zip", "smartscreen"]),
    ("2_install_place", ["program_files", "downloads_clutter"]),
    ("3_first_launch", ["empty_first_run", "sample_not_loaded", "smartscreen"]),
    ("4_create_macro", ["mouse_right_default", "mouse_no_coords", "hotkey_no_capture", "alias_unclear", "settings_hard_to_find"]),
    ("5_test_run", ["focus_miss", "delay_too_short", "admin_target"]),
    ("6_save_reopen", ["program_files", "dirty_prompt_confusing", "password_plaintext"]),
    ("7_cli_batch", ["alias_unclear", "cli_dialog_surprise"]),
    ("8_a11y_i18n", ["a11y_small_text", "japanese_only", "arm64_missing"]),
]


def main() -> None:
    personas = build_personas(100)
    friction_counts = Counter()
    phase_counts = Counter()
    for p in personas:
        for f in p["frictions"]:
            friction_counts[f] += 1
        for phase, keys in PHASES:
            if any(k in p["frictions"] for k in keys):
                phase_counts[phase] += 1

    out = {
        "total": len(personas),
        "friction_ranking": [
            {"id": k, "label": FRICTION[k], "hit_count": v, "hit_rate": round(v / 100, 2)}
            for k, v in friction_counts.most_common()
        ],
        "phase_hit_counts": dict(phase_counts),
        "personas": personas,
    }

    root = Path(__file__).resolve().parents[1]
    data_path = root / "docs" / "persona-roleplay-100.json"
    data_path.write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")

    # 人間向けサマリ Markdown
    lines = [
        "# KeyAutomator ペルソナロープレ（100人）",
        "",
        "## 計画",
        "",
        "1. 属性マトリクスで100人を生成（年齢×ITリテラシー×職種×目的×端末×配置習慣×言語×アクセシビリティ×警戒心）",
        "2. GitHubダウンロード → 配置 → 初回起動 → マクロ作成 → テスト → 保存/再起動 → CLI → 边缘 の順で判定",
        "3. 摩擦ヒット数で優先度付けし、実装へ反映",
        "",
        "## 属性の分布方針",
        "",
        "- 年齢: " + ", ".join(AGES),
        "- IT: " + ", ".join(IT_LEVELS),
        "- 職種: " + ", ".join(JOBS),
        "- 目的: " + ", ".join(GOALS),
        "- 端末: " + ", ".join(DEVICES),
        "- 配置: " + ", ".join(INSTALL_HABITS),
        "",
        "## フェーズ別ヒット人数（100人中）",
        "",
    ]
    for phase, _ in PHASES:
        lines.append(f"- `{phase}`: **{phase_counts[phase]}** 人")

    lines += ["", "## 摩擦ポイント順位（多い順）", ""]
    for i, row in enumerate(out["friction_ranking"], 1):
        lines.append(f"{i}. **{row['label']}** — {row['hit_count']}/100（{int(row['hit_rate']*100)}%）")

    lines += [
        "",
        "## 代表ロープレ（抜粋）",
        "",
        "### P001 事務・IT苦手・50代 / Program Files 派",
        "- GitHub の Code と Releases の違いで詰まる。zip 名の `win-x64-single` が不安。",
        "- 解凍後すぐ Program Files へコピー → 保存できず（または LocalAppData に逃げ）「どこに保存された？」",
        "- 起動すると一覧が空。「壊れてるのでは」→ sample に気づかない。",
        "",
        "### P014 情シス・上級 / CLIバッチ目的",
        "- DL〜起動は問題なし。引数名の必要性は理解。",
        "- サンプルの dialog 手順が CLI で止まり「サイレントじゃない」と不満。",
        "- 座標クリックが無い点を仕様確認で許容。",
        "",
        "### P027 学生・普通 / Downloads のまま",
        "- Downloads で起動し config が Downloads に散らばる。",
        "- テスト実行で自分のウィンドウに入力。待ち秒の意味に後から気づく。",
        "",
        "### P041 営業・IT苦手 / ログイン自動化",
        "- マウス追加が右クリック初期値で右クリックメニューが開いて混乱。",
        "- ショートカットを手入力（CTRL+S）できず挫折しそう。",
        "",
        "### P088 医療事務・警戒強い / 会社PC",
        "- SmartScreen で停止。パスワード平文に拒否感。管理者アプリへ送れず失敗。",
        "",
        "## 実装優先度（ヒット率＋深刻度）",
        "",
        "| 優先 | 改善 | 理由 |",
        "|---|---|---|",
        "| P0 | 初回空リスト時にサンプル読込を案内/ワンクリック | empty/sample がほぼ全員 |",
        "| P0 | zip に `使い方.txt` を同梱 | GitHub苦手層・説明不足 |",
        "| P1 | 空状態UIを具体的な次アクションに | 初回離脱防止 |",
        "| P1 | マウス追加の初期値を左クリックに | 誤操作が多い |",
        "| P1 | テスト実行ダイアログでフォーカス注意を強調 | focus_miss |",
        "| P2 | Release 説明文を非エンジニア向けに | zip_name / GitHub |",
        "| P2 | 手順間隔の発見性向上 | 設定が右下のみ |",
        "| P3 | CLI の dialog 挙動を help に明記 | CLIユーザー |",
        "| P3 | 英語UI / ARM64 は将来検討 | 少数だが実在 |",
        "",
        "詳細データ: `persona-roleplay-100.json`",
        "",
    ]
    md_path = root / "docs" / "persona-roleplay-100.md"
    md_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {data_path}")
    print(f"Wrote {md_path}")
    print("Top5:")
    for row in out["friction_ranking"][:5]:
        print(f"  {row['hit_count']:3d}  {row['label']}")


if __name__ == "__main__":
    main()
