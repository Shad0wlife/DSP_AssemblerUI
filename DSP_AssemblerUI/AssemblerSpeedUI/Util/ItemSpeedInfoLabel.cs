﻿using UnityEngine;
using UnityEngine.UI;

namespace DSP_AssemblerUI.AssemblerSpeedUI.Util
{
    internal class ItemSpeedInfoLabel
    {
        public GameObject GameObject { get; set; }
        public Text Value { get; set; }
        public int Index { get; set; }
        public bool IsInput { get; set; }
        public ContentSizeFitter Fitter { get; set; }
    }
}