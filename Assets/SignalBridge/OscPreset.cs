using System;

// OSC 送信設定 1 件分のプリセット. PlayerPrefs に JSON で永続化するためのデータ保持クラス.
// JsonUtility でシリアライズするため public フィールドで持つ (UnityEngine 非依存).
[Serializable]
public class OscPreset
{
    public string name;
    public string ip;
    public string port;
    public string address;
    public string typeTags;
    public string values;
}
