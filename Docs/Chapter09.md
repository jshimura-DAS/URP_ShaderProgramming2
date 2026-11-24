# Chapter 09：Render Graph への移行と Shader Graph・ワイプ演出

この章は、本プロジェクト（Render Graph 有効）で行います。Chapter 08 の輪郭線だけを Render Graph 版へ移行し、その後に Render Graph の構造と Shader Graph の基礎を扱います。最後に、Render Graph を使ったワイプ演出を実装します。

> Chapter 08 のステルス（`Stealth08.shader`）とディザリング透過（`Dither08.shader`）は、マテリアルのシェーダーだけで動作します。`Renderer Feature` や `ScriptableRenderPass` を使わないため、移行は不要です。

---

## 0. 移行前の確認と最終的なファイル構成

Chapter 08 の 0 章で `URP_COMPATIBILITY_MODE` を設定している場合は、この章を始める前に次の作業を行ってください。

### 0.1 作業の前提

| 確認項目 | 対処 |
| :--- | :--- |
| `URP_COMPATIBILITY_MODE` が設定されている | Player 設定の Scripting Define Symbols から削除する。残っていると `RecordRenderGraph` が呼ばれず、この章のパスが動かない |
| Graphics 設定で Compatibility Mode が有効 | 無効にし、Render Graph が有効な状態にする |
| `EmptyRendererFeature` が `PC_Renderer` に登録されている | 0.2 の手順で登録を外してからファイルを削除する |
| `Chapter08/OutlinePass.cs` が存在する | 削除する（0.3 参照） |

### 0.2 EmptyRendererFeature の削除手順

`EmptyRendererFeature.cs` を削除する前に、登録を外します。登録したままファイルを消すと、Renderer Data の Inspector に `Missing` の表示が残ります。

1. `Assets/Settings/PC_Renderer.asset` を選択する
2. Inspector の Renderer Feature 一覧で **Empty Renderer Feature** を選び、右上のメニュー（⋮）から **Remove** を選ぶ
3. 同じ手順で `Mobile_Renderer.asset` からも外す（登録している場合のみ）
4. `Assets/ShaderProgramming/Chapter08/EmptyRendererFeature.cs` と `EmptyRendererFeature.cs.meta` を削除する

### 0.3 OutlinePass の重複を解消する

Chapter 08 の `OutlinePass.cs`（Execute 版）と、Chapter 09 の `OutlinePass.cs`（Render Graph 版）は、同じ名前のクラス `OutlinePass` を定義しています。Unity では同名のクラスを 2 つ持てないため、次のどちらか 1 つだけを残します。

| 残すファイル | 判断 |
| :--- | :--- |
| `Assets/ShaderProgramming/Chapter09/OutlinePass.cs` | **残す**。Render Graph 版であり、本章の最終形 |
| `Assets/ShaderProgramming/Chapter08/OutlinePass.cs` | **削除する**。Execute 版で、Render Graph では動作しない |

削除後にコンパイルエラー（CS0101：型 'OutlinePass' は既に定義されています）が消えていれば、この段階は完了です。

### 0.4 RenderGraphBlitUtil.cs を作成する

2.4 で説明する `RenderGraphBlitUtil` は、`OutlinePass.cs` と `WipeRendererFeature.cs` から呼び出されます。このクラスを定義したファイルが無いと、次のエラーが出ます。

> CS0103：現在のコンテキストに 'RenderGraphBlitUtil' という名前は存在しません

ファイル名は `RenderGraphBlitUtil.cs`、クラス名は `RenderGraphBlitUtil` と一致させます。保存先は `Assets/ShaderProgramming/Chapter09/` を推奨します。Unity は保存先フォルダにかかわらずスクリプトを認識するため、既に別のフォルダに作成している場合はそのままでも動きます。

### 0.5 最終的なファイル構成

| ファイル | 場所 | 状態 |
| :--- | :--- | :--- |
| `OutlineFullscreen08.shader` | Chapter08 | 変更なし |
| `OutlineRendererFeature.cs` | Chapter08 | `Dispose` を変更（2.5） |
| `OutlinePass.cs` | Chapter09 | Render Graph 版（2.3） |
| `RenderGraphBlitUtil.cs` | Chapter09 | 新規作成（2.4） |
| `OutlinePass.cs`（Execute 版） | Chapter08 | 削除（0.3） |
| `EmptyRendererFeature.cs` | Chapter08 | 削除（0.2） |
| `Dither08.shader` / `Stealth08.shader` / `StealthController.cs` | Chapter08 | 変更なし |
| `Lambert09.shadergraph` | Chapter09 | 新規作成（8 章） |
| `WipeFullscreen09.shader` / `WipeRendererFeature.cs` / `WipeController.cs` | Chapter09 | 新規作成（9 章） |

---

## 1. 学習目標

- Chapter 08 の輪郭線を、`Execute` 方式から `RecordRenderGraph` 方式へ移行できる
- Render パイプラインの階層と Render Graph の位置づけを説明できる
- Render Graph の Record・Compile・Execute の段階を説明できる
- Render Graph Viewer と Frame Debugger で、パスとリソースの読み書きを読み取れる
- `TextureHandle` とパスの読み書き宣言を使って、フルスクリーン処理を組み立てられる
- Shader Graph でノードを接続し、簡単なライティングを作れる
- Render Graph を使ったワイプ演出を実装し、進行度を C# から制御できる

---

## 2. Chapter 08 輪郭線の Render Graph 版への移行

### 2.1 移行の対象と対象外

| 項目 | 移行の要否 | 理由 |
| :--- | :--- | :--- |
| `OutlinePass.cs`（Chapter08） | 削除する | Execute 版。Chapter09 の Render Graph 版と同名のクラスのため、残すとコンパイルエラーになる |
| `OutlinePass.cs`（Chapter09） | 新規に用意する（本章で作成） | Render Graph 版の本体 |
| `OutlineRendererFeature.cs` | 一部変更する | `Dispose` でパスの `RTHandle` を解放していたため、その処理を削除する |
| `OutlineFullscreen08.shader` | 変更しない | `Blit.hlsl` の `_BlitTexture` を使うため、Render Graph でも同じ書き方で動く |
| `EmptyRendererFeature.cs` | 削除する | 教材の骨組みとしての役割を終えたため（0.2 参照） |
| `Stealth08.shader` / `StealthController.cs` | 変更しない | マテリアルと `MaterialPropertyBlock` だけで動作するため |
| `Dither08.shader` | 変更しない | 同上 |

### 2.2 Execute と RecordRenderGraph の対応

Chapter 08 の `Execute` では、描画命令を実行時に `CommandBuffer` へ書いていました。Render Graph では、次のように役割が分かれます。

