#!/usr/bin/env bash
# Unity を使わずに、全スクリプトのコンパイル確認と EditMode テストを実行する。
# 必要なもの：mono（mcs）。  例：apt-get install -y mono-mcs mono-runtime
# UnityEngine/UnityEditor は最小のスタブ（UnityStub.cs / UnityEditorStub.cs）で代用する。
# Unity は Assets の外を読まないので、このフォルダのファイルが本番に混ざることはない。
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="${OUT:-$(mktemp -d)}"
mkdir -p "$OUT"
S="$ROOT/Assets/Scripts"
cd "$ROOT"

mcs -langversion:Latest -target:library -out:"$OUT/UnityEngine.dll" Tools/CompileCheck/UnityStub.cs
mcs -langversion:Latest -target:library -r:"$OUT/UnityEngine.dll" -out:"$OUT/UnityEditor.dll" Tools/CompileCheck/UnityEditorStub.cs

lib() { local out=$1; shift; mcs -langversion:Latest -nowarn:414,219,169,649 -target:library -r:"$OUT/UnityEngine.dll" -r:System.Core -out:"$OUT/$out" "$@"; }
lib npc.dll    $(find "$S/NPC" -name '*.cs' -not -path '*/Editor/*')
lib world.dll  -r:"$OUT/npc.dll" $(find "$S/World" -name '*.cs')
lib career.dll -r:"$OUT/npc.dll" -r:"$OUT/world.dll" $(find "$S/Career" -name '*.cs')
lib match.dll  -r:"$OUT/npc.dll" -r:"$OUT/world.dll" -r:"$OUT/career.dll" $(find "$S/Match" -name '*.cs')
lib npc_editor.dll -r:"$OUT/UnityEditor.dll" -r:"$OUT/npc.dll" $(find "$S/NPC/Editor" -name '*.cs')
lib privacy_editor.dll -d:UNITY_ANDROID -r:"$OUT/UnityEditor.dll" -r:System.Xml $(find "$S/Privacy" -name '*.cs')
echo "コンパイル OK"

mcs -langversion:Latest -nowarn:414,219,169,649 -r:"$OUT/npc.dll" -r:"$OUT/world.dll" -r:"$OUT/career.dll" -r:"$OUT/match.dll" -r:"$OUT/UnityEngine.dll" -r:System.Core \
    -out:"$OUT/tests.exe" Tools/CompileCheck/NUnitShim.cs Assets/Tests/EditMode/*.cs
LANG=C.UTF-8 MONO_PATH="$OUT" mono "$OUT/tests.exe"
