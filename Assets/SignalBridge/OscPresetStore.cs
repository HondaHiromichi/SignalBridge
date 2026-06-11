using System;
using System.Collections.Generic;
using UnityEngine;

// OSC 送信設定のプリセットを PlayerPrefs に保存/読み出しする static ヘルパ.
// 1 キーに JSON 配列でまとめて永続化する. PlayerPrefs/JsonUtility に依存するためアプリ層に置く
// (再利用コアの Osc/ は noEngineReferences のため UnityEngine API を使えない).
public static class OscPresetStore
{
    #region 定数

    private const string PrefsKey = "SignalBridge.OscPresets";

    #endregion

    #region Public メソッド

    // 保存済みプリセットを全件読み出す (なければ空リスト).
    public static List<OscPreset> LoadAll()
    {
        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return new List<OscPreset>();
        }

        PresetList list = JsonUtility.FromJson<PresetList>(json);
        return list != null && list.presets != null ? list.presets : new List<OscPreset>();
    }

    // プリセット一覧を保存する (全置換).
    public static void SaveAll(List<OscPreset> presets)
    {
        PresetList list = new PresetList { presets = presets ?? new List<OscPreset>() };
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }

    #endregion

    #region Private 型

    // JsonUtility はトップレベルの配列を扱えないため, リストを包むラッパで直列化する.
    [Serializable]
    private class PresetList
    {
        public List<OscPreset> presets = new List<OscPreset>();
    }

    #endregion
}