| Chapter 08（Execute） | Chapter 09（Render Graph） | 役割 |
| :--- | :--- | :--- |
| `Execute` の中の `Blitter.BlitCameraTexture` | `AddRasterRenderPass` と `SetRenderFunc` | 処理の宣言と、実際の描画命令を分ける |
| `RTHandle _tempTarget` と `Release` | `renderGraph.CreateTexture` | 一時テクスチャは Render Graph が寿命を管理する |
| `ConfigureInput(Depth)` | `builder.UseTexture(depth)` | 読み取るリソースを、パスごとに宣言する |
| `renderingData.cameraData.renderer.cameraColorTargetHandle` | `UniversalResourceData.activeColorTexture` | カメラのカラーを `TextureHandle` で取得する |

**ポイント**は、`RecordRenderGraph` の中ではまだ GPU の描画が行われないことです。ここでは「どのリソースを読み、どのリソースに書くか」を宣言するだけです。実際の描画は、後で呼ばれる `SetRenderFunc` のラムダ式の中で行われます。

### 2.3 OutlinePass（Render Graph 版）

`Assets/ShaderProgramming/Chapter09/OutlinePass.cs` を次の内容で作成します（Chapter 08 の同名ファイルは削除済みであること）。

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class OutlinePass : ScriptableRenderPass
{
    Material _material;
    OutlineRendererFeature.Settings _settings;

    class PassData
    {
        public TextureHandle source;
        public Material material;
    }

    public OutlinePass()
    {
        // 深度を使うことを URP に伝える（Depth Texture を生成させるため）
        ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public void Setup(Material material, OutlineRendererFeature.Settings settings)
    {
        _material = material;
        _settings = settings;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_material == null) return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        // 設定値はレコード時にマテリアルへ反映する（描画時に読まれる）
        _material.SetColor("_OutlineColor", _settings.outlineColor);
        _material.SetFloat("_Threshold", _settings.threshold);
        _material.SetFloat("_Sharpness", _settings.sharpness);
        _material.SetFloat("_Thickness", _settings.thickness);

        TextureHandle source = resourceData.activeColorTexture;
        TextureHandle depth = resourceData.cameraDepthTexture;

        // カメラと同じ形式の一時テクスチャを宣言する（深度は不要）
        TextureDesc desc = renderGraph.GetTextureDesc(source);
        desc.name = "_OutlineTemp";
        desc.clearBuffer = false;
        desc.depthBufferBits = DepthBits.None;
        TextureHandle temp = renderGraph.CreateTexture(desc);

        // パス 1：カラーと深度を読み、輪郭を重ねて temp へ書く
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Outline08", out var passData))
        {
            passData.source = source;
            passData.material = _material;

            builder.UseTexture(source);
            builder.UseTexture(depth);
            builder.SetRenderAttachment(temp, 0);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
            });
        }

        // パス 2：temp の結果を、カメラのカラーへ戻す
        RenderGraphBlitUtil.AddCopyPass(renderGraph, temp, source, "Outline08 Copy");
    }
}
```

**コードの要点**

- `desc.name` で、Render Graph Viewer に表示される名前を決めます。
- `builder.UseTexture` で読み取るリソースを宣言すると、Render Graph は依存関係を把握できます。宣言がないパスは、依存関係が正しく扱われません。
- `SetRenderFunc` の中では、`_settings` のようなフィールドを参照しないでください。実行時に値が変わっている可能性があるため、必要な値はレコード時にマテリアルへ反映しておきます。

> **注意（API の版による差）**
> 本節の API 名は、URP 17.x（Unity 6.x）で一般的に使われる書き方です。`TextureHandle` を `Blitter` に渡す部分、`_CameraDepthTexture` を参照させる登録方法（`UseTexture` と `UseGlobalTexture` のどちらか）は、URP のバージョンで書き方が変わることがあります。Package Manager の **Universal RP → Samples** に含まれる Render Graph の Fullscreen サンプルと照合してください。

### 2.4 共通のコピーパス（RenderGraphBlitUtil.cs）

2 つの Blit パスを 1 か所にまとめるため、`Assets/ShaderProgramming/Chapter09/RenderGraphBlitUtil.cs` を作成します。

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public static class RenderGraphBlitUtil
{
    class PassData
    {
        public TextureHandle source;
        public Material material;
    }

    // src を dst へ転送する。material が null なら単純コピー
    public static void AddCopyPass(RenderGraph renderGraph, TextureHandle src, TextureHandle dst,
                                   string passName, Material material = null)
    {
        using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
        {
            passData.source = src;
            passData.material = material;

            builder.UseTexture(src);
            builder.SetRenderAttachment(dst, 0);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                if (data.material != null)
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                else
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0f, false);
            });
        }
    }
}
```

> このクラスは `OutlinePass` と `WipePass` の両方から使います。`static` クラスのため、どの名前空間からも `RenderGraphBlitUtil.AddCopyPass(...)` で呼び出せます。

### 2.5 OutlineRendererFeature の変更点

`Assets/ShaderProgramming/Chapter08/OutlineRendererFeature.cs` は、次の 1 点だけ変更します。

1. `Dispose` の中の `_pass?.Dispose();` を削除する（Render Graph 版では一時テクスチャを手動で解放しないため）

```csharp
protected override void Dispose(bool disposing)
{
    CoreUtils.Destroy(_material);
}
```

`Create` の `_pass = new OutlinePass { ... }` と `AddRenderPasses` の処理（Game カメラのみ、`EnqueuePass`）は、そのまま動作します。`OutlinePass` は Chapter09 フォルダにありますが、同じアセンブリ内の型のため、フォルダをまたいでも参照できます。

### 2.6 Renderer Data への登録と確認

ここでは、Renderer Data（`PC_Renderer.asset`）に輪郭線の Feature を登録し、Shader 欄へシェーダーを割り当てます。

**手順 1：Renderer Data を開く**

1. 画面下の **Project** ウィンドウで、左のフォルダツリーの `Assets > Settings` を開く
2. `PC_Renderer` をクリックする

Inspector（右側）に Renderer の設定が表示されます。Renderer Feature の一覧は、Inspector の下のほうにあります。

**手順 2：Outline Renderer Feature を追加する**

1. Inspector の下部にある **Add Renderer Feature** ボタンを押す
2. 表示されたメニューから **OutlineRendererFeature** を選ぶ
3. 一覧に `Outline Renderer Feature` の項目が追加されたことを確認する

> すでに一覧に表示されている場合は、この手順は不要です。

**手順 3：Shader に OutlineFullscreen08 を割り当てる**

1. 追加された `Outline Renderer Feature` の項目を開く（項目名の左の三角をクリック）
2. **Shader** 欄の右側にある小さな丸い選択ボタン（◎）をクリックする
3. 一覧から `OutlineFullscreen08`（`ShaderProgramming/Chapter8/OutlineFullscreen08`）を選ぶ

一覧に出てこない場合は、Project ウィンドウで `Assets/ShaderProgramming/Chapter08/OutlineFullscreen08.shader` を探し、Shader 欄へドラッグ＆ドロップします。Shader 欄が **None** のままだと、輪郭線は描画されません。

