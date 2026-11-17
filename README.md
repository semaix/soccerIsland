# soccerIsland

アニメ調2Dのサッカー・オープンワールド。島民全員が小さい頃からサッカーをしている島に、大人の主人公が引っ越してくる。

## 実装済み：NPCデータベース / NPC生成システム

```
Assets/Scripts/NPC/
├─ Core/         データ定義（Unity非依存）
│   ├─ NPCEnums.cs          街・職業・性格・サッカー性格・成長タイプ・関係・情報フラグ
│   ├─ NPCData.cs           島民1人分（表の情報＋隠しパラメータ）
│   ├─ NPCAbilities.cs      隠し能力値10種（プレイヤーには非表示）
│   ├─ SoccerClub.cs        町クラブ / 職場チーム / 裏フットサル / 刑務所 / ビーチ
│   ├─ NPCDatabase.cs       検索インデックス・協会ランキング・裏ランキング・電話番号検索
│   └─ NPCLabels.cs         日本語表示名・ランク表記・金額表記
├─ Generation/   島民生成（Unity非依存・シード固定で再現可能）
│   ├─ NPCGenerator.cs           1000人生成＋クラブ所属＋相関（家族/同級生/同僚/親分子分/ライバル/師弟/友人）
│   ├─ NPCGenerationSettings.cs  街プロファイル・職業プロファイル・各種確率
│   ├─ NPCNamePool.cs            名前候補
│   └─ SeededRandom.cs           決定的乱数（セーブ可能）
├─ Systems/      ゲームロジック（Unity非依存）
│   ├─ GrowthCurve.cs            成長タイプ×年齢×努力 → 能力値
│   ├─ MarketValueCalculator.cs  市場価値・年俸の計算式
│   ├─ MarketValueSystem.cs      話題性(hype)と日々の変動
│   ├─ NPCConversationSystem.cs  会話で情報が判明・噂で市場価値が変動・相関図が増える
│   ├─ NPCDialogue.cs            性格別のセリフ（仮テキスト）
│   ├─ NPCKnowledgeBook.cs       プレイヤーの手帳（判明情報・好感度・相関図・電話帳）
│   ├─ NPCProfileView.cs         UI用プロフィール（未判明は「？？？」、能力値は出さない）
│   ├─ NPCLifecycleSystem.cs     年越し：加齢・覚醒・出所・卒業・引退
│   └─ IslandPopulation.cs       上記をまとめた窓口＋セーブデータ
├─ Unity/
│   ├─ NPCManager.cs             シーン常駐コンポーネント（生成/ロード/セーブ/イベント）
│   └─ NPCGenerationConfig.cs    設定 ScriptableObject
└─ Editor/
    └─ NPCDatabaseWindow.cs      デバッグ用ビューア（隠しパラメータも閲覧可）
Assets/Tests/EditMode/NPCSystemTests.cs
```

### 使い方

1. 空の GameObject に `NPCManager` を付ける（`config` は空ならデフォルト設定）。
2. 調整したい場合は Project ビューで **Create > SoccerIsland > NPC Generation Config** を作り、`NPCManager.config` に設定。
3. ゲームからの呼び出し:

```csharp
var mgr = NPCManager.Instance;
ConversationResult r = mgr.Talk(npcId);          // 会話：r.lines にセリフ、r.marketChanges に価値変動
NPCProfileView p = mgr.GetProfile(npcId);        // 判明済みの情報だけ入ったプロフィール
var top = mgr.Database.GetOfficialMarketValueRanking(20); // サッカー協会の市場価値ランキング
var tel = mgr.Database.FindByPhone("島-1234-5678");       // 電話システム
mgr.AdvanceDay();                                 // 1日進める（365日で年越し）
mgr.Save();
```

4. エディタで **SoccerIsland > NPC Database Viewer** を開くと、シードを変えて島を生成し、1000人の中身（隠し能力・潜在・覚醒年齢・人生周回）を確認できる。

### 仕様メモ

| 項目 | 内容 |
|---|---|
| 街 | 勇心町（人助け・気が強い）／古鐘町（人情・田舎・ヤンキー）／煌浜（垢抜け・チャラい）／天峰台（意識高い・島1番のプライド）。職業・性格・努力の傾向が街ごとに違う |
| 職業 | 学生、サラリーマン、土木、医者、看護師、ショップ店員、コンビニ、マフィア、警官、消防士、漁師、農家、教師、バーテン、料理人、美容師、銀行員、新聞記者、ライフセーバー、アイドル、無職、隠居、受刑者、漂流者 |
| 性格 | 内向的 / 外交的 / 根暗（口の軽さ・好感度の上がり方が変わる） |
| サッカー性格 | ムードメーカー / エゴイスト / センス的 / ヒステリー / ファイター / 献身的 ＋ 特殊：ゴッド / ギフテッド / 幻影 |
| 成長タイプ | 早熟 / 普通 / 晩成 / **覚醒型**（ずっと下手→ある年齢で化ける。才能度は低く見えるが本当の上限は高い） / 持続型 |
| 才能度・努力度 | 0-100。会話で判明し、ランク（SS〜E）で表示。能力値そのものは表示しない |
| 市場価値 | 市場は「表で見えている実力」しか知らない。覚醒前の覚醒型・裏で本気を出す人は安い＝掘り出し物 |
| 変動 | 話しかけられると少し話題に、噂で褒められると↑・悪口で↓。話題性は日ごとに0へ戻る |
| 裏 | マフィア等は裏の人間。協会未登録だと公式ランキングに載らず、裏ランキングにだけ出る |
| 隠し設定 | `lifeCycle`（人生何周目か）、`publicPerformanceRate`（表では手を抜く）、刑務所の天才と出所年 |

### 個人情報の方針

- 端末（スマホ）内の個人情報は使わない：連絡先・電話番号・位置情報・カメラ・マイク・端末ID・端末名・広告ID・クリップボードを読まない。
- 外部に出さない：通信・アナリティクス・外部URLを開く処理を持たない。セーブは端末内のみで、iOSではiCloudバックアップ対象外にする。
- NPCの名前・電話番号はすべて架空。電話番号は実在番号と一致しない `島-XXXX-XXXX` 形式。
- `PrivacyComplianceTests` が、`Assets/Scripts` に上記に反するAPIが入ると失敗する。
- Unityプロジェクト作成時の設定：Services の Analytics / Cloud Diagnostics をオフ、Android は `allowBackup=false` にする。

### テスト

Unity の Test Runner（EditMode）で `SoccerIsland.NPC.Tests` を実行。
