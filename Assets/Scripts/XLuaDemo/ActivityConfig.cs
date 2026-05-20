using System;

namespace XLuaDemo
{
    [Serializable]
    public sealed class ActivityConfig
    {
        public string activityId;
        public string title;
        public string subtitle;
        public string heroName;
        public string heroDescription;
        public string ctaText;
        public string backgroundColor;
        public string accentColor;
        public string prefabBundle;
        public string prefabName;
        public string backgroundBundle;
        public string backgroundAsset;
        public RewardConfig[] rewards;
    }

    [Serializable]
    public sealed class RewardConfig
    {
        public string name;
        public int amount;
    }
}