**手順 4：Render Pass Event を確認する**

同じ項目内の **Render Pass Event** を `Before Rendering Post Processing` に変更します。既定値を変えても、保存済みの値は自動では変わらないため、必ず手動で確認してください。

**手順 5：動作を確認する**

1. Game ビューを再生し、物体の縁に黒い線が出ていることを確認する
2. `Window > Analysis > Render Graph Viewer` を開き、`Outline08` と `Outline08 Copy` が表示されていることを確認する

### 2.7 移行チェックリスト

| 確認項目 | 期待する状態 |
| :--- | :--- |
| Console のエラー | CS0101（型の重複）と CS0103（RenderGraphBlitUtil が存在しない）が出ていない |
| Console の警告 | `RecordRenderGraph method` の警告が出ていない |
| Render Graph Viewer | `Outline08`、`Outline08 Copy` が表示される |
| 黒い画面になる | `_OutlineTemp` の書き込み先が `source` と同じになっていないか確認する |
| 線が出ない | Shader 欄が None でないか、Render Pass Event、Pipeline Asset の Depth Texture を確認する |
| 警告が出る | `URP_COMPATIBILITY_MODE` が残っていないか（0.1 参照） |

---

## 3. Render パイプラインの構造と Render Graph の関係

### 3.1 階層の整理

Render パイプラインは、設定の階層と、描画の実行の階層に分けて考えると理解しやすくなります。

```mermaid
flowchart TD
    A["RenderPipelineAsset<br/>PC_RPAsset.asset<br/>影・深度・HDR などの全体設定"] --> B["RenderPipeline<br/>カメラごとに呼ばれる"]
    B --> C["ScriptableRenderer<br/>PC_Renderer.asset（Renderer Data）"]
    C --> D["Renderer Feature<br/>OutlineRendererFeature など"]
    D --> E["ScriptableRenderPass<br/>OutlinePass など"]
    E --> F["Render Graph<br/>パスとリソースの宣言"]
    F --> G["GPU 命令<br/>RenderFunc の実行"]
```

- **Pipeline Asset**：Chapter 07 で扱った影の設定や、Depth Texture の有無を決めます。
- **Renderer Data**：どのパスを、どの順番で実行するかを決めます。Feature はここに登録します。
- **Render Graph**：Renderer が組み立てたパスを、依存関係をもとに整理して実行します。

### 3.2 1 フレームの流れ

```mermaid
flowchart LR
    S["Shadow"] --> O["Opaque"]
    O --> K["Skybox"]
    K --> T["Transparent"]
    T --> P["PostProcessing"]
    P --> OL["Outline08<br/>(BeforeRenderingPostProcessing)"]
    OL --> W["Wipe09<br/>(AfterRenderingPostProcessing)"]
    W --> F["Final Blit"]
```

Chapter 08 で示した流れに、本章の 2 つの独自パスを加えた構成です。輪郭線は Post Processing の前、ワイプは Post Processing の後に入ります。

### 3.3 Record・Compile・Execute

Render Graph は、1 フレームの処理を 3 段階に分けます。

```mermaid
flowchart LR
    A["Record<br/>RecordRenderGraph()<br/>パスとリソースを宣言"] --> B["Compile<br/>不要なパスを削除<br/>リソースの寿命を計算"]
    B --> C["Allocate<br/>寿命が重ならないリソースは<br/>同じメモリを再利用"]
    C --> D["Execute<br/>SetRenderFunc を順に実行<br/>GPU へ命令を発行"]
```

| 段階 | 行われること | 開発者が意識すること |
| :--- | :--- | :--- |
| Record | パスの登録、リソースの作成と読み書きの宣言 | 読み書きを正しく `UseTexture` / `SetRenderAttachment` で宣言する |
| Compile | 誰も読まないパスの削除、寿命の計算 | 結果を使わないパスは自動で消えるため、Viewer で確認する |
| Allocate | 一時テクスチャの確保と再利用 | 手動で `Release` する必要がない |
| Execute | 実際の描画 | `SetRenderFunc` の中では、Record 時の値だけを使う |

Execute 方式では、この 3 段階が 1 つにまとまっていました。Render Graph はこれを分けたことで、リソースの再利用と不要パスの削除を自動で行えるようになっています。

### 3.4 Render Graph の 3 要素

| 要素 | 内容 | 本章での例 |
| :--- | :--- | :--- |
| **パス（Pass）** | 1 回の描画・処理の単位 | `Outline08`、`Wipe09` |
| **リソース（Resource）** | パス間で受け渡すテクスチャなど | `activeColorTexture`、`_OutlineTemp` |
| **依存（Dependency）** | どのパスがどのリソースを読み、書くか | 読み書きの宣言（`UseTexture`、`SetRenderAttachment`） |

依存関係が宣言されると、Render Graph は「このパスの結果は次のパスで使われる」ことを理解します。逆に、宣言が不足していると、正しい順序で実行されず、描画結果が壊れます。

### 3.5 UniversalResourceData と ContextContainer

`RecordRenderGraph` の引数 `frameData`（`ContextContainer`）には、URP がフレームごとの情報を格納しています。よく使うのは次のとおりです。

| 取得するもの | 中身 | 用途 |
| :--- | :--- | :--- |
| `UniversalResourceData` | カラー・深度・シャドウなどの `TextureHandle` | 入力と出力のリソースを取得する |
| `UniversalCameraData` | カメラの種類、解像度など | 描画対象のカメラを判定する |
| `UniversalLightData` | ライトの情報 | ライトを使うパスで参照する |

```csharp
UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
```

`activeColorTexture` は現在のカメラの描画先で、Post Processing の段階によって指す先が変わることがあります。値を取得する位置を、パスの順番と合わせて確認してください。

### 3.6 Execute 方式と Render Graph 方式の比較

| 項目 | Execute（Compatibility Mode） | Render Graph |
| :--- | :--- | :--- |
| 処理の書き方 | 実行時に `CommandBuffer` へ命令を書く | 宣言（Record）と実行（RenderFunc）を分ける |
| 一時テクスチャ | `RTHandle` を手動で確保・解放する | `CreateTexture` で宣言し、寿命は自動管理 |
| 依存関係 | 開発者が順序を管理する | 読み書きの宣言から自動で把握する |
| 不要パス | 残る | 結果が使われなければ削除される |
| 方式の状態 | 廃止予定 | 現在の標準 |

---

## 4. Render Graph の見方

### 4.1 Render Graph Viewer

`Window > Analysis > Render Graph Viewer` で開きます。Game ビューを再生した状態で開くと、そのフレームの Render Graph が表示されます。

**確認する項目**

| 項目 | 見るポイント |
| :--- | :--- |
| パス一覧 | 実行順。`Outline08` が Post Processing の前後のどこにあるか |
| 読み取り・書き込み | 各パスが、どのリソースを読み、どのリソースに書いたか |
| リソースの寿命 | 一時テクスチャがいつ作られ、いつ使われなくなるか |
| 削除されたパス | 結果が使われず、実行されなかったパス |

