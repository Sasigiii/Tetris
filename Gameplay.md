# Gameplay 文档

## 1. 目标

- 基于词库按关卡生成单词序列，每关默认 10 词
- 同屏展示 4 行单词容器，每行随机挖空 1~3 个字符
- 生成十字形 5 字母下落块（上、左、中、右、下）
- 玩家可移动、旋转、软降、硬降
- 下落过程中实时检测并填补正确空位
- 行补全后加分并刷新为后续单词

## 2. 当前核心规则

### 2.1 单词生成

- 读取 `GameContext.CurrentLexicon`、`GameContext.CurrentLevel`
- 结合 `LexiconConfig.wordsPerLevel` 和 `LexiconConfig.maxWordLength`
- 仅保留 `word.Length <= UI槽位数` 且全字母单词
- 初始化时装填前 4 行，后续按完成行补入新词

### 2.2 挖空规则

- 每个激活行随机挖空 1~3 个位置
- 挖空位置不重复
- UI 显示 `_`，并在 `holeOpen[]` 标记可填

### 2.3 下落块字母规则

- 每个新块 5 字母
- 优先保证至少 1 个字母来自“所有未填空位”的正确答案集合
- 其余字母随机 A-Z

### 2.4 匹配时机

- 每次 `StepDown()` 成功下移一格后立即尝试匹配
- `HardDrop()` 每下移一步都尝试匹配
- 匹配成功立刻刷新该行显示与分数
- 落地时仍执行一次统一结算，未命中的格子不计分

## 3. 重合与匹配判定

### 3.1 重合判定（下落停止条件）

- 纵向：任意小格 `y < _minGridY` 即判定碰撞
- 横向：仅当“所有小格都在列范围外”才判定碰撞
- 也就是允许部分小格越出左右边界，只要至少 1 格仍在有效列内

### 3.2 匹配判定（`ApplyPieceMatches`）

对 5 个小格逐个检查，全部通过才填空：

- 由当前小格 `gridY` 映射出 `uiRow`
- `uiRow` 对应行存在且激活
- `x` 在 `answer` 长度范围内
- 该位 `holeOpen[x] == true`
- 输入字母（块字母）等于目标字母（答案字母）

成功后：

- `holeOpen[x] = false`
- `displayChars[x] = expected`
- `View.RenderRow(uiRow, row)`
- `Model.score += 10`

### 3.3 行完成判定

- 一行 `holeOpen[]` 全为 `false` 视为完成
- 完成行额外 `+100`
- 若还有后续词则替换为新词，否则置为非激活行

## 4. 输入映射

- 左移：`A` / `←` / 小键盘 `4`
- 右移：`D` / `→` / 小键盘 `6`
- 旋转：`W` / `↑` / 小键盘 `8`
- 软降：`S` / `↓` / 小键盘 `2`
- 硬降：`Space`

## 5. 关键脚本

- `Assets/Scripts/UI/Panels/GamePlayUIController.cs`
- `Assets/Scripts/UI/Panels/GamePlayUIView.cs`
- `Assets/Scripts/UI/Panels/GamePlayUIModel.cs`
- `Assets/Scripts/UI/Panels/ChooseUIController.cs`
- `Assets/Resources/UIConfig.json`

## 6. 预制体绑定要求

Prefab：`Assets/Resources/UIPanel/GamePlayUI.prefab`

- `returnBtn`
- `playArea`
- `blockItem`
- `blockLetters[0..4]`
- `scoreText`
- `levelText`
- `remainText`
- `wordRows[0..3]`
  - `container`
  - `slotTexts[0..N]`（建议每行完整从左到右绑定）

## 7. 日志排查建议

- 匹配链路日志前缀：`[GamePlayUI] MatchFlow`
- 重点看：
  - `Cell[i] matched`
  - `Cell[i] mismatch`
  - `Cell[i] skip`
  - `ResolveUiRow miss`
  - `RefreshCompletedRows`
