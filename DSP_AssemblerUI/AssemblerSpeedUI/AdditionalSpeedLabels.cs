using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DSP_AssemblerUI.AssemblerSpeedUI
{
    public class AdditionalSpeedLabels
    {
        private readonly string speedTextPath;
        private const string OutputKeyBase = "assembler-speed-out-item";
        private const string InputKeyBase = "assembler-speed-in-item";
        
        private static readonly string[] ItemOutputKeys = { $"{OutputKeyBase}0", $"{OutputKeyBase}1", $"{OutputKeyBase}2" };
        private static readonly string[] ItemInputKeys = { $"{InputKeyBase}0", $"{InputKeyBase}1", $"{InputKeyBase}2" };
        
        private Dictionary<string, ItemSpeedInfoLabel> speedInfos = new Dictionary<string, ItemSpeedInfoLabel>();
        private int speedInfosOutCount = 0;
        private int speedInfosInCount = 0;
        private Vector3? vanillaSpeedPos = null;

        private ModLogger modLogger;
        private bool setupOutputLabels;
        private bool setupInputLabels;
        
        public AdditionalSpeedLabels(ModLogger modLogger, bool setupOutputLabels, bool setupInputLabels, string speedTextPath)
        {
            this.modLogger = modLogger;
            this.setupOutputLabels = setupOutputLabels;
            this.setupInputLabels = setupInputLabels;
            this.speedTextPath = speedTextPath;
        }

        /// <summary>
        /// Sets up all currently required and configured labels
        /// </summary>
        public void SetupLabels(int? productCount, int? inputCount)
        {
            //Output
            if (setupOutputLabels)
            {
                if (productCount.HasValue)
                {
                    SetupSidedLabels(productCount.Value, false);
                }
            }

            //Input
            if (setupInputLabels)
            {
                if (inputCount.HasValue)
                {
                    SetupSidedLabels(inputCount.Value, true);
                }
            }
        }

        /// <summary>
        /// Sets up the currently required labels for either input or output side
        /// </summary>
        /// <param name="itemCount">The number of items which currently need a label</param>
        /// <param name="isInput">Whether the labels are on the input or output side of the UI</param>
        public void SetupSidedLabels(int itemCount, bool isInput)
        {
            string[] matchingKeys = isInput ? ItemInputKeys : ItemOutputKeys;
            int loopCap = Math.Min(itemCount, matchingKeys.Length);

            for (int cnt = 0; cnt < loopCap; cnt++)
            {
                if (!speedInfos.ContainsKey(matchingKeys[cnt]))
                {
                    AddSpeedLabel(matchingKeys[cnt], cnt, loopCap, isInput);
                    if (isInput)
                    {
                        speedInfosInCount++;
                    }
                    else
                    {
                        speedInfosOutCount++;
                    }
                }
            }

            string perMinuteString = "每分钟".Translate();
            int matchingInfoCount = isInput ? speedInfosInCount : speedInfosOutCount;

            //Iterate only over the already created text labels for the side
            for (int cnt2 = 0; cnt2 < matchingInfoCount; cnt2++)
            {
                if (cnt2 < itemCount)
                {
                    //If it is a label that should be visible, set it up
                    PositionSpeedLabel(speedInfos[matchingKeys[cnt2]].GameObject, cnt2, loopCap, isInput);
                    speedInfos[matchingKeys[cnt2]].GameObject.SetActive(true);
                    speedInfos[matchingKeys[cnt2]].Value.text = "  0.0" + perMinuteString;
                }
                else
                {
                    //If the label exists, but the current assembler doesn't use it, set it to inactive
                    speedInfos[matchingKeys[cnt2]].GameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Adds and initalizes a new speed label when it is needed.
        /// </summary>
        /// <param name="id">The dictionary ID of the new label</param>
        /// <param name="num">Index of the new label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        public void AddSpeedLabel(string id, int num, int ofNum, bool input)
        {
            var originalDetailLabel = GameObject.Find(speedTextPath);
            if (originalDetailLabel == null)
            {
                throw new InvalidOperationException("Assembler speed base entry is not present");
            }

            GameObject gameObject = null;
            var originalDetailLabelText = originalDetailLabel.GetComponent<Text>();

            gameObject = Object.Instantiate(originalDetailLabel, originalDetailLabel.transform.position, Quaternion.identity);
            Object.Destroy(gameObject.GetComponentInChildren<Localizer>());

            gameObject.name = id;
            gameObject.transform.SetParent(originalDetailLabel.transform.parent);

            var textComponents = gameObject.GetComponentsInChildren<Text>();
            var value = textComponents[0];

            if (!vanillaSpeedPos.HasValue)
            {
                vanillaSpeedPos = originalDetailLabel.transform.localPosition;
            }

            gameObject.transform.localScale = new Vector3(1f, 1f, 1f);
            PositionSpeedLabel(gameObject, num, ofNum, input);
            gameObject.transform.right = originalDetailLabel.transform.right;

            //Input area is smaller, decrease font size
            if (input || ofNum > 1)
            {
                value.fontSize -= 2;
            }

            ItemSpeedInfoLabel newItemSpeedInfo = new ItemSpeedInfoLabel()
            {
                GameObject = gameObject,
                Value = value
            };
            speedInfos.Add(id, newItemSpeedInfo);
        }

        /// <summary>
        /// Sets the position of a given label.
        /// </summary>
        /// <param name="gameObject">The GameObject of the label to be moved.</param>
        /// <param name="num">Index of the label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        private void PositionSpeedLabel(GameObject gameObject, int num, int ofNum, bool input)
        {

            modLogger.DebugLog($"OgPosition:{gameObject.transform.localPosition}");
            Vector3 shiftVector = GetPosShift(num, ofNum, input);
            modLogger.DebugLog($"ShiftedBy:{shiftVector}");

            gameObject.transform.localPosition = vanillaSpeedPos.Value + shiftVector;
        }

        /// <summary>
        /// Gets the Vector3 by which the label is shifted compared to the original speed label.
        /// </summary>
        /// <param name="num">Index of the label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        /// <returns>The Vector3 by which the label shall be shifted</returns>
        private static Vector3 GetPosShift(int num, int ofNum, bool input)
        {
            float yShift = input ? 25f : -50f;
            float xShift = GetXShift(num, ofNum, input);

            return new Vector3(xShift, yShift, 0f);
        }

        /// <summary>
        /// Calculates the x-Shift of a Label, based on the label count and whether it's on the input or output side.
        /// </summary>
        /// <param name="num">Index of the label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        /// <returns>The x-Shift of the label</returns>
        private static float GetXShift(int num, int ofNum, bool input)
        {
            //based on:
            //float[][] xOutputLookup = new float[][] { new float[] { -60f }, new float[] { -125f, -60f }, new float[] { -190f, -125f, -60f } };
            //float[][] xInputLookup = new float[][] { new float[] { 74 }, new float[] { 74f, 125f }, new float[] { 74f, 125f, 176f } };
            if (input)
            {
                float baseX = 74f;
                float itemStep = 49f;
                return baseX + num * itemStep;
            }
            else
            {
                float baseX = -60f;
                float itemStep = -65f;
                return baseX + (ofNum - 1 - num) * itemStep;
            }
        }

        /// <summary>
        /// Updates a singular label with the current speed data
        /// </summary>
        /// <param name="id">The Dict-ID of the label to update</param>
        /// <param name="value">The value which to write in the label</param>
        /// <param name="input"></param>
        public void UpdateSpeedLabel(string id, float value, bool input)
        {
            var perMinuteString = "每分钟".Translate();
            var speedText = value.ToString("0.0").PadLeft(5) + perMinuteString;
            if (!input)
            {
                speedText += $" ({value / 60:0.0}/s)";
            }

            speedInfos[id].Value.text = speedText;
        }
        
        public void UpdateSpeedLabels(float baseSpeed, int[] productCounts, int[] requireCounts)
        {
            //Output
            if (setupOutputLabels)
            {
                for (int cnt = 0; cnt < Math.Min(productCounts.Length, ItemOutputKeys.Length); cnt++)
                {
                    UpdateSpeedLabel(ItemOutputKeys[cnt], productCounts[cnt] * baseSpeed, false);
                }
            }

            //Input
            if (setupInputLabels)
            {
                for (int cnt = 0; cnt < Math.Min(requireCounts.Length, ItemInputKeys.Length); cnt++)
                {
                    UpdateSpeedLabel(ItemInputKeys[cnt], requireCounts[cnt] * baseSpeed, true);
                }
            }
        }
        
        public void Destroy()
        {
            foreach(var pair in speedInfos)
            {
                Object.Destroy(pair.Value.GameObject);
            }
        }
    }
}