### 4.2 Render Graph の見え方（概念図）

次の SVG は、Render Graph Viewer の見え方を簡略化した概念図です。上の行に実行順のパス、左の列にリソースを並べ、各セルで読み書きを示しています。

![Render Graph の概念図](images/chapter09_rendergraph_viewer.svg)

**読み取りのポイント**

1. `Outline08` は、`Camera Color` と `Depth` を読み（R）、`_OutlineTemp` に書き（W）ます。
2. `Outline08 Copy` は、`_OutlineTemp` を読み、`Camera Color` に書き戻します。
3. `_OutlineTemp` の寿命は、書き込みから読み取りまでの間だけです。その後は、同じメモリを別のリソースに再利用できます。
4. `Camera Color` は全体にわたって使われるため、寿命が最も長くなります。

### 4.3 Frame Debugger での確認

Frame Debugger でも Render Graph のパスを確認できます。Render Graph 版では、パスの名前は `AddRasterRenderPass` に指定した文字列で表示されます。Execute 方式のときに見えていた `ScriptableRenderer.Execute` の項目は、Render Graph を使うと表示が変わります。

---

## 5. Render Graph の利用方法

### 5.1 パスの種類

| パスの種類 | 登録する API | 用途 |
| :--- | :--- | :--- |
| **Raster** | `AddRasterRenderPass<T>` | 描画（ラスタライズ）を行う。フルスクリーン処理、MRT（複数出力）に使う |
| **Unsafe** | `AddUnsafePass<T>` | `CommandBuffer` を直接扱う。柔軟だが、依存関係の検査が弱い |
| **Compute** | `AddComputePass<T>` | Compute シェーダーを実行する（Chapter 13 で扱う） |

本章では Raster パスを使います。フルスクリーン処理では、ほとんどの場合これで足ります。

### 5.2 基本の手順

Render Graph を使うパスは、次の 5 段階で書きます。

```mermaid
flowchart TD
    A["1. リソースを取得<br/>frameData.Get&lt;UniversalResourceData&gt;()"] --> B["2. 一時テクスチャを宣言<br/>renderGraph.CreateTexture(desc)"]
    B --> C["3. パスを登録<br/>AddRasterRenderPass&lt;PassData&gt;"]
    C --> D["4. 読み書きを宣言<br/>UseTexture / SetRenderAttachment"]
    D --> E["5. 描画処理を定義<br/>SetRenderFunc"]
```

### 5.3 テンプレート

```csharp
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
{
    // 1. リソースを取得
    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
    TextureHandle source = resourceData.activeColorTexture;

    // 2. 一時テクスチャを宣言（寿命は Render Graph が管理）
    TextureDesc desc = renderGraph.GetTextureDesc(source);
    desc.name = "_MyTemp";
    desc.clearBuffer = false;
    desc.depthBufferBits = DepthBits.None;
    TextureHandle temp = renderGraph.CreateTexture(desc);

    // 3〜5. パスを登録し、読み書きを宣言し、描画処理を定義する
    using (var builder = renderGraph.AddRasterRenderPass<PassData>("MyPass", out var passData))
    {
        passData.source = source;

        builder.UseTexture(source);          // 読む
        builder.SetRenderAttachment(temp, 0); // 書く

        builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
        {
            // ここで GPU 命令を発行する
            Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0f, false);
        });
    }
}
```

### 5.4 よくある誤り

| 誤り | 症状 | 対処 |
| :--- | :--- | :--- |
| 読むリソースを `UseTexture` で宣言していない | 描画結果が前のフレームのものになる、または黒くなる | 読む前に必ず宣言する |
| 同じリソースを読み書きに同時に使う | エラーまたは不定の結果 | 一時テクスチャを経由して 2 段階に分ける |
| `SetRenderFunc` の中で `this` のフィールドを読む | 実行時の値が反映されない | 必要な値は PassData に入れるか、Record 時にマテリアルへ反映する |
| 一時テクスチャを `Release` しようとする | コンパイルエラーまたは未定義の動作 | Render Graph が管理するため、解放処理を書かない |
| 深度を使うのに Depth Texture を有効にしていない | `_CameraDepthTexture` が黒または無効 | Pipeline Asset の Depth Texture を有効にする |

### 5.5 MRT（複数の出力先）

同じパスで複数のテクスチャに書くこともできます。G-Buffer（Chapter 11）では、この方法で法線・アルベドなどを同時に書きます。

```csharp
builder.SetRenderAttachment(gBuffer0, 0);
builder.SetRenderAttachment(gBuffer1, 1);
builder.SetRenderAttachment(gBuffer2, 2);
```

---

## 6. Shader Graph の基本

### 6.1 Shader Graph とは

Shader Graph は、ノードを線で接続してシェーダーを作るツールです。接続の結果から、HLSL のシェーダーを自動で生成します。

| 比較項目 | ShaderLab + HLSL（Chapter 01〜08） | Shader Graph（Chapter 09） |
| :--- | :--- | :--- |
| 書き方 | テキストで書く | ノードを接続する |
| 出力 | 自分で書いたシェーダー | 生成されたシェーダー（View Generated Shader で確認できる） |
| 細かな制御 | 完全に制御できる | ノードで表現できる範囲に限られる |
| 学習の目的 | 仕組みの理解 | 見た目の試行錯誤、ノード化された計算の理解 |

**本教材の方針**：Shader Graph は、HLSL で学んだ計算がノードでどう表されるかを対応させながら使います。生成されたコードを必ず確認してください。

### 6.2 作成と画面の見方

**作成手順**

1. Project ウィンドウで `Assets/ShaderProgramming/Chapter09/` を開く（フォルダがなければ右クリックして `Create > Folder` で作る）
2. フォルダの余白を右クリックし、`Create > Shader Graph > URP > Unlit Shader Graph` を選ぶ
3. 名前の入力欄が出たら `Lambert09` と入力して Enter を押す
4. `Lambert09` をダブルクリックして開く

**画面の構成**

| 領域 | 内容 |
| :--- | :--- |
| Graph エリア（中央） | ノードを配置して線で接続する |
| Blackboard（左上） | Properties（プロパティ）を追加・管理する |
| Graph Inspector（右上） | 選んだ項目の設定（Node Settings / Graph Settings） |
| Master Stack（右端） | 最終出力。Unlit では Fragment の Base Color など |
| Main Preview（右下） | 結果のプレビュー |

### 6.3 ノードの基本

