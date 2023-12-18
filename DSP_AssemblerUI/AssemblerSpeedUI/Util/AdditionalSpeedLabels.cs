using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DSP_AssemblerUI.AssemblerSpeedUI.Util
{
    public class AdditionalSpeedLabels
    {
        private readonly string speedTextPath;
        private const string OutputKeyBase = "assembler-speed-out-item";
        private const string InputKeyBase = "assembler-speed-in-item";
        private const int safetyIndexLimit = 5;
        
        private Dictionary<string, ItemSpeedInfoLabel> speedInfos = new Dictionary<string, ItemSpeedInfoLabel>();
        private int speedInfosOutCount = 0;
        private int speedInfosInCount = 0;
        private Vector3? vanillaSpeedPos = null;

        private int currentOutputs = 1;
        private int currentInputs = 1;

        private ModLogger modLogger;
        private bool setupOutputLabels;
        private bool setupInputLabels;

        /// <summary>
        /// The actual formatstring. Gets the value directly to avoid the extra call to ShownDecimals' getter.
        /// No idea if that would even affect anything, but still.
        /// </summary>
        private string DecimalFormatString
        {
            get
            {
                return "F" + AssemblerSpeedUIMod.configShownDecimalPlaces.Value;
            }
        }
        
        public AdditionalSpeedLabels(ModLogger modLogger, bool setupOutputLabels, bool setupInputLabels, string speedTextPath)
        {
            this.modLogger = modLogger;
            this.setupOutputLabels = setupOutputLabels;
            this.setupInputLabels = setupInputLabels;
            this.speedTextPath = speedTextPath;

            //Devices without inputs will be created with 0 current inputs, just in case
            this.currentInputs = setupInputLabels ? 1 : 0;
        }

        /// <summary>
        /// Pad The numbers with 2 leading spaces so that there is spacing between values.
        /// </summary>
        /// <param name="f">The value to be formatted.</param>
        /// <returns>The formatted string.</returns>
        private string MakeFormattedPaddedLabelString(float f)
        {
            return "  " + f.ToString(DecimalFormatString);
        }

        private static string ItemInputKey(int index)
        {
            return string.Concat(InputKeyBase, index);
        }

        private static string ItemOutputKey(int index)
        {
            return string.Concat(OutputKeyBase, index);
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
            Func<int, string> keyFunc = isInput ? (Func<int, string>)ItemInputKey : ItemOutputKey;
            int loopCap = Math.Min(itemCount, safetyIndexLimit);

            if (isInput)
            {
                currentInputs = loopCap;
            }
            else
            {
                currentOutputs = loopCap;
            }

            for (int cnt = 0; cnt < loopCap; cnt++)
            {
                if (!speedInfos.ContainsKey(keyFunc(cnt)))
                {
                    AddSpeedLabel(keyFunc(cnt), cnt, loopCap, isInput);
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
                    speedInfos[keyFunc(cnt2)].Value.text = MakeFormattedPaddedLabelString(0f) + perMinuteString;
                    speedInfos[keyFunc(cnt2)].GameObject.SetActive(true);
                    TryStartPositioningOnLabel(speedInfos[keyFunc(cnt2)], loopCap);
                }
                else
                {
                    //If the label exists, but the current assembler doesn't use it, set it to inactive
                    speedInfos[keyFunc(cnt2)].GameObject.SetActive(false);
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
            modLogger.DebugLog($"originalDetailLabelText was:{originalDetailLabelText.text}");

            //Object.Instantiate creates a copy of an original object
            gameObject = Object.Instantiate(originalDetailLabel, originalDetailLabel.transform.position, Quaternion.identity);
            Object.Destroy(gameObject.GetComponentInChildren<Localizer>());

            //ContentSizeFitter to somewhat accurately measure text label size
            ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            gameObject.name = id;
            gameObject.transform.SetParent(originalDetailLabel.transform.parent);

            var textComponents = gameObject.GetComponentsInChildren<Text>();
            var value = textComponents[0];

            if (!vanillaSpeedPos.HasValue)
            {
                vanillaSpeedPos = originalDetailLabel.transform.localPosition;
            }

            gameObject.transform.localScale = new Vector3(1f, 1f, 1f);
            gameObject.transform.right = originalDetailLabel.transform.right;

            ItemSpeedInfoLabel newItemSpeedInfo = new ItemSpeedInfoLabel()
            {
                GameObject = gameObject,
                Value = value,
                Index = num,
                IsInput = input,
                Fitter = fitter
            };
            speedInfos.Add(id, newItemSpeedInfo);

            TryStartPositioningOnLabel(newItemSpeedInfo, ofNum);
        }

        /// <summary>
        /// Sets the position of a given label.
        /// </summary>
        /// <param name="gameObject">The GameObject of the label to be moved.</param>
        /// <param name="num">Index of the label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        private System.Collections.IEnumerator PositionSpeedLabel(ItemSpeedInfoLabel labelData, int ofNum)
        {
            modLogger.DebugLog($"Working on label {labelData.GameObject.name} which is input? {labelData.IsInput}");
            modLogger.DebugLog($"OgPosition:{labelData.Fitter.transform.localPosition}");

            RectTransform targetTransform = (RectTransform)labelData.Fitter.transform;
            float sizeLimit = labelData.IsInput ? Constants.INPUT_MAXWIDTH : Constants.OUTPUT_MAXWIDTH;

            yield return null;

            //Increase somewhat fast up to 12
            while(targetTransform.rect.width < (sizeLimit - 5) && labelData.Value.fontSize < 12)
            {
                labelData.Value.fontSize = Math.Min(12, labelData.Value.fontSize + 4);
                yield return null;
            }

            //Approach size from above
            while(targetTransform.rect.width > sizeLimit)
            {
                labelData.Value.fontSize -= 1;
                yield return null;
            }

            Vector3 shiftVector = GetPos(labelData.Index, ofNum, labelData.IsInput, vanillaSpeedPos.Value.z, targetTransform.rect.width);

            modLogger.DebugLog($"Shifted to:{shiftVector} from {targetTransform.localPosition}");

            targetTransform.localPosition = shiftVector;
        }

        /// <summary>
        /// Safely call the coroutine on the labels.
        /// This is done by checking if the corresponding GameObject is acually active before starting the coroutine.
        /// </summary>
        /// <param name="labelData">The label to position.</param>
        /// <param name="ofNum">The number of given labels.</param>
        private void TryStartPositioningOnLabel(ItemSpeedInfoLabel labelData, int ofNum)
        {
            if (labelData.GameObject.activeInHierarchy)
            {
                modLogger.DebugLog($"Starting coroutine on {labelData.GameObject.name} since it is active and all its parents are too.");
                labelData.Fitter.StartCoroutine(PositionSpeedLabel(labelData, ofNum));
            }
        }

        /// <summary>
        /// Gets the Vector3 by which the label is shifted compared to the original speed label.
        /// </summary>
        /// <param name="num">Index of the label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        /// <returns>The Vector3 by which the label shall be shifted</returns>
        private static Vector3 GetPos(int num, int ofNum, bool input, float z, float width)
        {

            float yPos = input ? Constants.INPUT_YPOS : Constants.OUTPUT_YPOS;
            float xPos = GetXShift(num, ofNum, input, width);

            return new Vector3(xPos, yPos, z);
        }

        /// <summary>
        /// Calculates the x-Shift of a Label, based on the label count and whether it's on the input or output side.
        /// </summary>
        /// <param name="num">Index of the label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        /// <returns>The x-Shift of the label</returns>
        private static float GetXShift(int num, int ofNum, bool input, float width)
        {
            if (input)
            {
                float baseX = Constants.INPUT_FIRST_X_OFFSET;
                float itemStep = Constants.INPUT_X_ITEMSIZE;
                return baseX - width/2 + num * itemStep;
            }
            else
            {
                float baseX = Constants.OUTPUT_FIRST_X_OFFSET;
                float itemStep = -Constants.OUTPUT_X_ITEMSIZE;
                return baseX - width/2 + (ofNum - 1 - num) * itemStep;
            }
        }

        /// <summary>
        /// Updates a singular label with the current speed data
        /// </summary>
        /// <param name="id">The Dict-ID of the label to update</param>
        /// <param name="value">The value which to write in the label</param>
        /// <param name="input"></param>
        public void UpdateSpeedLabel(string id, float value, bool perSecond)
        {
            string perMinuteString = "每分钟".Translate();
            string perSecondString = "/s";
            string speedText;
            if (perSecond)
            {
                speedText = MakeFormattedPaddedLabelString(value/60f) + perSecondString;
            }
            else
            {
                speedText = MakeFormattedPaddedLabelString(value) + perMinuteString;
            }

            bool labelChange = speedText != speedInfos[id].Value.text;

            if (labelChange)
            {
                speedInfos[id].Value.text = speedText;
                TryStartPositioningOnLabel(speedInfos[id], speedInfos[id].IsInput ? currentInputs : currentOutputs);
            }
        }
        
        public void UpdateSpeedLabels(float baseSpeed, int[] productCounts, int[] requireCounts)
        {

            //Output
            if (setupOutputLabels)
            {
                for (int cnt = 0; cnt < Math.Min(productCounts.Length, safetyIndexLimit); cnt++)
                {
                    UpdateSpeedLabel(ItemOutputKey(cnt), productCounts[cnt] * baseSpeed, AssemblerSpeedUIMod.configOutputSpeedsPerSecond.Value);
                }
            }

            //Input
            if (setupInputLabels)
            {
                for (int cnt = 0; cnt < Math.Min(requireCounts.Length, safetyIndexLimit); cnt++)
                {
                    UpdateSpeedLabel(ItemInputKey(cnt), requireCounts[cnt] * baseSpeed, AssemblerSpeedUIMod.configInputSpeedsPerSecond.Value);
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