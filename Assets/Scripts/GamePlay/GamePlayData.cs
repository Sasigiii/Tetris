using System;
using System.Collections.Generic;

/// <summary>
/// 单个格子的状态数据，包含字母、是否是空格、是否已填充、所在列索引等信息。
/// </summary>
[Serializable]
public class CellData
{
    public char letter;
    public bool isBlank;
    public bool isFilled;
    public int columnIndex;
}

/// <summary>
/// 单行词组的数据结构，包含词条信息、格子数据数组、空格索引列表、所在行索引等信息。
/// </summary>
[Serializable]
public class WordRowData
{
    public WordEntry wordEntry;
    public CellData[] cells;
    public List<int> blankIndices;
    public int rowIndex;

    /// <summary>
    /// 判断当前词组是否已完成
    /// </summary>
    /// <returns></returns>
    public bool IsComplete()
    {
        foreach (int idx in blankIndices)
        {
            if (!cells[idx].isFilled)
                return false;
        }
        return true;
    }
    
    /// <summary>
    /// 获取当前词组中未填充的空格索引列表
    /// </summary>
    /// <returns></returns>
    public List<int> GetUnfilledBlankIndices()
    {
        var unfilled = new List<int>();
        foreach (int idx in blankIndices)
        {
            if (!cells[idx].isFilled)
                unfilled.Add(idx);
        }
        return unfilled;
    }
}

/// <summary>
/// 关卡流程状态机，由 <see cref="GamePlayManager"/> 驱动。
/// </summary>
public enum GameState
{
    Loading, // 加载关卡数据、等待 UI 布局就绪；不处理方块输入，倒计时暂停。
    Spawning, // 当前词组内生成新十字方块（选目标空格与干扰字母），随即进入 Falling
    Falling, // 方块下落中：接收移动/旋转/加速输入，检测是否触达目标行
    Checking, // 方块触达目标行，同步判定填字是否正确并更新分数/网格
    GroupTransition, // 当前词组全部完成，切换到下一组前的过渡（随即调用 StartGroup）
    LevelComplete, // 本关所有词组已完成或无可用词条；触发胜利结算
    GameOver, // 失败（错误过多或倒计时结束）；停止方块并触发失败结算
}