| 種類 | 代表例 | 役割（HLSL との対応） |
| :--- | :--- | :--- |
| 入力 | Normal Vector、Main Light Direction、Position | `IN.normalWS`、`GetMainLight().direction`、`IN.positionWS` |
| 数学 | Dot Product、Saturate、Multiply、Add | `dot()`、`saturate()`、`*`、`+` |
| 変換 | Transform、Normalize | 座標空間の変換、`normalize()` |
| テクスチャ | Sample Texture 2D | `SAMPLE_TEXTURE2D()` |
| プロパティ | Color、Float、Texture2D | `Properties` ブロックと CBUFFER の変数 |

**ノードの操作の基本**

- ノードの追加：Graph エリアを右クリック、またはスペースキーを押し、検索欄に名前を入力して候補をクリックする
- 接続：出力ポート（右側の丸）から、入力ポート（左側の丸）までドラッグしてマウスを離す
- 接続の解除：線の端をクリックして選択し、Delete キーを押す

### 6.4 プロパティ（Exposed）

Blackboard の `+` ボタンから **Color** などを選んで、プロパティを追加します。名前を変更するには、Blackboard の項目名をダブルクリックして入力します。

マテリアルの Inspector に表示するかどうかは、**Graph Inspector の Node Settings** で設定します。

| 設定 | 意味 | HLSL との対応 |
| :--- | :--- | :--- |
| Reference | シェーダー内で使う名前（`_BaseColor`） | ShaderLab の `Properties` の名前 |
| Show In Inspector（Exposed） | マテリアルの Inspector に表示するか | 表示しなくても値は保持される |
| Default Value | 初期値（色の見本をクリックして変更する） | `Properties` の `= (1,1,1,1)` などの既定値 |

> Unity 6 では Exposed の表示名が **Show In Inspector** に変わっています。意味は同じです。

### 6.5 Vertex と Fragment、Master Stack

Shader Graph は、頂点処理（Vertex）とピクセル処理（Fragment）を分けて扱います。通常は、Fragment 側でノードを接続し、結果を Master Stack に渡します。

| Master Stack の項目 | 対応する HLSL の処理 |
| :--- | :--- |
| Vertex（Position、Normal） | `vert` 関数で出力する位置・法線 |
| Fragment（Base Color、Alpha、Clip Threshold） | `frag` 関数の戻り値、`clip` |

### 6.6 SubGraph と Custom Function

- **SubGraph**：ノードをまとめて再利用する部品です。複数のグラフで同じ計算を使うときに作ります。
- **Custom Function**：HLSL の関数をノードとして呼び出します。Shader Graph で表現しにくい処理に使います。

```hlsl
// Custom Function（File モード）の例：Lambert.hlsl
void Lambert_float(float3 Normal, float3 Albedo, out float3 Out)
{
    Light mainLight = GetMainLight();
    float NdotL = saturate(dot(normalize(Normal), normalize(mainLight.direction)));
    Out = Albedo * mainLight.color * NdotL;
}
```

> Custom Function の関数名には、精度のサフィックス（`_float`、`_half`）が必要です。引数名と型が、グラフ側のノードの入出力と一致しているか確認してください。

---

## 7. Shader Graph と Render パイプライン・Render Graph の関係

Shader Graph と Render Graph は、扱う対象が異なります。

| 項目 | Shader Graph | Render Graph |
| :--- | :--- | :--- |
| 対象 | 1 つのマテリアルの見た目（頂点・ピクセルの計算） | 1 フレームの描画パスの組み立て |
| 単位 | マテリアル（Pass の中） | パス（フルスクリーン処理・描画処理） |
| 出力 | シェーダーファイル（HLSL） | 描画命令の順序と、リソースの管理 |
| 関係 | Shader Graph で作ったシェーダーは、Render Graph の中のパスで使われる | Render Graph のパスの中で、Shader Graph のマテリアルを描画することもできる |

**フルスクリーン処理を Shader Graph で作る方法**

URP 17 では、Renderer Feature の **Full Screen Pass Renderer Feature** を使うと、Fullscreen 用の Shader Graph を直接登録できます。本章では、この方法と、C# で書いたパスの違いを 9.6 で比較します。

```mermaid
flowchart LR
    SG["Shader Graph<br/>マテリアルの計算"] --> SH["生成されたシェーダー<br/>HLSL"]
    SH --> PASS["Render Graph のパス<br/>描画の宣言"]
    PASS --> GPU["GPU 命令"]
```

---

## 8. 簡単なシェーディングの実装例：Lambert

### 8.1 目標

表面の向きとライトの向きの内積（`N·L`）で明るさを決める、Lambert 拡散反射を Shader Graph で作ります。第 2 章で HLSL で書いた `saturate(dot(N, L))` と同じ計算です。

### 8.2 完成時のノード構成

```mermaid
flowchart LR
    N["Normal Vector<br/>Space: World"] --> D["Dot Product<br/>A"]
    L["Main Light Direction"] --> D2["Dot Product<br/>B"]
    D --> S["Saturate"]
    S --> M["Multiply<br/>B"]
    C["_BaseColor<br/>Color プロパティ"] --> M2["Multiply<br/>A"]
    M2 --> O["Fragment<br/>Base Color"]
    M --> O
```

> 説明を簡単にするため、図では Multiply を 2 つに分けて描いています。実際の手順では、Dot Product と Main Light Direction の接続が `B` 入力、`_BaseColor` と Saturate の結果を掛けるノードの `A` / `B` 入力、という配置です。

### 8.3 手順

#### 手順 1：Lambert09 を開く

上の 6.2 の手順で作成済みのものを開きます。

#### 手順 2：_BaseColor プロパティを追加し、Show In Inspector にする

**2-1. プロパティを追加する**

1. Blackboard 上部の **`+`（Add）** ボタンをクリックする
2. メニューから **Color** を選ぶ

**2-2. 名前を _BaseColor にする**

1. Blackboard の `Color` という項目名をダブルクリックする
2. `_BaseColor` と入力し、Enter を押す

> 先頭の `_` は、HLSL の変数名として `Properties` と対応させるためです。

**2-3. Show In Inspector にする**

1. Blackboard の `_BaseColor` をクリックして選択する
2. 右側の **Graph Inspector** の **Node Settings** に `Property: _BaseColor` が表示される
3. **Show In Inspector** の右側にある空の四角（チェックボックス）をクリックしてチェックを入れる

**2-4. Default Value を白にする**

初期値が黒のままだと、最後の計算結果がすべて黒になります。必ず白にしてください。

1. Graph Inspector の **Default Value** の右側にある色の見本（黒いバー）をクリックする
2. カラーピッカーが開くので、**HEX** の欄に `FFFFFFFF` と入力する（または A を 255、V を 100 にする）
3. カラーピッカーを閉じる

#### 手順 3：Normal Vector ノードを追加し、Space を World にする

**3-1. ノードを追加する**

1. Graph エリアの空いている場所で右クリックする
2. メニュー上部の検索欄をクリックし、`Normal Vector` と入力する
3. 候補の **Normal Vector** をクリックする

> 別の方法：Graph エリアの空いている場所をクリックしてからスペースキーを押すと、検索ウィンドウが開きます。

**3-2. Space を World にする**

1. Normal Vector ノードの中の **Space** の欄（初期値は `Object`）をクリックする
2. 一覧から **World** を選ぶ

欄の表示が `World` になれば完了です。

#### 手順 4：Main Light Direction ノードを追加する

1. Graph エリアの空いている場所で右クリックする
2. 検索欄に `Main Light Direction` と入力し、候補をクリックする

ノードの右側に **Direction(3)** の出力ポート（丸）が表示されます。

> 候補に出ない場合は、6.6 の Custom Function で代用します（後述の補足を参照）。

#### 手順 5：Dot Product ノードを追加し、2 つの入力を接続する

1. 右クリックして検索欄に `Dot Product` と入力し、クリックして配置する
2. **Normal Vector** の右側の出力ポート（`Out(3)`）から、**Dot Product** の **A(3)** の入力ポートまでドラッグして離す
3. **Main Light Direction** の **Direction(3)** の出力ポートから、**Dot Product** の **B(3)** の入力ポートまでドラッグして離す

A と B の両方に線がつながれば完了です。

#### 手順 6：Saturate ノードを追加する

1. Dot Product の右側の空いている場所で右クリックし、`Saturate` と入力してクリックする
2. Dot Product の出力ポート **Out(1)**（右端の丸）から、Saturate の入力ポート **In(1)** までドラッグして離す

> Saturate は、負の値を 0 に丸めます。ライトの裏側の面が負の明るさになるのを防ぎます。

#### 手順 7：Multiply ノードを追加し、_BaseColor と Saturate を接続する

1. 右クリックして検索欄に `Multiply` と入力し、クリックして配置する
2. Blackboard の `_BaseColor` の項目を、Graph エリアの空いている場所へドラッグする。色のノード（`_BaseColor(4)`）が置かれる
3. `_BaseColor(4)` の右側の出力ポートから、**Multiply** の **A(4)** の入力ポートまでドラッグして離す
4. **Saturate** の出力ポート **Out(1)** から、**Multiply** の **B(4)** の入力ポートまでドラッグして離す

> Blackboard からドラッグすると、プロパティのノードが自動で作られます。

#### 手順 8：Multiply の結果を Fragment の Base Color に接続する

1. **Multiply** の右側の出力ポート **Out(4)** から、**Fragment** の **Base Color(3)** の入力ポートまでドラッグして離す

これで「法線とライトの向きの内積 → 0〜1 に丸める → 色と掛ける → 最終色」が完成します。

![グラフ](images/Chapter09_graph01.png)

#### 手順 9：プレビューと生成コードを確認する

1. Main Preview（右下）を確認する。ライトの方向に対応した明暗のある球になっていれば成功
2. 画面上部の **View Generated Shader**（または Graph Inspector の Generated Code）を開く
3. `saturate(dot(...))` を含む行を探す。ノードの接続が、そのまま式になっていることが分かる

### 8.4 補足：Main Light Direction が見つからない場合

Create Node の検索で `Main Light Direction` が出ない場合は、Custom Function で同じ値を作ります。

1. Graph エリアで右クリックし、`Custom Function` を追加する
2. Graph Inspector の **Type** を **String** にし、次のコードを貼り付ける

```hlsl
void MainLightDir_float(out float3 Direction)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction = float3(0, 1, 0);
#else
    Direction = GetMainLight().direction;
#endif
}
```

3. 出力 **Direction(3)** を、Dot Product の **B(3)** に接続する

> `SHADERGRAPH_PREVIEW` の分岐は、プレビュー画面でエラーにならないための処理です。

### 8.5 生成コードとの対応

| ノード | HLSL の対応 |
| :--- | :--- |
| Normal Vector（World） | `normalize(IN.normalWS)` |
| Main Light Direction | `GetMainLight().direction` |
| Dot Product | `dot(N, L)` |
| Saturate | `saturate(...)` |
| Multiply | `_BaseColor.rgb * NdotL` |

### 8.6 確認ポイント

| 症状・確認内容 | 原因と対処 |
| :--- | :--- |
| プレビューが黒い | `_BaseColor` の Default Value が黒。白に変更する（手順 2-4） |
| 明暗が変わらない | Multiply の A と B の接続を確認する。B が Saturate につながっているか |
| Dot Product のプレビューが単色 | Normal Vector の Space が Object のまま。World にする |
| 線がつながらない | 出力ポート（右側の丸）から入力ポート（左側の丸）へ向けてドラッグしているか確認する |

- ライトを真上から当てると、上を向いた面が明るくなる
- ライトの裏側に回ると、その面は暗くなる（負の値が 0 に丸められている）
- `_BaseColor` を変えると、明るい部分の色が変わる

---

## 9. ワイプ演出の実装（Render Graph）

### 9.1 演出の設計

ワイプは、画面の一部を別の色で覆うことで場面転換を表す演出です。本章では次の 2 種類を実装します。

| モード | 形状 | 進行度 0 | 進行度 1 |
| :--- | :--- | :--- | :--- |
| **Iris（アイリス）** | 中心から円が閉じる | 画面全体が見える | 画面全体が覆われる |
| **Horizontal（水平）** | 左から右へ覆う | 画面全体が見える | 画面全体が覆われる |

**進行度 `_Progress`** を 0 から 1 へ動かすと、覆われる範囲が広がります。覆う境界には、輝く縁（エッジ）を付けます。

```mermaid
flowchart LR
    P["進行度 Progress<br/>0 → 1"] --> R["境界 R = 1 - Progress"]
    D["画面上の距離 d<br/>中心 0、角 1"] --> C{"d > R ?"}
    R --> C
    C -->|"Yes：覆う"| WC["WipeColor"]
    C -->|"No：見える"| SC["元の画像"]
    R --> E["|d - R| が小さい部分<br/>EdgeColor で縁を描く"]
```

**進行度と境界の関係**：`R = 1 - Progress` とすると、Progress が 0 のとき `R = 1` で、覆われる領域がありません。Progress が 1 のとき `R = 0` で、画面全体が覆われます。

### 9.2 シェーダー（WipeFullscreen09.shader）

`Assets/ShaderProgramming/Chapter09/WipeFullscreen09.shader` を作成します。

```shaderlab
Shader "ShaderProgramming/Chapter9/WipeFullscreen09"
{
	Properties
	{
		_WipeColor ("Wipe Color", Color) = (0,0,0,1)
		_EdgeColor ("Edge Color", Color) = (1,0.6,0.1,1)
		_Progress ("Progress", Range(0,1)) = 0
		_EdgeWidth ("Edge Width", Range(0,0.1)) = 0.02
		_Softness ("Softness", Range(0.001,0.3)) = 0.03
		_Mode ("Mode (0=Iris,1=Horizontal)", Float) = 0
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" }

		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "WipeFullscreen"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			half4 _WipeColor;
			half4 _EdgeColor;
			float _Progress;
			float _EdgeWidth;
			float _Softness;
			float _Mode;

			half4 Frag(Varyings input) : SV_Target
			{
				float2 uv = input.texcoord;

				// 距離 d を 0〜1 に正規化する
				float d;
				if (_Mode < 0.5)
				{
					// Iris：画面の縦横比を補正した中心からの距離（角で 1）
					float aspect = _ScreenParams.x / _ScreenParams.y;
					float2 p = (uv - 0.5) * float2(aspect, 1.0);
					float halfDiag = 0.5 * length(float2(aspect, 1.0));
					d = length(p) / halfDiag;
				}
				else
				{
					// Horizontal：左から右へ 0〜1
					d = uv.x;
				}

				// 境界 R：Progress 0 で 1（覆わない）、Progress 1 で 0（全部覆う）
				float R = 1.0 - _Progress;

				// 覆う領域（滑らかな境界）
				float mask = smoothstep(R - _Softness, R + _Softness, d);

				// 縁の線：境界付近だけを強める（進行中のみ）
				float active = step(0.001, _Progress) * step(_Progress, 0.999);
				float edge = (1.0 - smoothstep(0.0, _EdgeWidth, abs(d - R))) * active;

				// 元の画像（_BlitTexture は直前までの描画結果）
				half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

				half3 col = lerp(scene, _WipeColor.rgb, mask);
				col = lerp(col, _EdgeColor.rgb, edge);
				return half4(col, 1.0h);
			}
			ENDHLSL
		}
	}
}
```

**コードの要点**

- `smoothstep(R - _Softness, R + _Softness, d)` は、境界で 0 から 1 へ滑らかに変化する値を作ります。境界がぎざぎざにならないようにするためです。
- `active` は、Progress が 0 または 1 のとき縁を消すための係数です。完全に開いた状態や覆われた状態で、縁の線が残らないようにしています。
- `Blit.hlsl` の `_BlitTexture` は、Outline と同じく直前までのカラーです。

### 9.3 Renderer Feature と Render Pass（WipeRendererFeature.cs）

`Assets/ShaderProgramming/Chapter09/WipeRendererFeature.cs` を作成します。Feature とパスは同じファイルに入れています。

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class WipeRendererFeature : ScriptableRendererFeature
{
    public enum WipeMode { Iris = 0, Horizontal = 1 }

    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        public Shader shader;
        public WipeMode mode = WipeMode.Iris;
        public Color wipeColor = Color.black;
        public Color edgeColor = new Color(1f, 0.6f, 0.1f, 1f);
        [Range(0f, 0.1f)] public float edgeWidth = 0.02f;
        [Range(0.001f, 0.3f)] public float softness = 0.03f;
    }

    // 進行度（0 = 全部見える、1 = 全部覆われる）。WipeController から設定する
    public static float Progress;

    public Settings settings = new Settings();

    WipePass _pass;
    Material _material;

    public override void Create()
    {
        CoreUtils.Destroy(_material);
        _material = settings.shader != null ? CoreUtils.CreateEngineMaterial(settings.shader) : null;

        _pass = new WipePass
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        _pass.Setup(_material, settings);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }
}

public class WipePass : ScriptableRenderPass
{
    Material _material;
    WipeRendererFeature.Settings _settings;

    public void Setup(Material material, WipeRendererFeature.Settings settings)
    {
        _material = material;
        _settings = settings;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_material == null) return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        // 値はレコード時にマテリアルへ反映する
        _material.SetFloat("_Progress", WipeRendererFeature.Progress);
        _material.SetColor("_WipeColor", _settings.wipeColor);
        _material.SetColor("_EdgeColor", _settings.edgeColor);
        _material.SetFloat("_EdgeWidth", _settings.edgeWidth);
        _material.SetFloat("_Softness", _settings.softness);
        _material.SetFloat("_Mode", (float)_settings.mode);

        TextureHandle source = resourceData.activeColorTexture;

        TextureDesc desc = renderGraph.GetTextureDesc(source);
        desc.name = "_WipeTemp";
        desc.clearBuffer = false;
        desc.depthBufferBits = DepthBits.None;
        TextureHandle temp = renderGraph.CreateTexture(desc);

        // パス 1：ワイプを重ねて temp へ書く
        RenderGraphBlitUtil.AddCopyPass(renderGraph, source, temp, "Wipe09", _material);

        // パス 2：temp の結果をカメラのカラーへ戻す
        RenderGraphBlitUtil.AddCopyPass(renderGraph, temp, source, "Wipe09 Copy");
    }
}
```

**設計のポイント**

- `Progress` を `static` にしているのは、教材として C# からの値の受け渡しを単純にするためです。複数のカメラや複数のワイプを同時に扱う場合は、`Settings` 側に持たせる設計に変えてください。
- `RecordRenderGraph` の中で値をマテリアルに反映するため、同じフレームのワイプは、そのフレームの進行度で描かれます。
- `AddCopyPass` の第 3 引数に `Material` を渡すと、ワイプの描画になります。第 3 引数を省略すると、単純なコピーになります。

### 9.4 進行度を制御する（WipeController.cs）

`Assets/ShaderProgramming/Chapter09/WipeController.cs` を作成します。Space キーを押すたびに、覆う・戻すを切り替えます。

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class WipeController : MonoBehaviour
{
    [Tooltip("進行度が変化する速さ（1 秒あたり）")]
    public float speed = 1.5f;

    [Tooltip("true なら覆う方向、false なら戻す方向へ動く")]
    public bool covering = true;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            covering = !covering;
        }

        float target = covering ? 1f : 0f;
        WipeRendererFeature.Progress = Mathf.MoveTowards(
            WipeRendererFeature.Progress, target, speed * Time.deltaTime);
    }
}
```

**使い方**

1. Hierarchy で右クリックし、`Create Empty` で空の GameObject を作る。名前を `WipeManager` にする
2. `WipeManager` を選び、Inspector の **Add Component** に `WipeController` と入力して追加する
3. Play を押し、Space を押すと覆われ、もう一度押すと戻る

> `Keyboard.current` は Input System パッケージを使います。本プロジェクトは Input System の設定ファイル（`InputSystem_Actions.inputactions`）を持つため、そのまま使えます。

### 9.5 Renderer Data への登録と確認

**手順 1：Renderer Data を開く**

`Assets/Settings/PC_Renderer.asset` を Project ウィンドウでクリックします。

**手順 2：WipeRendererFeature を追加する**

1. Inspector の下部にある **Add Renderer Feature** を押す
2. メニューから **WipeRendererFeature** を選ぶ

**手順 3：設定を入力する**

追加された `Wipe Renderer Feature` の項目を開き、次のように設定します。

| 項目 | 設定値 |
| :--- | :--- |
| Render Pass Event | `After Rendering Post Processing`（既定のまま） |
| Shader | `WipeFullscreen09`（◎ボタンから選ぶ。または Project から Shader 欄へドラッグ＆ドロップ） |
| Mode | `Iris` |
| Wipe Color | 黒（既定のまま） |
| Edge Color | オレンジ（既定のまま） |

**手順 4：動作を確認する**

1. Play し、Space キーを押す
2. 画面が中心から円状に覆われ、覆われる境界にオレンジの縁が見えることを確認する
3. Mode を `Horizontal` に変えると、左から右へ覆われることを確認する（変更は再生を止めてから行ってください）
4. Render Graph Viewer で `Wipe09` と `Wipe09 Copy` が、`Outline08` の後に表示されていることを確認する

**確認ポイント**

| 確認項目 | 期待する状態 |
| :--- | :--- |
| 進行度 0 | 画面が通常どおり見える |
| 進行度 1 | 画面全体が Wipe Color で覆われる |
| 途中の状態 | 縁がオレンジの線で見える |
| Mode を Horizontal にする | 左から右へ覆われる |
| 輪郭線と併用 | 輪郭線の上にワイプが重なる（順序の確認） |

### 9.6 Shader Graph 版との比較（Full Screen Pass）

同じ演出は、Full Screen Pass Renderer Feature と Fullscreen 用の Shader Graph でも作れます。

| 項目 | C# の Render Pass（本章） | Full Screen Pass + Shader Graph |
| :--- | :--- | :--- |
| 作成の手間 | C# とシェーダーの両方を書く | シェーダーをグラフで作る |
| 制御 | 細かく制御できる（複数パス、MRT、別テクスチャ） | 1 枚のフルスクリーン処理に限られる |
| 進行度の受け渡し | `static` 変数やマテリアルのプロパティ | グラフの Exposed プロパティ |
| 学習の目的 | Render Graph の仕組みの理解 | 見た目の試行錯誤 |

**本章の方針**：ワイプは、Render Graph のパス構造を理解するための題材として C# で実装しました。Shader Graph で同じ見た目を作る課題は、発展として扱ってください。

---

## 10. 実習手順

1. **事前準備**：0.1 の確認（`URP_COMPATIBILITY_MODE` の削除、Render Graph の有効化）を行う
2. **EmptyRendererFeature の登録解除と削除**：0.2 の手順で行う
3. **OutlinePass の重複解消**：`Chapter08/OutlinePass.cs` を削除する（0.3）
4. **RenderGraphBlitUtil.cs の作成**：2.4 の内容で `Chapter09/` に作成する（0.4）
5. **Chapter09/OutlinePass.cs の作成**：2.3 の内容で作成する
6. **OutlineRendererFeature.cs の修正**：`Dispose` から `_pass?.Dispose();` を削除する（2.5）
7. **輪郭線の確認**：2.6 の手順で Renderer Data に登録し、輪郭線と Render Graph Viewer の表示を確認する
8. **Lambert09 の作成**：8.3 の手順で作成し、プレビューと生成コードを確認する
9. **ワイプの実装**：`WipeFullscreen09.shader`、`WipeRendererFeature.cs`、`WipeController.cs` を作成し、9.5 の手順で登録する
10. **動作確認**：Space キーでワイプを動かし、Iris と Horizontal の違い、Render Graph Viewer での `Wipe09` の順序（`Outline08` の後）を確認する
11. **発展課題**：`Wipe Color` に画像を使えるように、Texture2D のプロパティを追加し、覆った部分に画像を表示させる

---

## 11. トラブルシューティング

| 症状 | 原因と対処 |
| :--- | :--- |
| CS0101：型 'OutlinePass' は既に定義されています | `Chapter08/OutlinePass.cs` と `Chapter09/OutlinePass.cs` が両方ある。Chapter08 の方を削除する（0.3） |
| CS0103：'RenderGraphBlitUtil' という名前は存在しません | `RenderGraphBlitUtil.cs` が無い、またはファイル名とクラス名が一致していない（0.4） |
| `RecordRenderGraph` の警告が出る | Execute 方式のパスが残っている。`EmptyRendererFeature` を登録から外す（0.2）。`URP_COMPATIBILITY_MODE` が残っている場合も同じ警告が出る（0.1） |
| パスが実行されない | `URP_COMPATIBILITY_MODE` が有効のまま。Render Graph 版のパスは、このシンボルがあると呼ばれない（0.1） |
| Feature の項目が赤く Missing と出る | 削除したスクリプトが Renderer Data に残っている。一覧から Remove する |
| 輪郭線やワイプが黒くなる | 読み取りの宣言（`UseTexture`）が不足している、または `source` と `temp` の順序が逆 |
| 深度を使うと線が出ない | Depth Texture が無効、Shader 欄が None、または `_CameraDepthTexture` の登録方法がバージョンに合っていない（2.3 の注意を参照） |
| ワイプが覆わない | `Progress` が 0 のまま。`WipeController` が `WipeManager` に付いているか、Console のエラーを確認する |
| 縁が画面全体に出る | `_EdgeWidth` が大きすぎる、または `active` が効いていない（Progress の値を確認） |
| Iris が楕円に見える | `_ScreenParams` の縦横比補正が効いていない。Game ビューの解像度を確認する |
| Space キーに反応しない | Input System が有効か（Project Settings の Active Input Handling）、Game ビューにフォーカスがあるか確認する |
| Lambert09 のプレビューが黒い | `_BaseColor` の Default Value が黒（8.3 の手順 2-4 で白にする） |
| Shader Graph のノードが見つからない | URP のバージョンによりノード名が異なる。Custom Function で代用する（8.4） |

---

## 12. まとめ

- 移行の前に、`URP_COMPATIBILITY_MODE` の削除、`EmptyRendererFeature` の登録解除、`Chapter08/OutlinePass.cs` の削除を行う。同名クラスの重複と、Execute 方式の残存が、コンパイルエラーと描画不良の主な原因になる
- 輪郭線は、`Execute` の命令を `RecordRenderGraph` の宣言と `SetRenderFunc` の実行に分けることで、Render Graph 版に移行できる
- Render パイプラインは、Pipeline Asset → Renderer Data → Feature → Pass → Render Graph という階層で組み立てられ、Render Graph は Record・Compile・Execute の 3 段階で実行される
- Render Graph Viewer では、パスの順序と、リソースの読み書き・寿命を読み取れる
- `TextureHandle` と読み書きの宣言によって、依存関係と一時テクスチャの寿命が自動的に管理される
- Shader Graph は、ノードの接続で HLSL の計算を表すツールであり、生成されたコードを確認することで、HLSL で学んだ内容と結びつけられる。`_BaseColor` の Default Value を白にしないと結果が黒くなる点に注意する
- ワイプは、進行度から境界を作り、滑らかな遷移と縁の線を重ねる演出であり、Render Graph のフルスクリーンパスとして実装できる

次の第 10 章では、Volume と Post Processing を扱い、本章で作った Render Graph のパスを、ブルームのような複数段階の画像処理へ応用します。